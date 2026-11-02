using Godot;
using San14.Core;
using San14.Client.Gameplay;

public partial class Main
{
    private sealed class MonthlyFeedbackWorkspace(GameplaySession session, GameState world,
        MapSceneView map, CampaignEffectRequest request)
    {
        internal GameplaySession Session { get; } = session;
        internal GameState World { get; } = world;
        internal MapSceneView Map { get; } = map;
        internal CampaignEffectRequest Request { get; } = request;
        internal CancellationTokenSource Lifetime { get; } = new();
        internal bool Running { get; set; } = true;
    }

    private MonthlyFeedbackWorkspace? _monthlyFeedback;
    private bool HasMonthlyFeedback => _monthlyFeedback?.Running == true;
    private bool IsMonthlyFeedbackCurrent => _monthlyFeedback is { } feedback &&
        ReferenceEquals(_session, feedback.Session) && _residentMap == feedback.Map &&
        ReferenceEquals(PersonnelPresentationOwner(feedback.Session), feedback.World);

    private bool TryPresentMonthlyTerritory(PersonnelPresentationEntry entry, GameplaySession session,
        GameState world, MapSceneView map)
    {
        var request = entry.Request;
        if (_monthlyFeedback is { } previous && IsMonthlyFeedbackCurrent &&
            ReferenceEquals(previous.Request, request)) return previous.Running;
        CloseMonthlyFeedback();
        bool retirement=MapObjectPresentationSources.IsRetirement(entry.Source,request.MapObjectPresentation);
        var audio=_sceneAudioBinding;
        if (!retirement&&audio is null)
            throw new InvalidOperationException("Monthly map feedback needs the active scene audio owner.");
        var feedback = new MonthlyFeedbackWorkspace(session, world, map, request);
        _monthlyFeedback = feedback;
        RetirePeriodReportView(); CloseActionReport(); map.CloseCityCommandMenu();
        map.SetDetailsOpen(true, keepMapVisible: true); HideDeploymentHud();
        _ = Dispatch();
        return true;

        async Task Dispatch()
        {
            try
            {
                MonthlyTerritoryFeedbackDispatch result;
                bool obstacle=MapObjectPresentationSources.IsDestruction(entry.Source,request.MapObjectPresentation);
                if(retirement)
                    result=await map.DispatchMapObjectRetirementAsync(request.MapObjectPresentation!,world,feedback.Lifetime.Token);
                else if(obstacle)
                {
                    var projection=_campaignComposition!.PrepareArmyQueries(world).QueryOccupationObstaclePresentation(world,request.MapObjectPresentation!);
                    if(!projection.Available)throw new InvalidDataException(projection.UnresolvedInput);
                    result=await map.DispatchOccupationObstacleAsync(projection.Value!,audio!,feedback.Lifetime.Token);
                }
                else result=request.MapObjectPresentation is{} capture
                    ?await map.DispatchBaseCaptureFeedbackAsync(capture,audio!,feedback.Lifetime.Token)
                    :await map.DispatchMonthlyTerritoryFeedbackAsync(request.MonthlyMaintenance!,audio!,feedback.Lifetime.Token);
                if (_monthlyFeedback != feedback || !IsMonthlyFeedbackCurrent) return;
                //The actual source camera/pool may cull a visual attempt.
                //That terminal outcome is distinct from a missing consumer.
                if (result.ConsumersCompleted)
                {
                    bool completed=obstacle||retirement?session.TryCompleteOccupationMapObjectPresentation(world,entry.BatchSequence,entry.RequestIndex,request.MapObjectPresentation!):
                        session.TryCompletePersonnelPresentation(world,entry.BatchSequence,entry.RequestIndex);
                    if (completed)
                    {
                        if(retirement)map.ForgetMapObjectRetirementPresentation(request.MapObjectPresentation!);
                        map.ForgetCaptureFeedback(request);
                    }
                }
                else if (result.UnresolvedInput is { } missing)
                {GD.PushWarning("Monthly map feedback: " + missing);_personnelPresentationInspectedOwner=world;}
            }
            catch (OperationCanceledException) when (feedback.Lifetime.IsCancellationRequested) { }
            catch (Exception error)
            {
                if (_monthlyFeedback == feedback && IsMonthlyFeedbackCurrent)
                    GD.PushWarning("Monthly map feedback: " + error.Message);
            }
            finally
            {
                feedback.Running = false;
                if (_monthlyFeedback != feedback) feedback.Lifetime.Dispose();
                else if (GodotObject.IsInstanceValid(map) && _residentMap == map)
                {
                    map.SetDetailsOpen(false);
                    if (!_failed && !_shuttingDown && IsInsideTree()) RunUiAction(RefreshPlayControls);
                }
            }
        }
    }

    private void CloseMonthlyFeedback()
    {
        var feedback = _monthlyFeedback; _monthlyFeedback = null;
        if (feedback is null) return;
        feedback.Lifetime.Cancel();
        //A running dispatch disposes its borrowed token in its own finally.
        if (!feedback.Running) feedback.Lifetime.Dispose();
        if (feedback.Running && GodotObject.IsInstanceValid(feedback.Map) && _residentMap == feedback.Map)
            feedback.Map.SetDetailsOpen(false);
    }
}
