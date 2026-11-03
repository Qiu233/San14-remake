using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private CityDomesticView? _cityDomesticView;
    private CityDomesticPresenter? _cityDomesticPresenter;
    private CityDomesticPersonSelectionHost? _cityDomesticSelectors;
    private DeploymentNarrationView? _cityDomesticNarration;
    private MapSceneView? _cityDomesticMap;
    private GameState? _cityDomesticWorld;
    private GameplaySession? _cityDomesticSession;
    private bool HasCityDomesticPage => _cityDomesticView is not null || _cityDomesticNarration is not null;

    private bool IsCityDomesticCurrent(GameplaySession session, MapSceneView map) =>
        !_failed && !_shuttingDown && ReferenceEquals(_session, session) && _cityDomesticWorld is { } world &&
        ReferenceEquals(session.World, world) && ReferenceEquals(_world, world) && _residentMap == map &&
        ReferenceEquals(_cityDomesticSession, session) && _cityDomesticMap == map;

    private void AdoptCityDomesticSpeechWorld(GameplaySession session, GameState previous, GameState replacement)
    {
        // The common speech dispatcher proves the committed presentation-only
        // successor. Keep the editable projection and its pending selections.
        if (_cityDomesticView is null || _cityDomesticPresenter is not { } presenter ||
            _cityDomesticSession != session || _cityDomesticMap is null || _cityDomesticMap != _residentMap ||
            !ReferenceEquals(_cityDomesticWorld, previous) ||
            !presenter.Draft.TryAdoptPresentationWorld(previous, replacement)) return;
        _cityDomesticWorld = replacement;
    }

    private AreaAdministrationQuery<CityDomesticMenuAdmission> QueryCityDomesticMenu(
        MapSceneView map, GameState world, int city, int? settledCameraLevel)
    {
        if (_failed || _shuttingDown || _residentMap != map || _session is not { } session ||
            !ReferenceEquals(_world, world) || !ReferenceEquals(session.World, world) || _campaignComposition is not { } composition)
            return new(null, "city-domestic.menu.ui_world_binding");
        return composition.PrepareArmyQueries(world).QueryCityDomesticMenuAdmission(world, city, settledCameraLevel);
    }

    private AreaAdministrationQuery<CityCommandMenuProjection> QueryCityCommandMenu(
        MapSceneView map, GameState world, int city, int? settledCameraLevel)
    {
        if (_failed || _shuttingDown || _residentMap != map || _session is not { } session ||
            !ReferenceEquals(_world, world) || !ReferenceEquals(session.World, world) || _campaignComposition is not { } composition)
            return new(null, "city-command-menu.ui_world_binding");
        return composition.PrepareArmyQueries(world).QueryCityCommandMenu(world, city, settledCameraLevel);
    }

    private void OpenCityDomestic(int city)
    {
        if (HasAdministrationPage || HasMilitaryOrderPage || HasCampaignSavePage || HasMapSystemMenu || HasCommandWorkspace || _detailsView is not null || _repository is null ||
            _session is not { } session || session.HasPendingOperation ||
            _world is not { } world || !ReferenceEquals(session.World, world) || _campaignComposition is not { } composition ||
            _residentMap is not { IsPlayableMapReady: true } map || _playerForceId is not int force) return;
        var admission = map.QueryCityDomesticMenu(city);
        if (!admission.Available) { CityDomesticUnavailable(admission.UnresolvedInput!); return; }
        if (admission.Value is not { Present: true, Enabled: true }) return;
        var opened = composition.PrepareArmyQueries(world).OpenCityDomesticDraft(world, city);
        if (!opened.Available) { CityDomesticUnavailable(opened.UnresolvedInput!); return; }
        var draft = opened.Value!;
        _cityDomesticWorld = world; _cityDomesticSession = session; _cityDomesticMap = map;
        var view = new CityDomesticView(); _cityDomesticView = view; AddChild(view);
        try
        {
            SetMapFocus(true); map.CloseCityCommandMenu(); map.SetDetailsOpen(true, keepMapVisible: true); HideDeploymentHud();
            _cityDomesticSelectors = new(this, _repository, _ui, world, draft,
                () => IsCityDomesticCurrent(session, map) && _cityDomesticView == view, CityDomesticUnavailable,
                CurrentListScrollSpeed(),CurrentListScrollSpeed);
            _cityDomesticPresenter = new(view, _repository, _ui, draft,
                () => IsCityDomesticCurrent(session, map) && _cityDomesticView == view, RunUiAction,
                terminal => RunPlayAction(() => CompleteCityDomestic(session, map, view, city, force, terminal)),
                CityDomesticUnavailable, _ => true, _cityDomesticSelectors.Open, resources: TryBorrowApplicationUiResources(view));
        }
        catch { draft.Dispose(); CloseCityDomestic(); throw; }
    }

    private void CompleteCityDomestic(GameplaySession session, MapSceneView map,
        CityDomesticView view, int city, int force, CityDomesticTerminal terminal)
    {
        if (!IsCityDomesticCurrent(session, map) || _cityDomesticView != view ||
            _cityDomesticPresenter is not { } presenter || _cityDomesticNarration is not null) return;
        var draft = presenter.Draft;
        var exported = draft.ExportCompletionIntent(terminal);
        if (!exported.Available) { CityDomesticUnavailable(exported.UnresolvedInput!); return; }
        var intent = exported.Value!;
        if (terminal == CityDomesticTerminal.Cancel) { Submit(intent); return; }
        var preparation = draft.PrepareHandover(_ui.SourceLocale, _presentationRandom.Next);
        if (!preparation.Available) { CityDomesticUnavailable(preparation.UnresolvedInput!); return; }
        var handover = preparation.Value!;
        if (!Current(intent) || handover.DraftRevision != intent.DraftRevision) return;
        if (handover.Kind == CityDomesticHandoverKind.SourceCancel)
        {
            var cancelled = draft.ExportCompletionIntent(CityDomesticTerminal.Cancel);
            if (!cancelled.Available) { CityDomesticUnavailable(cancelled.UnresolvedInput!); return; }
            if (cancelled.Value!.DraftRevision != intent.DraftRevision) return;
            Submit(cancelled.Value!); return;
        }
        var speech = handover.Narration ?? throw new InvalidDataException("City handover narration is unavailable.");
        var route = speech.PortraitRoute;
        var narration = new DeploymentNarrationView { KeyboardInput = HandleGameplayNarrationKeyboard }; _cityDomesticNarration = narration; AddChild(narration);
        try
        {
            var resources=TryBorrowApplicationUiResources(narration)??throw new InvalidOperationException("Narration requires its prepared application resources.");
            var layers=route.LoadSources(resources.Assets.Portraits.RequireSource);
            presenter.SetBusy(true);
            narration.Configure(_repository!, _ui, speech.SpeakerName, speech.Text, layers,resources, () => RunPlayAction(() =>
            {
                if (_cityDomesticNarration != narration) return;
                if (!Current(intent)) { CloseCityDomestic(); return; }
                _cityDomesticNarration = null; narration.Abandon(); narration.QueueFree();
                Submit(intent);
            }), () => _preferences.AutomaticMessageAdvance,
                () => CanAdvanceNarration&&_cityDomesticNarration==narration&&Current(intent));
        }
        catch
        {
            _cityDomesticNarration = null; narration.Abandon(); narration.QueueFree(); presenter.SetBusy(false); throw;
        }

        bool Current(CityDomesticCompletionIntent captured) =>
            IsCityDomesticCurrent(session, map) && _cityDomesticView == view && _cityDomesticPresenter == presenter &&
            draft.IsForWorld(session.World) && draft.Revision == captured.DraftRevision && captured.CityId == city;

        void Submit(CityDomesticCompletionIntent captured)
        {
            if (!Current(captured)) return;
            presenter.SetBusy(true);
            try
            {
                if (ApplyPlayableOrder(new CompleteCityDomesticOrder(force, captured.CityId, captured.Operations,
                    captured.Terminal, captured.Policy), CloseCityDomestic))
                {
                    if (captured.Terminal == CityDomesticTerminal.Cancel) ReturnToCityMenu(session, map, city);
                }
                else { presenter.SetBusy(false); presenter.ApplyNotice(_playMessage.Text); }
            }
            catch
            {
                if (Current(captured))
                { presenter.SetBusy(false); presenter.ApplyNotice(_ui.Get("detail_unknown")); }
                throw;
            }
        }
    }

    private void ReturnToCityMenu(GameplaySession session, MapSceneView map, int city)
    {
        if (_failed || _shuttingDown || !ReferenceEquals(_session, session) || _residentMap != map ||
            !ReferenceEquals(_world, session.World) || !session.World.Cities.TryGetValue(city, out var owner)) return;
        SetMapFocus(true);
        if (owner.HexId is int hex) SelectMapTarget(hex);
        map.ReopenCityCommandMenu(city);
    }

    private void CityDomesticUnavailable(string reason)
    {
        string message = _ui.Get("detail_unknown"); _playMessage.Text = message;
        _cityDomesticPresenter?.ApplyNotice(message); GD.PushWarning(reason);
    }

    private void CloseCityDomestic()
    {
        if (!HasCityDomesticPage && _cityDomesticMap is null && _cityDomesticPresenter is null && _cityDomesticSelectors is null) return;
        var map = _cityDomesticMap; _cityDomesticMap = null; _cityDomesticWorld = null; _cityDomesticSession = null;
        var selectors = _cityDomesticSelectors; _cityDomesticSelectors = null;
        var presenter = _cityDomesticPresenter; _cityDomesticPresenter = null;
        var view = _cityDomesticView; _cityDomesticView = null;
        var narration = _cityDomesticNarration; _cityDomesticNarration = null;
        if (narration is not null) { narration.Abandon(); narration.QueueFree(); }
        selectors?.Dispose(); presenter?.Dispose();
        if (view is not null) { view.Abandon(); view.QueueFree(); }
        if (map is not null && GodotObject.IsInstanceValid(map) && _residentMap == map)
            map.SetDetailsOpen(false);
        if (!_failed && !_shuttingDown && _residentMap is not null) SetMapFocus(_mapFocused);
    }
}
