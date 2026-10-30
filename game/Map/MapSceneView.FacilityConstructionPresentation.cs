using Godot;
using San14.Core;
using San14.Data;

internal sealed record FacilityConstructionDispatch(bool Accepted,string? UnresolvedInput=null);

public partial class MapSceneView
{
    private D3D11FacilityPublisher? _d3d11Facilities;
    private IReadOnlyList<FacilitySceneConnection> _facilityConnections = Array.Empty<FacilitySceneConnection>();
    private sealed record FacilityModelWaiter(D3D11FacilityPublication Expected,
        TaskCompletionSource<FacilityConstructionDispatch> Completion);
    private readonly Dictionary<FacilityConstructionPresentationRequest,FacilityModelWaiter>
        _facilityModelWaiters = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<FacilityConstructionPresentationRequest,Task<FacilityConstructionDispatch>>
        _facilityDispatches = new(ReferenceEqualityComparer.Instance);
    private D3D11FacilityPublication? _acceptedFacilityModels;

    internal void AcceptFacilityConstructionModels(D3D11FacilityPublication? publication)
    {
        if (publication is not { IsReady:true }) return;
        _acceptedFacilityModels=publication;
        foreach (var pair in _facilityModelWaiters.ToArray())
            if (ReferenceEquals(publication,pair.Value.Expected)&&FacilityModelInstalled(publication,pair.Key))
            { pair.Value.Completion.TrySetResult(new(true)); _facilityModelWaiters.Remove(pair.Key); }
    }

    internal Task<FacilityConstructionDispatch> DispatchFacilityConstructionPresentationAsync(
        FacilityConstructionPresentationRequest request,GameState world,SceneAudioBinding? audio,CancellationToken cancellation)
    {
        if (_facilityDispatches.TryGetValue(request,out var existing)) return existing.WaitAsync(cancellation);
        EnsureCaptureFeedbackLifetime();
        var task=Dispatch();
        _facilityDispatches.Add(request,task);
        return task.WaitAsync(cancellation);

        async Task<FacilityConstructionDispatch> Dispatch()
        {
            try
            {
                var token=_monthlyFeedbackLifetime!.Token;
                if (!D3D11MapInteractive || _d3d11Facilities is null)
                    return new(false,"facility.presentation.native_model_owner");
                if (request.Operation is FacilityConstructionPresentationOperation.EnsureInitialFacilityModel or
                    FacilityConstructionPresentationOperation.NotifyFacilityCreated or
                    FacilityConstructionPresentationOperation.ConnectAdjacentFacility)
                {
                    if (request.Operation==FacilityConstructionPresentationOperation.ConnectAdjacentFacility)
                    {
                        var other=request.AdjacentReceiverId ?? throw new InvalidDataException("facility.connection.second_receiver");
                        if (!_facilityConnections.Any(p=>p.ReceiverId==request.ReceiverId&&p.AdjacentReceiverId==other ||
                            p.ReceiverId==other&&p.AdjacentReceiverId==request.ReceiverId))
                            _facilityConnections=Array.AsReadOnly(_facilityConnections.Append(new(request.ReceiverId,other)).ToArray());
                    }
                    var publication=_d3d11Facilities.Capture(world,_d3d11CommonFrustumMode,_facilityConnections);
                    if (!publication.IsReady) return new(false,string.Join("; ",publication.Missing.Select(m=>m.InstanceKey+": "+m.Reason)));
                    //Ready CPU packets do not stand in for GPU installation.
                    if (ReferenceEquals(publication,_acceptedFacilityModels)&&FacilityModelInstalled(publication,request))
                        return CompleteFacilityModelInstallation(publication,request);
                    if (!FacilityModelInstalled(publication,request))
                        return new(false,"facility.presentation.request_model_not_published:"+request.ReceiverId.Value);
                    var waiter=new TaskCompletionSource<FacilityConstructionDispatch>(TaskCreationOptions.RunContinuationsAsynchronously);
                    _facilityModelWaiters.Add(request,new(publication,waiter));
                    using var registration=token.Register(()=>waiter.TrySetCanceled(token));
                    ScheduleD3D11MapFrame();
                    var installed=await waiter.Task;
                    return installed.Accepted?CompleteFacilityModelInstallation(publication,request):installed;
                }
                if (TerrainMeshes is null || _sourceMainCameraFrame is not { } camera)
                    return new(false,"facility.presentation.current_map_geometry");
                var point=CeHexWorldCoordinates.Project(request.HexId);
                var position=new Vector3((float)point.X,MapGrounding.SampleHeight(TerrainMeshes,point.X,point.Z),(float)point.Z);
                if (request.Operation==FacilityConstructionPresentationOperation.PlaySiteOwnershipChangedSound)
                {
                    if (audio is null) return new(false,"facility.presentation.scene_audio_owner");
                    var clip=System.Numerics.Vector4.Transform(new System.Numerics.Vector4(position.X,position.Y,position.Z,1),camera.Camera.Matrices.WorldToProjection100);
                    float x=clip.X/clip.W,y=clip.Y/clip.W,z=clip.Z/clip.W;
                    //405370's actual point visibility rejection is a completed
                    //attempt; an eligible attempt reaches the shared62-slot manager.
                    if (x> -1&&x<1&&y> -1&&y<1&&z>0&&z<1)
                        await DispatchCaptureSoundAsync(0xAD,position,audio,token);
                    return new(true);
                }
                if (request.Operation!=FacilityConstructionPresentationOperation.ShowSiteOwnershipChanged)
                    return new(false,"facility.presentation.operation");
                int force=request.ForceSlot ?? throw new InvalidDataException("facility.presentation.source_Force");
                byte color=world.Forces.GetValueOrDefault(force)?.ReportColorIndex
                    ?? throw new InvalidDataException("facility.presentation.Force49");
                Color tint=Colors.White;
                if (color<60)
                {
                    var value=(ForcePalette ?? throw new InvalidDataException("facility.presentation.palette")).FirstTableColor(color,2);
                    tint=new(value.Red/255f,value.Green/255f,value.Blue/255f,value.Alpha/255f);
                }
                var admitted=await AdmitCaptureVisualAsync(request,new(5,request.HexId,force,position,1.7f,tint),token);
                return new(admitted.Accepted||admitted.Culled,admitted.UnresolvedInput);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception error) { return new(false,error.Message); }
        }
    }

    private FacilityConstructionDispatch CompleteFacilityModelInstallation(
        D3D11FacilityPublication publication,FacilityConstructionPresentationRequest request)
    {
        if (request.Operation!=FacilityConstructionPresentationOperation.NotifyFacilityCreated) return new(true);
        if (_d3d11Markers is null) return new(false,"facility.presentation.ObjectInfoLayout_owner");
        //224070 only stores Object18 when the actual2C4830 model constructor
        //runs. Connection sub-units never satisfy the primary SourceUnit gate.
        bool sourceUnitPresent=publication.Rows.Any(row=>row.ReceiverId==request.ReceiverId.Value&&
            row.Identity=="facility:"+request.ReceiverId.Value&&row.Instance.SourceWorldState is not null);
        bool created=_d3d11Markers.TryNotifyFacilityCreatedFromAccepted(publication,request.ReceiverId.Value,sourceUnitPresent,out var missing);
        return new(created,missing);
    }

    private static bool FacilityModelInstalled(D3D11FacilityPublication publication,FacilityConstructionPresentationRequest request)
    {
        if (request.Operation==FacilityConstructionPresentationOperation.NotifyFacilityCreated)
            return publication.Source.KnownReceivers.Contains(request.ReceiverId.Value);
        string identity=request.Operation==FacilityConstructionPresentationOperation.ConnectAdjacentFacility
            ? "facility-connection:"+request.ReceiverId.Value+":"+request.AdjacentReceiverId!.Value.Value
            : "facility:"+request.ReceiverId.Value;
        return publication.Rows.Any(row=>row.Identity==identity ||
            request.Operation==FacilityConstructionPresentationOperation.ConnectAdjacentFacility&&
            row.Identity=="facility-connection:"+request.AdjacentReceiverId!.Value.Value+":"+request.ReceiverId.Value);
    }

    internal void ForgetFacilityConstructionPresentation(FacilityConstructionPresentationRequest request)
    { _facilityDispatches.Remove(request); _facilityModelWaiters.Remove(request); _captureEffects?.Forget(request); }
}
