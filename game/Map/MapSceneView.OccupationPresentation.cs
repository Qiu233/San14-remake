using Godot;
using San14.Core;
using San14.Data;

public partial class MapSceneView
{
    private sealed record TerritoryDisplayWait(D3D11TerritoryPublication Target,
        TaskCompletionSource<bool> Completion);
    private readonly List<TerritoryDisplayWait> _occupationTerritoryWaits=[];
    private D3D11TerritoryPublication? _occupationTerritory;

    internal async Task<MonthlyTerritoryFeedbackDispatch> DispatchEncirclementPresentationAsync(
        GameState world,RegionEncirclementPublication publication,CancellationToken cancellation)
    {
        if(!D3D11MapInteractive||_d3d11Ownership is null)
            return new(false,false,"region.encirclement.presentation.native_map_owner");
        EnsureCaptureFeedbackLifetime();
        var target=_d3d11Ownership.Capture(world);
        // Animation is deferred. The actual final territory packet must still
        // pass through the normal native frame and its real completion callback.
        if(ReferenceEquals(_d3d11PresentedInput?.TerritoryOwnership,target))return new(true,true,null);
        var waiting=new TerritoryDisplayWait(target,new(TaskCreationOptions.RunContinuationsAsynchronously));
        _occupationTerritoryWaits.Add(waiting);_occupationTerritory=target;
        ScheduleD3D11MapFrame();
        try
        {
            await waiting.Completion.Task.WaitAsync(cancellation);
            return new(true,true,null);
        }
        finally
        {
            _occupationTerritoryWaits.Remove(waiting);
            if(_occupationTerritoryWaits.Count==0)_occupationTerritory=null;
        }
    }
    private void AcceptOccupationTerritory(D3D11TerritoryPublication? presented)
    {
        foreach(var waiting in _occupationTerritoryWaits)
            if(ReferenceEquals(waiting.Target,presented))waiting.Completion.TrySetResult(true);
    }
    private void RetireOccupationTerritory()
    {
        foreach(var waiting in _occupationTerritoryWaits)waiting.Completion.TrySetCanceled();
        _occupationTerritoryWaits.Clear();_occupationTerritory=null;
    }

    internal Task<MonthlyTerritoryFeedbackDispatch> DispatchOccupationObstacleAsync(
        OccupationObstaclePresentationPublication publication,SceneAudioBinding audio,CancellationToken cancellation)
    {
        var request=publication.Request;
        if(_baseCaptureDispatches.TryGetValue(request,out var pending))return pending;
        EnsureCaptureFeedbackLifetime();
        var dispatch=Dispatch();_baseCaptureDispatches.Add(request,dispatch);return dispatch;
        async Task<MonthlyTerritoryFeedbackDispatch> Dispatch()
        {
            using var lifetime=CancellationTokenSource.CreateLinkedTokenSource(cancellation,_monthlyFeedbackLifetime!.Token);
            try
            {
                lifetime.Token.ThrowIfCancellationRequested();
                if(!_loaded||TerrainMeshes is null||!IsInsideTree())return new(false,false,"occupation.obstacle.map_owner");
                var point=CeHexWorldCoordinates.Project(publication.HexId);
                Vector3 position=new((float)point.X,MapGrounding.SampleHeight(TerrainMeshes,point.X,point.Z),(float)point.Z);
                if(request.Event==MapObjectPresentationEvent.DestructionVisual)
                {
                    var visual=new MonthlyTerritoryVisual(publication.EffectId,publication.HexId,null,position,publication.Scale,null);
                    _baseCaptureVisuals.Add(request,visual);
                    var admission=await AdmitCaptureVisualAsync(request,visual,lifetime.Token);
                    return new(true,admission.Accepted,admission.UnresolvedInput){VisualCulled=admission.Culled};
                }
                if(request.Event!=MapObjectPresentationEvent.DestructionSound)return new(false,false,"occupation.obstacle.operation");
                var sound=await DispatchCaptureSoundAsync(publication.SoundId,position,audio,lifetime.Token);
                return new(true,true,null){AudioCulled=!sound.Allocated};
            }
            catch(OperationCanceledException){throw;}
            catch(Exception error){return new(false,false,error.Message);}
        }
    }
}
