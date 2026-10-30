using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private EventIllustrationView? _monthlyScene;
    private GameplaySession? _monthlySceneSession;
    private MapSceneView? _monthlySceneMap;
    private ResourceNoticeWorkspace? _resourceNotice;
    private MapSceneView? _foodWarningMap;
    private GameplaySession? _foodWarningSession;
    private object? _foodWarningCityFlags;

    private void RestoreFoodWarnings(GameplaySession session, GameState owner, MapSceneView map)
    {
        var cityFlags = owner.NativeBaseCaptureCurrent?.Cities;
        if (ReferenceEquals(_foodWarningMap, map) && ReferenceEquals(_foodWarningSession, session) &&
            ReferenceEquals(_foodWarningCityFlags, cityFlags)) return;
        var query = _campaignComposition!.PrepareArmyQueries(owner).QueryCurrentNormalTurnFoodWarnings(owner);
        map.ApplyFoodWarnings(query.Value ?? throw new InvalidDataException(query.UnresolvedInput));
        _foodWarningMap = map;
        _foodWarningSession = session;
        _foodWarningCityFlags = cityFlags;
    }

    private bool TryPresentFoodWarnings(PersonnelPresentationEntry entry, GameplaySession session,
        GameState owner, MapSceneView map)
    {
        var query = _campaignComposition!.PrepareArmyQueries(owner)
            .QueryNormalTurnFoodWarnings(owner, entry.Request.NormalTurnFoodWarningPresentation!);
        map.ApplyFoodWarnings(query.Value ?? throw new InvalidDataException(query.UnresolvedInput));
        _foodWarningMap = map;
        _foodWarningSession = session;
        _foodWarningCityFlags = owner.NativeBaseCaptureCurrent?.Cities;
        return session.TryCompletePersonnelPresentation(owner, entry.BatchSequence, entry.RequestIndex);
    }

    private sealed class ResourceNoticeWorkspace(GameplaySession session, GameState world, MapSceneView map)
    {
        internal GameplaySession Session { get; } = session;
        internal GameState World { get; } = world;
        internal MapSceneView Map { get; } = map;
        internal CancellationTokenSource Lifetime { get; } = new();
        internal bool Preparing { get; set; } = true;
    }

    private bool IsResourceNoticeCurrent => _resourceNotice is { } notice &&
        ReferenceEquals(_session, notice.Session) && ReferenceEquals(_residentMap, notice.Map) &&
        ReferenceEquals(PersonnelPresentationOwner(notice.Session), notice.World);

    private void RefreshMonthlyScene(GameplaySession session, GameState owner, MapSceneView map)
    {
        if (_resourceNotice is not null && IsResourceNoticeCurrent) return;
        if (owner.ActiveMonthlyScene is not { } active)
        { CloseMonthlyScene(); return; }
        if (active.Family == MonthlySceneFamily.PersonDeath)
        {
            var death = new PersonDeathReportRequest(PersonDeathReportOperation.EventEnter, active.EventKind, null, null);
            var publication = _campaignComposition!.PrepareArmyQueries(owner).QueryPersonDeathPresentation(owner, death);
            var illustration = publication.Value?.Illustration ?? throw new InvalidDataException(publication.UnresolvedInput);
            ShowMonthlyScene(session, map, illustration);
            return;
        }
        if (active.Family == MonthlySceneFamily.Coalition)
        {
            var coalitionRequest = new ForceCoalitionVisualRequest(ForceCoalitionVisualOperation.NarrationEnter, null)
                { SceneKind = active.EventKind };
            var publication = _campaignComposition!.PrepareArmyQueries(owner).QueryForceCoalitionIllustration(owner, coalitionRequest);
            ShowMonthlyScene(session, map, publication.Value ?? throw new InvalidDataException(publication.UnresolvedInput));
            return;
        }
        if (active.Family == MonthlySceneFamily.Emperor)
        {
            var emperorRequest = new MonthlyMaintenanceRequest(MonthlyMaintenanceOperation.EmperorSceneEnter, active.EventKind, null, null);
            var publication = _campaignComposition!.PrepareArmyQueries(owner).QueryEmperorScene(owner, emperorRequest);
            ShowMonthlyScene(session, map, publication.Value?.Illustration ?? throw new InvalidDataException(publication.UnresolvedInput));
            return;
        }
        if (active.Family == MonthlySceneFamily.Personnel)
        {
            var personnel = new MonthlyMaintenanceRequest(MonthlyMaintenanceOperation.PersonnelSceneEnter,
                active.EventKind, null, null);
            var publication = _campaignComposition!.PrepareArmyQueries(owner).QueryProposalScene(owner, personnel);
            ShowMonthlyScene(session, map, publication.Value ?? throw new InvalidDataException(publication.UnresolvedInput));
            return;
        }
        var request = new MonthlyMaintenanceRequest(MonthlyMaintenanceOperation.FinalOrderSceneEnter,
            active.EventKind, null, null);
        var query = _campaignComposition!.PrepareArmyQueries(owner).QueryNormalTurnFinalOrderScene(owner, request);
        var route = query.Value?.Illustration ?? throw new InvalidDataException(query.UnresolvedInput);
        ShowMonthlyScene(session, map, route);
    }

    private void ShowMonthlyScene(GameplaySession session, MapSceneView map, FinalOrderIllustrationRoute route)
    {
        if (_monthlyScene?.EventKind == route.IllustrationId && ReferenceEquals(_monthlySceneSession, session) &&
            ReferenceEquals(_monthlySceneMap, map)) return;
        CloseMonthlyScene();
        var view = new EventIllustrationView();
        _monthlyScene = view; _monthlySceneSession = session; _monthlySceneMap = map;
        AddChild(view);
        try { view.Configure(_repository!, route); }
        catch { CloseMonthlyScene(); throw; }
    }

    private bool TryPresentFinalOrderScene(PersonnelPresentationEntry entry, GameplaySession session,
        GameState owner, MapSceneView map)
    {
        var query = _campaignComposition!.PrepareArmyQueries(owner)
            .QueryNormalTurnFinalOrderScene(owner, entry.Request.MonthlyMaintenance!);
        var publication = query.Value ?? throw new InvalidDataException(query.UnresolvedInput);
        if (publication.Illustration is { } route) ShowMonthlyScene(session, map, route);
        else CloseMonthlyScene();
        //Animations are deferred. The real still has accepted its final state;
        //the following narration and sound retain their own ordered receipts.
        return session.TryCompletePersonnelPresentation(owner, entry.BatchSequence, entry.RequestIndex);
    }

    private bool TryPresentResourceNotice(PersonnelPresentationEntry entry, GameplaySession session,
        GameState owner, MapSceneView map)
    {
        var query = _campaignComposition!.PrepareArmyQueries(owner)
            .QueryNormalTurnForceResourceNotice(owner, entry.Request.NormalTurnForceResourceNotice!);
        var publication = query.Value ?? throw new InvalidDataException(query.UnresolvedInput);
        var audio = _sceneAudioBinding ?? throw new InvalidOperationException("Resource notice needs its scene audio owner.");
        ShowMonthlyScene(session, map, publication.Illustration);
        var notice = new ResourceNoticeWorkspace(session, owner, map);
        _resourceNotice = notice;
        RetirePeriodReportView(); CloseActionReport(); map.CloseCityCommandMenu();
        map.SetDetailsOpen(true, keepMapVisible: true); SetMapFocus(false); HideDeploymentHud();
        _ = PresentAsync();
        return true;

        async Task PresentAsync()
        {
            try
            {
                await audio.PlayNormalSoundAsync(publication.SoundId, notice.Lifetime.Token);
                if (_resourceNotice != notice || !IsResourceNoticeCurrent) return;
                var view = new DeploymentNarrationView { Name = "PersonnelPresentation", ZIndex = 95,
                    KeyboardInput = HandleGameplayNarrationKeyboard };
                _personnelPresentation = view; _personnelPresentationSession = session;
                _personnelPresentationWorld = owner; _personnelPresentationMap = map;
                AddChild(view);
                view.ConfigureAnnouncement(_repository!, _ui, publication.Text, () => RunUiAction(() =>
                {
                    if (_resourceNotice != notice || !IsResourceNoticeCurrent) return;
                    ClosePersonnelPresentation();
                    CloseMonthlyScene();
                    if (!session.TryCompletePersonnelPresentation(owner, entry.BatchSequence, entry.RequestIndex)) return;
                    if (!TryOpenPersonnelPresentation()) { RefreshPlayControls(); RefreshDiplomaticNegotiation(); }
                }), () => _preferences.AutomaticMessageAdvance,
                    () => CanAdvanceNarration && _personnelPresentation == view && IsResourceNoticeCurrent);
            }
            catch (OperationCanceledException) when (notice.Lifetime.IsCancellationRequested) { }
            catch (Exception error)
            {
                if (_resourceNotice == notice && IsResourceNoticeCurrent)
                { ClosePersonnelPresentation(); CloseMonthlyScene(); ReportPlayFailure(error); }
            }
            finally
            {
                notice.Preparing = false;
                if (_resourceNotice != notice) notice.Lifetime.Dispose();
            }
        }
    }

    private void CloseResourceNotice()
    {
        var notice = _resourceNotice; _resourceNotice = null;
        if (notice is null) return;
        notice.Lifetime.Cancel();
        if (!notice.Preparing) notice.Lifetime.Dispose();
        if (_residentMap == notice.Map && GodotObject.IsInstanceValid(notice.Map))
            notice.Map.SetDetailsOpen(false);
    }

    private void CloseMonthlyScene()
    {
        var view = _monthlyScene;
        _monthlyScene = null; _monthlySceneSession = null; _monthlySceneMap = null;
        if (view is null) return;
        view.Hide(); view.GetParent()?.RemoveChild(view); view.QueueFree();
    }
}
