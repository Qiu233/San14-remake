using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private SceneryHexCommandMenu? _sceneryHexMenu;
    private int? _sceneryHexMenuId;
    private SceneryHexCommandPage? _sceneryHexCommands;
    private Func<int, int, Action, bool>? SceneryUnitTargetOpener { get; set; }
    private SceneryInformationView? _sceneryInformationView;
    private Func<bool>? SceneryInvestmentDecisionAdmission { get; set; }
    private bool SceneryInvestmentReturnAllowed { get; set; } = true;
    private Func<int, bool>? SceneryHexActionAdmission { get; set; }
    private bool HasSceneryInvestmentPage => _sceneryHexMenu is not null || _sceneryInvestmentView is not null || _sceneryInformationView is not null;
    private bool SceneryHexMenuPresent => _sceneryHexMenu is not null && IsSceneryInvestmentWorkspaceCurrent;
    private bool SceneryHexMenuVisible => SceneryHexMenuPresent && _sceneryHexMenu!.IsOpen;
    private bool IsSceneryInvestmentWorkspaceCurrent => !_failed && !_shuttingDown &&
        ReferenceEquals(_session, _sceneryInvestmentSession) && ReferenceEquals(_world, _sceneryInvestmentWorld) &&
        _sceneryInvestmentMap is not null && _residentMap == _sceneryInvestmentMap;
    private SceneryInvestmentView? _sceneryInvestmentView;
    private SceneryInvestmentDraft? _sceneryInvestmentDraft;
    private GameplaySession? _sceneryInvestmentSession;
    private GameState? _sceneryInvestmentWorld;
    private MapSceneView? _sceneryInvestmentMap;
    private bool IsSceneryInvestmentCurrent => !_failed && !_shuttingDown &&
        _sceneryInvestmentView is not null && _sceneryInvestmentDraft is not null &&
        ReferenceEquals(_session, _sceneryInvestmentSession) && ReferenceEquals(_world, _sceneryInvestmentWorld) &&
        _sceneryInvestmentMap is not null && _residentMap == _sceneryInvestmentMap;
    private void AdoptSceneryInvestmentSpeechWorld(GameplaySession session, GameState previous, GameState replacement)
    {
        // Adopt the hidden Hex parent as well as its editor/information child.
        // Keep the business projection and the selector's unaccepted selection.
        if (!HasSceneryInvestmentPage || _sceneryInvestmentSession != session ||
            _sceneryInvestmentMap is null || _sceneryInvestmentMap != _residentMap ||
            !ReferenceEquals(_sceneryInvestmentWorld, previous)) return;
        _sceneryInvestmentWorld = replacement;
    }

    private bool CanEditSceneryInvestment => IsSceneryInvestmentCurrent && !_sceneryInvestmentView!.SelectorVisible;
    private long SceneryInvestmentAcceptedSequence { get; set; }
    private int? SceneryInvestmentLastAcceptedSceneryId { get; set; }
    private bool SceneryInvestmentMainVisible => IsSceneryInvestmentCurrent && _sceneryInvestmentView!.MainVisible;
    private bool SceneryInvestmentButtonsVisible => IsSceneryInvestmentCurrent && _sceneryInvestmentView!.ButtonsVisible;
    private bool SceneryInvestmentSelectorControlsReady => IsSceneryInvestmentWorkspaceCurrent &&
        (_sceneryInformationView?.ControlsReady == true || _sceneryInvestmentView?.SelectorControlsReady == true);
    private bool SceneryInvestmentSelectorVisible => IsSceneryInvestmentWorkspaceCurrent &&
        (_sceneryInformationView?.MainVisible == true || _sceneryInvestmentView?.SelectorVisible == true);
    private int? SceneryInvestmentSelectedSceneryId => _sceneryInformationView?.SelectedSceneryId ??
        (IsSceneryInvestmentCurrent ? _sceneryInvestmentView!.SelectedSceneryId ?? _sceneryInvestmentDraft!.SceneryId : null);

    private void OpenSceneryInvestmentOrders(int? cityId = null, int? sceneryId = null)
    {
        if (_failed || _shuttingDown || _advancingPeriod || IsReturningToTitle || HasAdministrationPage && !SceneryHexMenuPresent ||
            HasMilitaryOrderPage || HasCampaignSavePage || HasMapSystemMenu || HasCommandWorkspace || HasActionReport ||
            HasPersonnelConfirmation || _detailsView is not null || _repository is null ||
            _campaignComposition is not { } composition || _session is not { PlayerForceId: int player } session ||
            session.HasPendingOperation || session.World is not { Phase: TurnPhase.Strategy } world ||
            _residentMap is not { IsPlayableMapReady: true } map) return;
        var district = cityId is int requestedCity && world.Cities.GetValueOrDefault(requestedCity)?.DistrictId is int districtId
            ? world.Districts.GetValueOrDefault(districtId)
            : world.Districts.Values.SingleOrDefault(value => value.ForceId == player && value.Number == 1);
        if (district is null) { _playMessage.Text = _ui.Get("detail_unknown"); return; }
        SceneryInvestmentDraft draft;
        try { draft = composition.PrepareArmyQueries(world).OpenSceneryInvestmentDraft(world, player, district.Id, cityId, sceneryId); }
        catch (InvalidDataException error) { _playMessage.Text = _ui.Get("detail_unknown"); GD.PushWarning(error.ToString()); return; }
        _sceneryInvestmentDraft = draft; _sceneryInvestmentSession = session;
        _sceneryInvestmentWorld = world; _sceneryInvestmentMap = map;
        var view = new SceneryInvestmentView(); _sceneryInvestmentView = view; AddChild(view);
        try
        {
            SetMapFocus(true); map.CloseCityCommandMenu(); map.SetDetailsOpen(true, keepMapVisible: true); HideDeploymentHud();
            view.Configure(_repository, _ui, draft.Page, () => IsSceneryInvestmentCurrent,
                () => RunUiAction(OpenSceneryInvestmentSelection),
                keepOpen => RunPlayAction(() => ConfirmSceneryInvestment(keepOpen)),
                () => RunUiAction(() => CloseSceneryInvestmentChild(showParent: true)));
            BindCurrentTutorialSceneryInvestment();
            RefreshSceneryInvestment();
        }
        catch { CloseSceneryInvestment(); throw; }
    }
    private void RefreshSceneryInvestment()
    {
        if (!IsSceneryInvestmentCurrent) return;
        _sceneryInvestmentView!.ApplyInteractionAdmission(SceneryInvestmentDecisionAdmission, SceneryInvestmentReturnAllowed);
        _sceneryInvestmentView.Apply(_sceneryInvestmentDraft!.Page);
        if (_sceneryInvestmentDraft.UnresolvedOwner is { } missing) GD.PushWarning(missing);
    }
    private void OpenSceneryInvestmentSelection()
    {
        if (!CanEditSceneryInvestment) return;
        var draft = _sceneryInvestmentDraft!;
        _sceneryInvestmentView!.ShowScenerySelector(draft.SceneryCandidates, draft.SceneryId, selected => RunUiAction(() =>
        {
            if (!IsSceneryInvestmentCurrent || _sceneryInvestmentDraft != draft) return;
            draft.SelectScenery(selected); RefreshSceneryInvestment();
        }));
    }
    private void ConfirmSceneryInvestment(bool keepOpen)
    {
        if (!CanEditSceneryInvestment || _sceneryInvestmentDraft!.Page.CanConfirm != true ||
            !(SceneryInvestmentDecisionAdmission?.Invoke() ?? true)) return;
        var draft = _sceneryInvestmentDraft; var view = _sceneryInvestmentView!;
        var session = _sceneryInvestmentSession; int city = draft.CityId;
        view.SetBusy(true);
        if (!ApplyPlayableOrder(draft.CreateOrder(), () =>
        {
            SceneryInvestmentLastAcceptedSceneryId = draft.SceneryId;
            SceneryInvestmentAcceptedSequence++;
            CloseSceneryInvestmentChild(showParent: !keepOpen, acceptedWorld: session!.World);
        })) { view.SetBusy(false); RefreshSceneryInvestment(); }
        else if (keepOpen && ReferenceEquals(_session, session)) OpenSceneryInvestmentOrders(city);
    }
    private void RequestSceneryInvestmentBack()
    {
        if (HasGeneralUnitTargetPage) { RequestGeneralUnitTargetBack(); return; }
        if (_sceneryInformationView is { } information) information.RequestBack();
        else if (IsSceneryInvestmentCurrent) _sceneryInvestmentView!.RequestBack();
        else if (SceneryHexMenuVisible) _sceneryHexMenu!.RequestBack();
        else CloseSceneryInvestment();
    }
    private void CloseSceneryInvestment()
    {
        var information = _sceneryInformationView; _sceneryInformationView = null;
        information?.Abandon(); information?.QueueFree();
        var map = _sceneryInvestmentMap; _sceneryInvestmentMap = null;
        _sceneryInvestmentWorld = null; _sceneryInvestmentSession = null; _sceneryInvestmentDraft = null;
        var view = _sceneryInvestmentView; _sceneryInvestmentView = null;
        view?.Abandon(); view?.QueueFree();
        var menu = _sceneryHexMenu; _sceneryHexMenu = null; _sceneryHexMenuId = null; _sceneryHexCommands = null;
        menu?.Abandon(); menu?.QueueFree();
        if (map is not null && GodotObject.IsInstanceValid(map) && _residentMap == map) map.SetDetailsOpen(false);
        if (!_failed && !_shuttingDown && _residentMap is not null) SetMapFocus(_mapFocused);
    }
    private void OpenSceneryHexMenu(int sceneryId, Vector2 canonicalMouse)
    {
        if (_failed || _shuttingDown || _advancingPeriod || IsReturningToTitle || HasAdministrationPage ||
            HasMilitaryOrderPage || HasCampaignSavePage || HasMapSystemMenu || HasCommandWorkspace || HasActionReport ||
            HasPersonnelConfirmation || _detailsView is not null || _repository is null ||
            _campaignComposition is not { } composition || _session is not { PlayerForceId: int player } session ||
            session.HasPendingOperation || session.World is not { Phase: TurnPhase.Strategy } world ||
            _residentMap is not { IsPlayableMapReady: true } map) return;
        var page = composition.PrepareArmyQueries(world).QuerySceneryHexCommands(world, player, sceneryId);
        if (page.HexId is not int hex) { _playMessage.Text = _ui.Get("detail_unknown"); return; }
        _sceneryInvestmentSession = session; _sceneryInvestmentWorld = world; _sceneryInvestmentMap = map;
        _sceneryHexMenuId = sceneryId; _sceneryHexCommands = page;
        var menu = new SceneryHexCommandMenu(); _sceneryHexMenu = menu; AddChild(menu);
        try
        {
            menu.Configure(_repository, _ui, canonicalMouse, BuildSceneryHexMenuItems(page),
                () => SceneryHexMenuPresent && _sceneryHexMenu == menu && _sceneryInvestmentView is null && _sceneryInformationView is null,
                payload => RunUiAction(() => AcceptSceneryHexCommand(payload)),
                () => RunUiAction(CloseSceneryInvestment));
            BindCurrentTutorialSceneryInvestment();
            SetMapFocus(true); map.CloseCityCommandMenu(); map.SetDetailsOpen(true, keepMapVisible: true);
            HideDeploymentHud();
        }
        catch { CloseSceneryInvestment(); throw; }
    }
    private void AcceptSceneryHexCommand(int payload)
    {
        if (!(SceneryHexActionAdmission?.Invoke(payload) ?? true) || !SceneryHexMenuVisible || _sceneryHexMenuId is not int scenery || _sceneryHexCommands is not { HexId: int hex } page) return;
        if (payload == 0 && page.InvestmentAllowed == true)
        {
            _sceneryHexMenu!.Hide();
            OpenSceneryInvestmentOrders(page.CityId, scenery);
            if (_sceneryInvestmentView is null) _sceneryHexMenu?.Show();
            else ObserveTutorialSceneryAction(payload);
        }
        else if (payload == 1 && page.TargetAllowed == true && SceneryUnitTargetOpener is { } target)
        {
            _sceneryHexMenu!.Hide();
            if (!target(scenery, hex, () => CloseSceneryInvestmentChild(showParent: true))) _sceneryHexMenu?.Show();
            else ObserveTutorialSceneryAction(payload);
        }
        else if (payload == 2 && page.DetailAllowed == true)
        {
            _sceneryHexMenu!.Hide();
            if (!OpenSceneryInformation(scenery)) _sceneryHexMenu?.Show();
            else ObserveTutorialSceneryAction(payload);
        }
    }

    private IReadOnlyList<SceneryHexMenuItem> BuildSceneryHexMenuItems(SceneryHexCommandPage page)
    {
        var messages = _repository!.LoadMessages(_ui.SourceLocale, San14.Data.Formats.MessageKind.Data).Messages.Sections[1].Records;
        string Caption(int id) => messages[id].Text ?? throw new InvalidDataException("Missing scenery Hex command caption.");
        return [new(0, Caption(0xB1), ApplySceneryActionAdmission(0, page.InvestmentAllowed), page.UnresolvedOwner),
            new(1, Caption(0x29A), SceneryUnitTargetOpener is null ? null : ApplySceneryActionAdmission(1, page.TargetAllowed),
                SceneryUnitTargetOpener is null ? "scenery.unit_target_host" : page.UnresolvedOwner),
            new(2, Caption(0xB2), ApplySceneryActionAdmission(2, page.DetailAllowed), page.UnresolvedOwner)];
    }
    private void CloseSceneryInvestmentChild(bool showParent, GameState? acceptedWorld = null)
    {
        var information = _sceneryInformationView; _sceneryInformationView = null;
        information?.Abandon(); information?.QueueFree();
        var view = _sceneryInvestmentView; _sceneryInvestmentView = null; _sceneryInvestmentDraft = null;
        view?.Abandon(); view?.QueueFree();
        if (_sceneryHexMenu is null) { CloseSceneryInvestment(); return; }
        if (acceptedWorld is not null) _sceneryInvestmentWorld = acceptedWorld;
        if (!showParent) return;
        if (_sceneryInvestmentSession is not { PlayerForceId: int player } session ||
            _sceneryHexMenuId is not int scenery || _campaignComposition is not { } composition)
        { CloseSceneryInvestment(); return; }
        var world = acceptedWorld ?? session.World;
        _sceneryInvestmentWorld = world;
        var page = composition.PrepareArmyQueries(world).QuerySceneryHexCommands(world, player, scenery);
        _sceneryHexCommands = page;
        _sceneryHexMenu.Apply(BuildSceneryHexMenuItems(page)); _sceneryHexMenu.Show();
    }

    private bool? ApplySceneryActionAdmission(int payload, bool? allowed) => allowed == true
        ? SceneryHexActionAdmission?.Invoke(payload) ?? true : allowed;
    private void RefreshSceneryInvestmentAdmission()
    {
        _sceneryInvestmentView?.ApplyInteractionAdmission(SceneryInvestmentDecisionAdmission, SceneryInvestmentReturnAllowed);
        if (_sceneryHexMenu is not null && _sceneryHexCommands is { } page)
            _sceneryHexMenu.Apply(BuildSceneryHexMenuItems(page));
    }
    private void CloseSceneryHexParentAfterInvestment()
    {
        if (_sceneryHexMenu is not null && _sceneryInvestmentView is null && _sceneryInformationView is null)
            CloseSceneryInvestment();
    }
    private bool OpenSceneryInformation(int sceneryId)
    {
        if (!IsSceneryInvestmentWorkspaceCurrent || _repository is null || _campaignComposition is not { } composition ||
            _sceneryInvestmentWorld is not { } world) return false;
        var page = composition.PrepareArmyQueries(world).QuerySceneryInformation(world, sceneryId);
        var information = new SceneryInformationView(); _sceneryInformationView = information; AddChild(information);
        try
        {
            information.Configure(_repository, _ui, page,
                () => IsSceneryInvestmentWorkspaceCurrent && _sceneryInformationView == information,
                () => RunUiAction(() => CloseSceneryInvestmentChild(showParent: true)));
            BindCurrentTutorialSceneryInvestment();
            return true;
        }
        catch { _sceneryInformationView = null; information.Abandon(); information.QueueFree(); throw; }
    }

}
