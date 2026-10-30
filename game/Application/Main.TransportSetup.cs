using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private TransportSetupView? _transportSetup;
    private TransportSetupPresenter? _transportSetupPresenter;
    private DeploymentTargetView? _transportTarget;
    private TransportTargetPresenter? _transportTargetPresenter;
    private DeploymentNarrationView? _transportNarration;
    private MapSceneView? _transportSetupMap;
    private GameplaySession? _transportSetupSession;
    private GameState? _transportSetupWorld;
    private Action<GameState>? _transportSetupAdoptSpeechWorld;

    private void AdoptTransportSetupSpeechWorld(GameplaySession session, GameState previous, GameState replacement)
    {
        if (_transportSetup is null || _transportSetupPresenter is null || _transportSetupSession != session ||
            _transportSetupMap is null || _transportSetupMap != _residentMap ||
            !ReferenceEquals(_transportSetupWorld, previous)) return;
        _transportSetupAdoptSpeechWorld?.Invoke(replacement);
    }

    private void OpenTransportSetup(int source, bool fromCommandWorkspace = false)
    {
        if (_failed || _shuttingDown || _advancingPeriod || IsReturningToTitle || HasMilitaryOrderPage ||
            HasAdministrationPage || HasCampaignSavePage || HasMapSystemMenu || HasActionReport || HasPersonnelConfirmation ||
            HasCommandWorkspace && !fromCommandWorkspace || _detailsView is not null ||
            _repository is null || _campaignComposition is not { } composition ||
            _residentMap is not { IsPlayableMapReady: true } map ||
            _session is not { PlayerForceId: int force } session || session.World is not { Phase: TurnPhase.Strategy } world ||
            world.Cities.GetValueOrDefault(source) is not { } city || city.ForceId != force ||
            PlayableOutcome.Evaluate(world, force) != PlayableResult.Ongoing) return;
        if (fromCommandWorkspace) CloseCommandWorkspace();
        var rules = composition.PrepareArmyQueries(world);
        var view = new TransportSetupView(); _transportSetup = view;
        _transportSetupMap = map; _transportSetupSession = session; _transportSetupWorld = world;
        _transportSetupAdoptSpeechWorld = replacement =>
        {
            if (_transportSetup != view || _transportSetupPresenter is not { } presenter ||
                _transportSetupSession != session || _transportSetupMap != map) return;
            var previous = world;
            // The existing preference adoption preserves Make quantities, its
            // selector and cached food quote while advancing their query owner.
            presenter.AcceptPreferenceWorld(previous, replacement);
            _transportTargetPresenter?.AcceptPresentationWorld(previous, replacement);
            world = replacement; _transportSetupWorld = replacement;
        };
        SetMapFocus(true); map.CloseCityCommandMenu(); map.SetDetailsOpen(true, keepMapVisible: true); HideDeploymentHud();
        AddChild(view);
        try
        {
            _transportSetupPresenter = new(view, _repository, _ui, world, composition.Snapshot, rules, source,
                _sourceHeaders, Current, CurrentListScrollSpeed, BeginTarget, PrepareFoodWarning, Returned, TransportUnavailable);
        }
        catch { CloseTransportSetup(); throw; }

        bool Current() => !_failed && !_shuttingDown && _transportSetup == view &&
            ReferenceEquals(_session, session) && ReferenceEquals(_world, world) && _residentMap == map;
        TransportFoodWarning PrepareFoodWarning(int cargo)
        {
            if (!Current() || _advancingPeriod || session.HasPendingOperation)
                return new(null, null, "transport.setup.food-warning-owner");
            var previous = world;
            var result = session.Issue(new PrepareTransportFoodWarningOrder(force, source, cargo), new(_presentationRandom.State));
            if (!result.Accepted)
                return new(null, null, result.MissingRuleKey ?? result.ErrorCode ?? "transport.setup.food-warning");
            var warning = result.TransportFoodWarning ?? throw new InvalidDataException("transport.setup.food-warning-result");
            //The normal command transaction owns quote housekeeping. Adopt
            //it before map refresh or the page's next lifetime admission.
            world = session.World; _transportSetupWorld = world;
            _transportSetupPresenter!.AcceptFoodWarningWorld(previous, world);
            if (world.CampaignAppRandom32 is { } random) _presentationRandom.InstallCommittedState(random.State);
            SynchronizeApplicationCrtRandom(world);
            RefreshCurrentPlayWorld(); HideDeploymentHud();
            return new(warning.Required, warning.Expense, null);
        }
        void Returned()
        {
            if (!Current()) { CloseTransportSetup(); return; }
            CloseTransportSetup();
            if (world.Cities[source].HexId is int hex) SelectMapTarget(hex);
            map.ReopenCityCommandMenu(source);
        }
        void BeginTarget(TransportSetupSelection selection)
        {
            if (!Current() || _transportTarget is not null || _transportSetupPresenter?.Draft.Quantities is not { } quantities) return;
            view.Hide(); view.SetBusy(true);
            var target = new DeploymentTargetView(); _transportTarget = target; AddChild(target);
            try
            {
                _transportTargetPresenter = new(target, _repository!, _ui, world, rules, source, force, selection,
                    _transportSetupPresenter.Draft.Setup, quantities, _sourceHeaders, TargetCurrent,
                    map.ProjectDeploymentTarget, map.ProjectWaypointMarker, CurrentListScrollSpeed,
                    PrepareNarration, ResumeMake, TransportUnavailable, SetModal,
                    (returnOfficer, behavior, accepted) =>
                    {
                        if (!TargetCurrent()) return false;
                        var previous = world;
                        bool saved = ApplyPlayableOrder(new SaveTransportDefaultsOrder(force, returnOfficer, behavior), () =>
                        {
                            world = session.World; _transportSetupWorld = world;
                            _transportSetupPresenter!.AcceptPreferenceWorld(previous, world); accepted(world);
                        });
                        if (saved) HideDeploymentHud(); else TransportUnavailable(_playMessage.Text);
                        return saved;
                    },routePreviewChanged:map.SetDeploymentRoutePreviews);
                map.SetDetailsOpen(false, keepMapVisible: true); HideDeploymentHud();
                map.SetCommandTargetPicker(hex => _transportTargetPresenter?.PickTarget(hex, map.SourceSelectionShiftHeld),
                    hex => _transportTargetPresenter?.AllowPointer(hex) == true,
                    hex => _transportTargetPresenter?.HoverTarget(hex), supportsShiftWaypoint: true);
            }
            catch { ResumeMake(); throw; }
            bool TargetCurrent() => Current() && _transportTarget == target;
        }
        void SetModal(bool open)
        {
            if (_residentMap == map && _transportSetupMap == map)
            { map.SetDetailsOpen(open, keepMapVisible: true); HideDeploymentHud(); }
        }
        void ResumeMake()
        {
            CloseTransportTarget();
            if (!Current()) { CloseTransportSetup(); return; }
            map.SetCommandTargetPicker(null); SetModal(true); _transportSetupPresenter?.Resume();
        }
        void PrepareNarration(DispatchTransportArmyOrder order)
        {
            if (!Current() || _transportTarget is null || _transportNarration is not null) return;
            var preparation = rules.QueryTransportNarration(world, order, _ui.SourceLocale, _presentationRandom.Next);
            if (!preparation.Available || preparation.PortraitRoute is not { } portrait)
            { TransportUnavailable(preparation.UnresolvedInput ?? "transport.narration.portrait"); return; }
            var sprites = new LocalizedUiSpriteSource(_repository!);
            var layers = portrait.LoadSprites(sprites.LoadTexturePackSprite);
            var narration = new DeploymentNarrationView { KeyboardInput = HandleGameplayNarrationKeyboard }; _transportNarration = narration; AddChild(narration);
            try
            {
                narration.Configure(_repository!, _ui, preparation.SpeakerName!, preparation.Text!, layers, () => RunPlayAction(() =>
                {
                    if (_transportNarration != narration) return;
                    if (!Current()) { CloseTransportSetup(); return; }
                    if (!ApplyPlayableOrder(order, CloseTransportSetup))
                    {
                        _transportNarration = null; narration.Abandon(); narration.GetParent()?.RemoveChild(narration); narration.QueueFree();
                        ResumeMake(); TransportUnavailable(_playMessage.Text);
                    }
                }), () => _preferences.AutomaticMessageAdvance,
                () => CanAdvanceNarration&&_transportNarration==narration&&Current());
                narration.BindPreparedVoices(preparation.Voices, _sceneAudioBinding);
                CloseTransportTarget(); map.SetCommandTargetPicker(null); SetModal(true);
            }
            catch { _transportNarration = null; narration.Abandon(); narration.QueueFree(); throw; }
        }
    }
    private void TransportUnavailable(string reason)
    {
        string message = _ui.Get("play_transport_setup_unavailable"); _playMessage.Text = message;
        _transportSetup?.ApplyHint(message); _transportTarget?.ApplyHint(message); GD.PushWarning(reason);
    }
    private void CancelTransportTarget()
    {
        if (_transportTargetPresenter?.TryCancelOverlay() == true) return;
        _transportTarget?.RequestBack();
    }
    private void CloseTransportTarget()
    {
        var presenter = _transportTargetPresenter; var view = _transportTarget;
        _transportTargetPresenter = null; _transportTarget = null; presenter?.Dispose();
        if(_transportSetupMap is {} map&&GodotObject.IsInstanceValid(map)&&_residentMap==map)map.SetDeploymentRoutePreviews([]);
        if (view is not null) { view.Abandon(); view.GetParent()?.RemoveChild(view); view.QueueFree(); }
    }
    private void CloseTransportSetup()
    {
        var map = _transportSetupMap;
        CloseTransportTarget();
        var narrator = _transportNarration; _transportNarration = null;
        if (narrator is not null) { narrator.Abandon(); narrator.GetParent()?.RemoveChild(narrator); narrator.QueueFree(); }
        var presenter = _transportSetupPresenter; var view = _transportSetup;
        _transportSetupPresenter = null; _transportSetup = null; presenter?.Dispose();
        _transportSetupMap = null; _transportSetupSession = null; _transportSetupWorld = null; _transportSetupAdoptSpeechWorld = null;
        if (view is not null) { view.Abandon(); view.GetParent()?.RemoveChild(view); view.QueueFree(); }
        if (map is not null && GodotObject.IsInstanceValid(map) && _residentMap == map)
        { map.SetCommandTargetPicker(null); map.SetDetailsOpen(false); if (!_shuttingDown && !_failed) SetMapFocus(true); }
    }
}
