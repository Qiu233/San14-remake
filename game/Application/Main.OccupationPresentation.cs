using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private sealed class EncirclementWorkspace(GameplaySession session,GameState world,MapSceneView map,
        RegionEncirclementPresentation request)
    {
        internal readonly GameplaySession Session=session;
        internal readonly GameState World=world;
        internal readonly MapSceneView Map=map;
        internal readonly RegionEncirclementPresentation Request=request;
        internal readonly CancellationTokenSource Lifetime=new();
        internal bool Running=true;
    }
    private EncirclementWorkspace? _encirclementPresentation;
    private bool HasEncirclementPresentation=>_encirclementPresentation?.Running==true;
    private bool IsEncirclementPresentationCurrent=>_encirclementPresentation is{} owner&&
        ReferenceEquals(_session,owner.Session)&&ReferenceEquals(PersonnelPresentationOwner(owner.Session),owner.World)&&_residentMap==owner.Map;
    private bool TryPresentEncirclement(GameplaySession session,GameState world,MapSceneView map,CampaignComposition composition)
    {
        var request=world.RegionOccupationFollowups!.EncirclementPresentations[0];
        var query=composition.PrepareArmyQueries(world).QueryRegionEncirclementPresentation(world,request);
        if(!query.Available){GD.PushWarning(query.UnresolvedInput);_personnelPresentationInspectedOwner=world;return false;}
        CloseEncirclementPresentation();var owner=new EncirclementWorkspace(session,world,map,request);_encirclementPresentation=owner;
        RetirePeriodReportView();CloseActionReport();map.CloseCityCommandMenu();
        map.SetDetailsOpen(true,keepMapVisible:true);SetMapFocus(false);HideDeploymentHud();
        map.RefreshPlayableWorld(world);_ = Dispatch();return true;
        async Task Dispatch()
        {
            try
            {
                var result=await map.DispatchEncirclementPresentationAsync(world,query.Value!,owner.Lifetime.Token);
                if(_encirclementPresentation!=owner||!IsEncirclementPresentationCurrent)return;
                if(result.ConsumersCompleted)session.TryCompleteRegionEncirclementPresentation(world,request);
                else{GD.PushWarning(result.UnresolvedInput);_personnelPresentationInspectedOwner=world;}
            }
            catch(OperationCanceledException) when(owner.Lifetime.IsCancellationRequested){}
            catch(Exception error){GD.PushWarning(error.Message);_personnelPresentationInspectedOwner=world;}
            finally
            {
                owner.Running=false;
                if(_encirclementPresentation!=owner)owner.Lifetime.Dispose();
                else if(GodotObject.IsInstanceValid(map)&&_residentMap==map)
                {map.SetDetailsOpen(false);if(!_failed&&!_shuttingDown&&IsInsideTree())RunUiAction(RefreshPlayControls);}
            }
        }
    }
    private void CloseEncirclementPresentation()
    {
        var owner=_encirclementPresentation;_encirclementPresentation=null;if(owner is null)return;
        owner.Lifetime.Cancel();if(!owner.Running)owner.Lifetime.Dispose();
    }

    private static PersonnelPresentationEntry? NextLiveOccupationEffect(GameState world)
    {
        foreach(var batch in world.CampaignEffectRequests?.PendingBatches??[])
        {
            if(!MapObjectPresentationSources.HasSource(batch.Source))continue;
            for(int index=0;index<batch.Requests.Length;index++)
            {
                var request=batch.Requests[index];if(request.HistoryNotification is not null)continue;
                // Another real ArmyMovement consumer still owns an earlier
                // request. Its source FIFO may not be skipped by this host.
                return request.MonthlyMaintenance is{Operation:MonthlyMaintenanceOperation.TerritoryOccupation,EventKind:4 or 5}||
                    MapObjectPresentationSources.Supports(batch.Source,request.MapObjectPresentation)
                    ?new(batch.Sequence,index,request,batch.Source):null;
            }
        }
        return null;
    }
    private void BeginPeriodOccupationPresentation(CampaignPeriodPlayback playback,MapSceneView? map,GameState candidate)
    {
        if(playback.OccupationPresentationOpen||map is null||_campaignComposition is not{} composition||
            !ReferenceEquals(playback.Candidate,candidate)||
            NextLiveOccupationEffect(candidate) is null&&candidate.RegionOccupationFollowups?.EncirclementPresentations.Count is not >0)return;
        playback.OccupationPresentationOpen=true;
        var token=_periodAdvanceCancellation?.Token??CancellationToken.None;_ = Dispatch();
        async Task Dispatch()
        {
            try
            {
                while(ReferenceEquals(_periodPlayback,playback)&&playback.Candidate is{} world&&_residentMap==map)
                {
                    token.ThrowIfCancellationRequested();
                    var rules=composition.PrepareArmyQueries(world);
                    if(NextLiveOccupationEffect(world) is{} entry)
                    {
                        bool retirement=MapObjectPresentationSources.IsRetirement(entry.Source,entry.Request.MapObjectPresentation);
                        var audio=_sceneAudioBinding;
                        if(!retirement&&audio is null)throw new InvalidDataException("occupation.presentation.scene_audio");
                        MonthlyTerritoryFeedbackDispatch result;
                        if(retirement)
                            result=await map.DispatchMapObjectRetirementAsync(entry.Request.MapObjectPresentation!,world,token);
                        else if(entry.Request.MapObjectPresentation is{} obstacle)
                        {
                            var query=rules.QueryOccupationObstaclePresentation(world,obstacle);
                            if(!query.Available)throw new InvalidDataException(query.UnresolvedInput);
                            result=await map.DispatchOccupationObstacleAsync(query.Value!,audio!,token);
                        }
                        else result=await map.DispatchMonthlyTerritoryFeedbackAsync(entry.Request.MonthlyMaintenance!,audio!,token);
                        if(!result.ConsumersCompleted)throw new InvalidDataException(result.UnresolvedInput);
                        if(!playback.TryCompleteOccupationEffect(world,entry.BatchSequence,entry.RequestIndex,entry.Request))return;
                        if(retirement)map.ForgetMapObjectRetirementPresentation(entry.Request.MapObjectPresentation!);
                        map.ForgetCaptureFeedback(entry.Request);
                    }
                    else if(world.RegionOccupationFollowups?.EncirclementPresentations.FirstOrDefault() is{} head)
                    {
                        var query=rules.QueryRegionEncirclementPresentation(world,head);
                        if(!query.Available)throw new InvalidDataException(query.UnresolvedInput);
                        var result=await map.DispatchEncirclementPresentationAsync(world,query.Value!,token);
                        if(!result.ConsumersCompleted)throw new InvalidDataException(result.UnresolvedInput);
                        if(!playback.TryCompleteEncirclement(world,head))return;
                    }
                    else return;
                    map.PublishPeriodPlayback(playback,playback.Candidate!);
                }
            }
            catch(OperationCanceledException) when(token.IsCancellationRequested){}
            catch(Exception error){playback.FailPresentation(error);}
            finally
            {
                playback.OccupationPresentationOpen=false;
                if(ReferenceEquals(_periodPlayback,playback)&&playback.Candidate is{} next&&_residentMap==map)
                    BeginPeriodDuelCaptureAnnouncement(playback,map,next);
            }
        }
    }
}
