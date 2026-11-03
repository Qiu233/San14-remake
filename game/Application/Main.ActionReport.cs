using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;
using San14.Client.Gameplay;

public partial class Main
{
    private ActionReportView? _actionReportView;
    private GameplaySession? _actionReportSession;
    private CampaignApplicationState? _actionReportApplication;
    private ICampaignActionReportSource? _actionReportSource;
    private ActionReportPeopleDisplay? _actionReportPeople;
    private MapSceneView? _actionReportMap;
    private bool _reportMapPriorFocus;
    private readonly Dictionary<ushort, LoadedUiSpriteSource> _actionReportFaces = [];
    private AdministrationPageChrome.Resources? _actionReportUiResources;
    private ReportMapReturnView? _reportMapReturn;
    private bool HasActionReport => _actionReportView is not null;

    private bool RefreshActionReport()
    {
        if (_repository is null || _session is not { PlayerForceId: not null } session ||
            session.World.Phase != TurnPhase.Reporting || _campaignApplication is not { } application ||
            _campaignComposition is not { } composition) return false;
        if (_actionReportView is not null && ReferenceEquals(_actionReportSession, session) &&
            ReferenceEquals(_actionReportApplication, application) && _actionReportMap == _residentMap)
        {
            EnsureReportBackground(session, application, _residentMap, _actionReportView);
            return true;
        }
        if (ReferenceEquals(_actionReportSession, session) && session.World.PreparedActionReport is { Completed: false }) return false;
        CloseActionReport();
        _actionReportSource = composition.PrepareArmyQueries(session.World).CreateActionReportSource(new(
            () => application.TutorialBlocksAreaReports, () => application.SpecialModeSuppressesSceneryReport,
            () => application.SpecialModeSuppressesAssociationReport, () => application.TutorialBlocksAssociationReports));
        var prepared = session.PrepareActionReport(_actionReportSource, _presentationRandom).Report;
        if (!prepared.Completed)
        {
            if (!ReferenceEquals(_actionReportSession, session))
                ReportPlayFailure(new InvalidDataException(prepared.Missing ?? "Action report preparation is incomplete."));
            _actionReportSession = session;
            return false;
        }
        _actionReportSession = session; _actionReportApplication = application;
        _actionReportMap = _residentMap;
        var content = prepared.Content;
        if (content.StrategistPortraitVisible is not bool portraitVisible)
            throw new InvalidDataException("Action report strategist portrait state is unknown.");
        _actionReportPeople = composition.PrepareArmyQueries(session.World).ReadActionReportPeople(session.World,
            content.Categories.SelectMany(category => category.Records).Select(record => record.PersonId).Distinct().ToArray(),
            content.StrategistPersonId, portraitVisible);
        var lists = content.Categories.ToDictionary(category => category.Category,
            category => new ActionReportListContent(category.Records, category.UsesEmptyFallback));
        RetirePeriodReportView();
        var view = new ActionReportView { Name = "CampaignActionReport" };
        _actionReportView = view;
        AddChild(view);
        try
        {
            EnsureReportBackground(session, application, _actionReportMap, view);
            var resources = TryBorrowApplicationUiResources(view);
            _actionReportUiResources = resources;
            var layers = _actionReportPeople.StrategistPortrait?.LoadSources(resources.Assets.Portraits.RequireSource);
            view.Configure(_repository, resources.Assets, resources.SpriteShader, resources.Font,
                _ui.SourceLocale, lists, null, false,
                CurrentListScrollSpeed(), CurrentListScrollSpeed, ResolveActionReportRow,
                (_, _, record, command) => RunUiAction(() => NavigateActionReport(record, command)),
                category => PrepareActionReportCategory(view, category), () => ActionReportInputAdmitted(view),
                () => RunPlayAction(() => CompleteActionReport(view)),
                error => { CloseActionReport(); ReportPlayFailure(error); RefreshPlayablePeriodReport(); },
                _sourceHeaders, prepared.SelectedCategory, preparedStandingLayers: layers);
            _actionReportMap?.SetDetailsOpen(true, keepMapVisible: true);
            SetMeta("play_report_overlay_visible", true);
            return true;
        }
        catch { CloseActionReport(); throw; }
    }

    private bool ActionReportInputAdmitted(ActionReportView view) =>
        !_failed && !_shuttingDown && !_advancingPeriod && !IsReturningToTitle && !TutorialOverlayOwnsInput &&
        _actionReportView == view && ReferenceEquals(_session, _actionReportSession) &&
        ReferenceEquals(_campaignApplication, _actionReportApplication) &&
        _world?.Phase == TurnPhase.Reporting && _detailsView is null && _reportMapReturn is null &&
        !HasMapSystemMenu && !HasCampaignSavePage && !HasAdministrationPage && !HasMilitaryOrderPage;

    private IReadOnlyList<string> PrepareActionReportCategory(ActionReportView view, byte category)
    {
        if (_actionReportView != view || !ReferenceEquals(_session, _actionReportSession))
            throw new InvalidOperationException("The action report campaign has been replaced.");
        var prepared = _actionReportSession!.SelectActionReportCategory(_actionReportSource!, category, _presentationRandom).Report;
        if (!prepared.Completed) throw new InvalidDataException(prepared.Missing ?? "Action report advice is incomplete.");
        return prepared.Content.Categories.Single(value => value.Category == category).StrategistPages;
    }

    private ActionReportDetailRowContent ResolveActionReportRow(CampaignHistoryRecord record)
    {
        var person = _actionReportPeople!.Rows[record.PersonId];
        if (!_actionReportFaces.TryGetValue(record.PersonId, out var face))
        {
            var route = person.Portrait;
            face = (_actionReportUiResources ?? throw new InvalidOperationException("Action report resource owner is absent."))
                .Assets.Portraits.RequireSource(route.ResourceName!, route.SpriteName!);
            _actionReportFaces[record.PersonId] = face;
        }
        return new(record, person.Name, face);
    }

    private void NavigateActionReport(CampaignHistoryRecord? record, int command)
    {
        if (record is null || _actionReportView is not { } view || !ActionReportInputAdmitted(view)) return;
        if (command == 1)
        {
            if (_actionReportPeople!.Rows[record.PersonId].OfficerId is int officer) OpenCommandOfficerDetails(officer);
            return;
        }
        if (command != 0 || _residentMap is not { } map || !map.FocusReportHex(record.HexId)) return;
        var navigation = new ReportMapReturnView();
        _reportMapReturn = navigation;
        _reportMapPriorFocus = _mapFocused;
        AddChild(navigation);
        try
        {
            SetReportBackgroundMapSuspended(true);
            view.Hide(); view.ProcessMode = ProcessModeEnum.Disabled;
            map.SetDetailsOpen(false); SetMapFocus(true);
            navigation.Configure(_repository!, _ui.SourceLocale, _sourceHeaders,
                () => RunUiAction(ReturnFromReportMap), TryBorrowApplicationUiResources(navigation));
        }
        catch { ReturnFromReportMap(); throw; }
    }

    private void ReturnFromReportMap()
    {
        var navigation = _reportMapReturn;
        _reportMapReturn = null;
        if (navigation is not null)
        {
            navigation.Retire();
            if (navigation.GetParent() is Node parent) parent.RemoveChild(navigation);
            navigation.QueueFree();
        }
        if (navigation is not null && _actionReportView is { } view && GodotObject.IsInstanceValid(view) &&
            ReferenceEquals(_session, _actionReportSession) && ReferenceEquals(_campaignApplication, _actionReportApplication) &&
            _residentMap == _actionReportMap)
        {
            _actionReportMap?.SetDetailsOpen(true, keepMapVisible: true);
            SetMapFocus(_reportMapPriorFocus);
            SetReportBackgroundMapSuspended(false);
            view.ProcessMode = ProcessModeEnum.Inherit; view.Show();
        }
        else if (navigation is not null) CloseReportBackground();
    }

    private void CompleteActionReport(ActionReportView view)
    {
        if (_actionReportView != view || !ReferenceEquals(_session, _actionReportSession)) return;
        CloseActionReport(keepReportBackground: true);
        try
        {
            ObserveTutorialActionReportAcknowledged();
            if (OpenIncomeReport(FinishPlayableReport)) return;
            FinishPlayableReport();
        }
        finally
        {
            if (!HasIncomeReport) CloseReportBackground();
        }
    }

    private void CloseActionReport(bool keepReportBackground = false)
    {
        var view = _actionReportView;
        _actionReportView = null;
        if (_commandDetailsReportParent is not null) CloseCommandDetails(resumeParent: false);
        ReturnFromReportMap();
        if (!_failed && !_shuttingDown && ReferenceEquals(_session, _actionReportSession) &&
            ReferenceEquals(_campaignApplication, _actionReportApplication) &&
            _actionReportMap is { } map && _residentMap == map && GodotObject.IsInstanceValid(map) && map.IsInsideTree())
            map.SetDetailsOpen(false);
        _actionReportSession = null; _actionReportApplication = null;
        _actionReportMap = null;
        _actionReportSource = null; _actionReportPeople = null; _actionReportFaces.Clear(); _actionReportUiResources = null;
        if (view is not null && GodotObject.IsInstanceValid(view))
        {
            view.Abandon();
            if (view.GetParent() is Node parent) parent.RemoveChild(view);
            if (!view.IsQueuedForDeletion()) view.QueueFree();
        }
        if (!keepReportBackground && !HasIncomeReport) CloseReportBackground();
    }
}
