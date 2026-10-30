using Godot;
using San14.Core;
using San14.Data;

public partial class MapSceneView
{
    private sealed record ObjectRetirementModelWaiter(D3D11FacilityPublication Expected,
        TaskCompletionSource<MonthlyTerritoryFeedbackDispatch> Completion);
    private readonly Dictionary<MapObjectPresentationRequest,ObjectRetirementModelWaiter> _objectRetirementModelWaiters=
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<MapObjectPresentationRequest,Task<MonthlyTerritoryFeedbackDispatch>> _objectRetirementDispatches=
        new(ReferenceEqualityComparer.Instance);
    private CancellationTokenSource? _objectRetirementLifetime;
    private bool _objectRetirementExitBound;
    private MapObjectSourceState? _objectRetirementDirectorySource;
    private MapObjectSourceDirectory? _objectRetirementDirectory;

    internal Task<MonthlyTerritoryFeedbackDispatch> DispatchMapObjectRetirementAsync(
        MapObjectPresentationRequest request,GameState world,CancellationToken cancellation)
    {
        if(_objectRetirementDispatches.TryGetValue(request,out var existing))return existing.WaitAsync(cancellation);
        _objectRetirementLifetime??=new();
        if(!_objectRetirementExitBound){TreeExiting+=RetireMapObjectRetirementPresentation;_objectRetirementExitBound=true;}
        var pending=Dispatch();_objectRetirementDispatches.Add(request,pending);return pending.WaitAsync(cancellation);

        async Task<MonthlyTerritoryFeedbackDispatch> Dispatch()
        {
            try
            {
                var token=_objectRetirementLifetime.Token;token.ThrowIfCancellationRequested();
                if(request.Event!=MapObjectPresentationEvent.RetirementVisualDetach)
                    return Unavailable("event");
                if(!D3D11MapInteractive||_d3d11Facilities is null||_d3d11Markers is null)
                    return Unavailable("native_scene");
                if(_periodMapActive&&!ReferenceEquals(_periodMapOwner?.Candidate,world))
                    return Unavailable("borrowed_candidate_owner");
                var receiver=world.MapObjects?.Find(request.ReceiverId);
                if(receiver is not{Subject:null,Pose.Kind:MapObjectPoseKind.None})
                    return Unavailable("retired_receiver_owner");
                var source=(world.ResourceState as CampaignSourceKnowledge)?.MapObjectSource;
                if(source is null)return Unavailable("current_Object_directory");
                // The receiver directory is immutable campaign metadata. Its
                // PhysicalReceivers owner survives ordinary resource resets
                // and effect receipts; do not rebuild3001 entries per intent.
                if(!ReferenceEquals(source.PhysicalReceivers,_objectRetirementDirectorySource?.PhysicalReceivers))
                {_objectRetirementDirectory=new(source);_objectRetirementDirectorySource=source;}
                int physical=_objectRetirementDirectory!.PhysicalOf(request.ReceiverId);
                // Inspect the already accepted native ownership, not another
                // whole-world current capture. City primaries use their source
                // City/Object physical slot; Gates retain the actual4E binding.
                var bases=_d3d11PresentedInput?.CityGateWorld?.Source.Primary.Owners;
                if(bases is null)return Unavailable("accepted_base_model_owner");
                foreach(var primary in bases)
                    if((primary.CitySelection is not null&&primary.PhysicalSlot==physical)||
                        primary.Gate?.Definition.ObjectSlot==physical)
                        return Unavailable("base_primary_detachment:"+primary.Identity);
                var connections=CaptureFacilitySceneConnections(world);
                if(connections.Any(row=>row.ReceiverId==request.ReceiverId||row.AdjacentReceiverId==request.ReceiverId))
                    return Unavailable("paired_connection_still_published");
                var publication=_d3d11Facilities.Capture(world,_d3d11CommonFrustumMode,connections);
                if(!publication.IsReady)return Unavailable(string.Join(";",publication.Missing.Select(row=>row.Reason)));
                if(publication.Source.KnownReceivers.Contains(request.ReceiverId.Value)||
                    publication.Rows.Any(row=>row.ReceiverId==request.ReceiverId.Value))
                    return Unavailable("model_still_published");
                if(ReferenceEquals(publication,_acceptedFacilityModels))return Complete(publication,request);
                var accepted=new TaskCompletionSource<MonthlyTerritoryFeedbackDispatch>(TaskCreationOptions.RunContinuationsAsynchronously);
                _objectRetirementModelWaiters.Add(request,new(publication,accepted));
                using var registration=token.Register(()=>accepted.TrySetCanceled(token));
                if(!_periodMapActive)_logicalWorld=world;
                ScheduleD3D11MapFrame();return await accepted.Task;
            }
            catch(OperationCanceledException){throw;}
            catch(Exception error){return Unavailable(error.Message);}
        }
    }

    private void AcceptMapObjectRetirementModels(D3D11FacilityPublication? publication)
    {
        if(publication is not{IsReady:true})return;
        foreach(var pair in _objectRetirementModelWaiters.ToArray())
            if(ReferenceEquals(pair.Value.Expected,publication))
            {pair.Value.Completion.TrySetResult(Complete(publication,pair.Key));_objectRetirementModelWaiters.Remove(pair.Key);}
    }

    private MonthlyTerritoryFeedbackDispatch Complete(D3D11FacilityPublication expected,MapObjectPresentationRequest request)
    {
        if(!ReferenceEquals(expected,_d3d11PresentedInput?.CityGateWorld?.Facilities)||
            !ReferenceEquals(expected,_acceptedFacilityModels))return Unavailable("native_frame_not_accepted");
        if(_d3d11Markers is null)return Unavailable("paired_view_owner");
        return _d3d11Markers.TryConfirmObjectRetirementFromAccepted(expected,request.ReceiverId.Value,out var missing)
            ?new(true,true,null):Unavailable(missing??"paired_view_detachment");
    }

    private static MonthlyTerritoryFeedbackDispatch Unavailable(string field)=>new(false,false,"Object.retirement."+field);
    internal void ForgetMapObjectRetirementPresentation(MapObjectPresentationRequest request)
    {_objectRetirementDispatches.Remove(request);_objectRetirementModelWaiters.Remove(request);}
    private void FailMapObjectRetirementPresentation(string reason)
    {
        foreach(var waiter in _objectRetirementModelWaiters.Values)waiter.Completion.TrySetResult(Unavailable(reason));
        _objectRetirementModelWaiters.Clear();
    }
    private void RetireMapObjectRetirementPresentation()
    {
        _objectRetirementLifetime?.Cancel();_objectRetirementLifetime?.Dispose();_objectRetirementLifetime=null;
        _objectRetirementModelWaiters.Clear();_objectRetirementDispatches.Clear();
        _objectRetirementDirectory=null;_objectRetirementDirectorySource=null;
    }
}
