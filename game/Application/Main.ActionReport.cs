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
    private readonly Dictionary<ushort, LoadedSprite> _actionReportFaces = [];
    private ReportMapReturnView? _reportMapReturn;
    private bool HasActionReport => _actionReportView is not null;

    private bool RefreshActionReport()
    {
        if (_repository is null || _session is not { PlayerForceId: not null } session ||
            session.World.Phase != TurnPhase.Reporting || _campaignApplication is not { } application ||
            _campaignComposition is not { } composition) return false;
        if (_actionReportView is not null && ReferenceEquals(_actionReportSession, session) &&
            ReferenceEquals(_actionReportApplication, application)) return true;
        if (ReferenceEquals(_actionReportSession, session) && session.World.PreparedActionReport is { Completed: false }) return false;
        CloseActionReport();
        _actionReportSource = composition.PrepareArmyQueries(session.World).CreateActionReportSource(new(
            () => application.TutorialBlocksAreaReports, () => application.SpecialModeSuppressesSceneryReport,
            () => application.SpecialModeSuppressesAssociationReport));
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
        var sprites = new LocalizedUiSpriteSource(_repository);
        IReadOnlyList<LoadedSprite>? layers = null;
        if (_actionReportPeople.StrategistPortrait is { } route)
            layers = new[] { route.SpriteB, route.SpriteA, route.SpriteC }
                .Select(name => sprites.LoadTexturePackSprite(route.ResourceName, name)).ToArray();
        RetirePeriodReportView();
        var view = new ActionReportView { Name = "CampaignActionReport" };
        _actionReportView = view;
        AddChild(view);
        try
        {
            view.ConfigureOwned(_repository, _ui.SourceLocale, lists, layers, false,
                CurrentListScrollSpeed(), CurrentListScrollSpeed, ResolveActionReportRow,
                (_, _, record, command) => RunUiAction(() => NavigateActionReport(record, command)),
                category => PrepareActionReportCategory(view, category), () => ActionReportInputAdmitted(view),
                () => RunPlayAction(() => CompleteActionReport(view)),
                error => { CloseActionReport(); ReportPlayFailure(error); RefreshPlayablePeriodReport(); },
                _sourceHeaders, prepared.SelectedCategory);
            _actionReportMap?.SetDetailsOpen(true, keepMapVisible: true);
            SetMeta("play_report_overlay_visible", true);
            return true;
        }
        catch { CloseActionReport(); throw; }
    }

    private bool ActionReportInputAdmitted(ActionReportView view) =>
        !_failed && !_shuttingDown && !_advancingPeriod && !IsReturningToTitle &&
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
            face = new LocalizedUiSpriteSource(_repository!).LoadTexturePackSprite(route.ResourceName!, route.SpriteName!);
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
            view.Hide(); view.ProcessMode = ProcessModeEnum.Disabled;
            map.SetDetailsOpen(false); SetMapFocus(true);
            navigation.Configure(_repository!, _ui.SourceLocale, _sourceHeaders,
                () => RunUiAction(ReturnFromReportMap));
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
            view.ProcessMode = ProcessModeEnum.Inherit; view.Show();
        }
    }

    private void CompleteActionReport(ActionReportView view)
    {
        if (_actionReportView != view || !ReferenceEquals(_session, _actionReportSession)) return;
        CloseActionReport();
        FinishPlayableReport();
    }

    private void CloseActionReport()
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
        _actionReportSource = null; _actionReportPeople = null; _actionReportFaces.Clear();
        if (view is not null && GodotObject.IsInstanceValid(view))
        {
            view.Abandon();
            if (view.GetParent() is Node parent) parent.RemoveChild(view);
            if (!view.IsQueuedForDeletion()) view.QueueFree();
        }
    }
}
