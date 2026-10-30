using Godot;
using San14.Core;
using San14.Data;
using NMatrix = System.Numerics.Matrix4x4;

public partial class MapSceneView
{
    private sealed class FacilityLoopSlot(int receiver, int slot, int kind)
    {
        internal readonly int Receiver=receiver, Slot=slot, Kind=kind;
        internal Task<FacilityConstructionDispatch>? Pending;
    }
    private readonly Dictionary<(int Receiver,int Slot),FacilityLoopSlot> _facilityLoops=[];
    private readonly HashSet<int> _facilityRepairBoundReceivers=[];
    private readonly Dictionary<FacilityRepairPresentationRequest,Task<FacilityConstructionDispatch>> _facilityRepairDispatches=
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<D3D11FacilityPublication,TaskCompletionSource<bool>> _facilityRepairModelWaiters=
        new(ReferenceEqualityComparer.Instance);
    private CancellationTokenSource? _facilityRepairLifetime;
    private FacilityRepairSceneFrame? _facilityRepairScene;
    private Task _facilityRepairRebinding=Task.CompletedTask;
    private bool _facilityRepairRebindingFailed;

    internal void AcceptFacilityRepairModels(D3D11FacilityPublication? publication)
    {
        if(publication is not {IsReady:true})return;
        if(_facilityRepairModelWaiters.Remove(publication,out var waiter))waiter.TrySetResult(true);
    }

    internal void BindFacilityRepairScene(GameState world,FacilityRepairSceneFrame frame,SceneAudioBinding? audio)
    {
        if(ReferenceEquals(_facilityRepairScene,frame))return;
        EnsureFacilityRepairLifetime();
        _facilityRepairScene=frame;
        var token=_facilityRepairLifetime!.Token;
        var previous=_facilityRepairRebinding;
        _facilityRepairRebinding=Rebind();
        async Task Rebind()
        {
            try
            {
                await previous;
                token.ThrowIfCancellationRequested();
                if(!ReferenceEquals(_facilityRepairScene,frame))return;
                _facilityRepairRebindingFailed=false;
                foreach(var row in frame.Rows)
                {
                    if(!ReferenceEquals(_facilityRepairScene,frame))return;
                    //1C8500 restores slots when binding a new Object view. A
                    //different immutable world is not a newly created view.
                    if(_facilityRepairBoundReceivers.Contains(row.ReceiverId))continue;
                    var model=await FacilityRepairModel(world,row.ReceiverId,token);
                    if(model is null)continue; //A known Object has no native SourceUnit.
                    if(row.RepairLoop is bool repair)
                    {
                        if(repair)RequireRepairAdmission(await EnsureFacilityLoop(row.ReceiverId,0,0x2A,model.Instance.SourceWorldState!.Position18,audio,token));
                    }
                    if(row.AmbientEffectCode is int ambient)
                        RequireRepairAdmission(await EnsureFacilityLoop(row.ReceiverId,1,ambient,model.Instance.SourceWorldState!.Position18,audio,token));
                    if(row.Missing is null)_facilityRepairBoundReceivers.Add(row.ReceiverId);
                }
                //An incomplete source frame must not erase a known scene slot.
                if(frame.Missing.Count==0&&ReferenceEquals(_facilityRepairScene,frame))
                {
                    var present=frame.Rows.Select(row=>row.ReceiverId).ToHashSet();
                    foreach(var key in _facilityLoops.Keys.Where(key=>!present.Contains(key.Receiver)).ToArray())
                    {
                        if(_facilityLoops.Remove(key,out var slot))
                        {
                            var retired=_captureEffects!.Retire(slot);ScheduleD3D11MapFrame();
                            await retired.WaitAsync(token);_captureEffects.Forget(slot);
                        }
                    }
                    _facilityRepairBoundReceivers.RemoveWhere(receiver=>!present.Contains(receiver));
                }
            }
            catch(OperationCanceledException) { }
            catch(Exception error)
            { _facilityRepairRebindingFailed=true; GD.PushWarning("Facility repair rebind: "+error.Message); }
        }
    }

    internal Task<FacilityConstructionDispatch> DispatchFacilityRepairPresentationAsync(
        FacilityRepairPresentationRequest request,GameState world,SceneAudioBinding? audio,CancellationToken cancellation)
    {
        if(_facilityRepairDispatches.TryGetValue(request,out var known))return known.WaitAsync(cancellation);
        EnsureFacilityRepairLifetime();
        var token=_facilityRepairLifetime!.Token;
        var pending=Dispatch();_facilityRepairDispatches.Add(request,pending);
        return pending.WaitAsync(cancellation);
        async Task<FacilityConstructionDispatch> Dispatch()
        {
            try
            {
                await _facilityRepairRebinding;
                token.ThrowIfCancellationRequested();
                if(_facilityRepairRebindingFailed)return new(false,"facility.repair.scene_rebind");
                int receiver=request.ReceiverId.Value;
                var model=await FacilityRepairModel(world,receiver,token);
                if(model?.Instance.SourceWorldState is not { } pose)return new(true);
                var point=pose.Position18;
                switch(request.Operation)
                {
                    case FacilityRepairPresentationOperation.EnsureRepairLoop:
                        return await EnsureFacilityLoop(receiver,0,0x2A,point,audio,token);
                    case FacilityRepairPresentationOperation.FinishRepairLoop:
                        await StopFacilityLoop(receiver,0,token);return new(true);
                    case FacilityRepairPresentationOperation.RefreshFacilityCompletionModel:
                        //FacilityRepairModel waited for this world's real GPU installation.
                        return new(true);
                    case FacilityRepairPresentationOperation.FacilityCompletionVisual:
                        var visualPending=_captureEffects!.Enqueue(request,0x4F,NMatrix.CreateTranslation(point),null,token);
                        ScheduleD3D11MapFrame();
                        var visual=await visualPending;
                        return new(visual.Completed,visual.UnresolvedInput);
                    case FacilityRepairPresentationOperation.FacilityCompletionSound:
                        if(audio is null)return new(false,"facility.repair.scene_audio");
                        await DispatchCaptureSoundAsync(0xC3,new(point.X,point.Y,point.Z),audio,token);
                        return new(true);
                    case FacilityRepairPresentationOperation.EnsureFacilityAmbientLoop:
                        int definition=_facilityRepairScene?.Rows.FirstOrDefault(row=>row.ReceiverId==receiver)?.DefinitionKind
                            ?? throw new InvalidDataException("facility.repair.current_definition");
                        int kind=definition switch
                            {7=>0x2E,8=>0x2D,_=>throw new InvalidDataException("facility.repair.ambient_definition")};
                        return await EnsureFacilityLoop(receiver,1,kind,point,audio,token);
                    default:return new(false,"facility.repair.operation");
                }
            }
            catch(OperationCanceledException) {throw;}
            catch(Exception error) {return new(false,error.Message);}
        }
    }

    private static void RequireRepairAdmission(FacilityConstructionDispatch result)
    {
        if(!result.Accepted)throw new InvalidDataException(result.UnresolvedInput??"facility.repair.loop_admission");
    }

    private void EnsureFacilityRepairLifetime()
    {
        if(_facilityRepairLifetime is not null)return;
        EnsureCaptureFeedbackLifetime();
        _facilityRepairLifetime=new();TreeExiting+=RetireFacilityRepairPresentation;
    }

    private async Task<FacilitySceneModel?> FacilityRepairModel(GameState world,int receiver,CancellationToken token)
    {
        if(!D3D11MapInteractive||_d3d11Facilities is null||_captureEffects is null)
            throw new InvalidDataException("facility.repair.native_scene");
        var publication=_d3d11Facilities.Capture(world,_d3d11CommonFrustumMode,_facilityConnections);
        if(!publication.IsReady)throw new InvalidDataException(string.Join("; ",publication.Missing.Select(item=>item.Reason)));
        if(!ReferenceEquals(publication,_acceptedFacilityModels))
        {
            if(!_facilityRepairModelWaiters.TryGetValue(publication,out var waiter))
            {
                waiter=new(TaskCreationOptions.RunContinuationsAsynchronously);
                _facilityRepairModelWaiters.Add(publication,waiter);
            }
            _logicalWorld=world;
            ScheduleD3D11MapFrame();await waiter.Task.WaitAsync(token);
        }
        if(!publication.Source.KnownReceivers.Contains(receiver))
            throw new InvalidDataException("facility.repair.Object_owner:"+receiver);
        return publication.Rows.FirstOrDefault(row=>row.ReceiverId==receiver&&row.Identity=="facility:"+receiver&&
            row.Instance.SourceWorldState is not null);
    }

    private Task<FacilityConstructionDispatch> EnsureFacilityLoop(int receiver,int slot,int kind,
        System.Numerics.Vector3 point,SceneAudioBinding? audio,CancellationToken token)
    {
        if(_facilityLoops.TryGetValue((receiver,slot),out var prior))return prior.Pending!;
        var owner=new FacilityLoopSlot(receiver,slot,kind);_facilityLoops.Add((receiver,slot),owner);
        owner.Pending=Start();return owner.Pending;
        async Task<FacilityConstructionDispatch> Start()
        {
            var pending=_captureEffects!.Enqueue(owner,kind,NMatrix.CreateTranslation(point),null,token);
            ScheduleD3D11MapFrame();
            //The SE attempt follows effect allocation even if visual admission is culled.
            if(slot==0)
            {
                if(audio is null)return new(false,"facility.repair.scene_audio");
                await DispatchCaptureSoundAsync(0xC2,new(point.X,point.Y,point.Z),audio,token);
            }
            var accepted=await pending;
            if(!accepted.Accepted)_facilityLoops.Remove((receiver,slot));
            return new(accepted.Completed,accepted.UnresolvedInput);
        }
    }

    private async Task StopFacilityLoop(int receiver,int slot,CancellationToken token)
    {
        if(!_facilityLoops.Remove((receiver,slot),out var owner))return;
        if(owner.Pending is { } pending)await pending.WaitAsync(token);
        var stopped=_captureEffects!.RequestStop(owner);ScheduleD3D11MapFrame();
        await stopped.WaitAsync(token);
        //The shared manager keeps the draining instance until its natural finish.
        _captureEffects.Forget(owner);
    }

    internal void ForgetFacilityRepairPresentation(FacilityRepairPresentationRequest request)
    { _facilityRepairDispatches.Remove(request);_captureEffects?.Forget(request); }

    internal void RetireFacilityRepairPresentation()
    {
        _facilityRepairLifetime?.Cancel();_facilityRepairLifetime?.Dispose();_facilityRepairLifetime=null;
        foreach(var slot in _facilityLoops.Values)_captureEffects?.Retire(slot);
        _facilityLoops.Clear();_facilityRepairBoundReceivers.Clear();_facilityRepairDispatches.Clear();_facilityRepairScene=null;
        foreach(var waiter in _facilityRepairModelWaiters.Values)waiter.TrySetCanceled();
        _facilityRepairModelWaiters.Clear();_facilityRepairRebinding=Task.CompletedTask;
    }
}
