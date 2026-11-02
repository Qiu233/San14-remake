using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private sealed class GeneralUnitTargetWorkspace(GameplaySession session, GameState world, MapSceneView map,
        GeneralUnitTargetPreparation preparation, DeploymentTargetView view, Action? returned)
    {
        internal GameplaySession Session { get; } = session;
        internal GameState World { get; set; } = world;
        internal MapSceneView Map { get; } = map;
        internal GeneralUnitTargetPreparation Preparation { get; } = preparation;
        internal DeploymentTargetView View { get; } = view;
        internal DeploymentTargetPresenter? Presenter { get; set; }
        internal DeploymentNarrationView? Narration { get; set; }
        internal Action? Returned { get; } = returned;
    }
    private GeneralUnitTargetWorkspace? _generalUnitTarget;
    private bool HasGeneralUnitTargetPage => _generalUnitTarget is not null;
    private bool IsGeneralUnitTargetCurrent => _generalUnitTarget is { } owner && !_failed && !_shuttingDown &&
        ReferenceEquals(_session, owner.Session) && ReferenceEquals(_world, owner.World) &&
        ReferenceEquals(owner.Session.World, owner.World) && _residentMap == owner.Map;

    private bool OpenGeneralUnitTarget(DeploymentTarget target, Action? returned = null)
    {
        if (_failed || _shuttingDown || _advancingPeriod || IsReturningToTitle || HasMilitaryOrderPage ||
            HasAdministrationPage && !SceneryHexMenuPresent || HasCampaignSavePage || HasMapSystemMenu ||
            HasCommandWorkspace || HasActionReport || HasPersonnelConfirmation || _detailsView is not null ||
            _repository is null || _campaignComposition is not { } composition ||
            _session is not { PlayerForceId: int player } session || session.HasPendingOperation ||
            session.World is not { Phase: TurnPhase.Strategy } world ||
            _residentMap is not { IsPlayableMapReady: true } map) return false;
        var rules = composition.PrepareArmyQueries(world);
        var preparation = rules.PrepareGeneralUnitTarget(world, target);
        if (!preparation.Available)
        { GeneralUnitTargetUnavailable(preparation.UnresolvedOwner ?? "general_unit_target.preparation"); return false; }
        if (preparation.ForceId != player)
        { GeneralUnitTargetUnavailable("general_unit_target.current_player"); return false; }
        var view = new DeploymentTargetView();
        var owner = new GeneralUnitTargetWorkspace(session, world, map, preparation, view, returned);
        _generalUnitTarget = owner; AddChild(view);
        try
        {
            owner.Presenter = new(view, _repository, _ui, world, rules, preparation,
                map.ProjectDeploymentTarget, map.ProjectWaypointMarker,
                rows => RunPlayAction(() => PrepareGeneralUnitTargetNarration(owner, rows)),
                () => RunUiAction(() => ReturnGeneralUnitTarget(owner)), GeneralUnitTargetUnavailable,
                modal => { if (IsGeneralUnitTargetOwnerCurrent(owner)) { map.SetDetailsOpen(modal, keepMapVisible: true); HideDeploymentHud(); } },
                saveDefaults: (behavior, accepted) => SaveGeneralUnitTargetDefaults(owner, behavior, accepted),
                scrollSpeed: CurrentListScrollSpeed,
                tutorialRestrictsFacilities: _campaignApplication?.TutorialRestrictsFacilities,
                chooseFalseReportDestination: (action, goal, accept) =>
                {
                    if (!IsGeneralUnitTargetOwnerCurrent(owner) || action is not
                        { ActionKey: "capture", Target.Kind: DeploymentTargetKind.Unit }) return;
                    if (!OpenFalseReportTarget(owner.World, map, player, action.Target.Id, view,
                        () => IsGeneralUnitTargetOwnerCurrent(owner), () => BindGeneralUnitTargetMap(owner),
                        destination => accept(action with { FalseReportDestinationHexId = destination })))
                        GeneralUnitTargetUnavailable("general_unit_target.false_report_destination");
                },
                routePreviewChanged: map.SetDeploymentRoutePreviews,
                uiResources: TryBorrowApplicationUiResources(view));
            view.RegisterHeader(_sourceHeaders);
            SetMapFocus(true); map.CloseCityCommandMenu(); map.SetDetailsOpen(false, keepMapVisible: true);
            BindGeneralUnitTargetMap(owner); HideDeploymentHud();
            return true;
        }
        catch { CloseGeneralUnitTarget(); throw; }
    }

    private bool IsGeneralUnitTargetOwnerCurrent(GeneralUnitTargetWorkspace owner)
        => ReferenceEquals(_generalUnitTarget, owner) && IsGeneralUnitTargetCurrent;

    private void BindGeneralUnitTargetMap(GeneralUnitTargetWorkspace owner)
    {
        if (!IsGeneralUnitTargetOwnerCurrent(owner) || owner.Narration is not null || owner.Presenter is not { } presenter) return;
        owner.Map.SetCommandTargetPicker(hex =>
        {
            if (IsGeneralUnitTargetOwnerCurrent(owner) && owner.Narration is null)
                presenter.PickTarget(hex, owner.Map.SourceSelectionShiftHeld);
        }, hex => IsGeneralUnitTargetOwnerCurrent(owner) && owner.Narration is null && presenter.AllowPointer(hex),
            hex => { if (IsGeneralUnitTargetOwnerCurrent(owner) && owner.Narration is null) presenter.HoverTarget(hex); },
            supportsShiftWaypoint: true);
    }

    private bool SaveGeneralUnitTargetDefaults(GeneralUnitTargetWorkspace owner, DeploymentBehavior behavior,
        Action<GameState> accepted)
    {
        if (!IsGeneralUnitTargetOwnerCurrent(owner)) return false;
        bool saved = false;
        RunPlayAction(() =>
        {
            if (!IsGeneralUnitTargetOwnerCurrent(owner)) return;
            var expected = owner.World;
            saved = ApplyPlayableOrder(new SaveDeploymentDefaultsOrder(owner.Preparation.ForceId, behavior), () =>
            {
                owner.World = owner.Session.World;
                if (_sceneryHexMenu is not null && ReferenceEquals(_sceneryInvestmentWorld, expected))
                    _sceneryInvestmentWorld = owner.World;
                accepted(owner.World);
            });
            if (saved) { BindGeneralUnitTargetMap(owner); HideDeploymentHud(); }
        });
        return saved;
    }

    private void PrepareGeneralUnitTargetNarration(GeneralUnitTargetWorkspace owner, IReadOnlyList<DeploymentDraftRow> acceptedRows)
    {
        if (!IsGeneralUnitTargetOwnerCurrent(owner) || owner.Narration is not null || _repository is null ||
            _campaignComposition is not { } composition || owner.Presenter is not { } presenter) return;
        var byOfficer = acceptedRows.ToDictionary(row => row.Request.OfficerId);
        var armies = presenter.GeneralArmies.Select(army => army with
            { Row = byOfficer[army.Row.Request.OfficerId] }).ToArray();
        var rows = armies.Select(army=>army.Row).ToArray();
        var preparation = composition.PrepareArmyQueries(owner.World).QueryGeneralUnitTargetNarration(
            owner.World, rows, _ui.SourceLocale, _presentationRandom.Next);
        if (!preparation.Available || preparation.PortraitRoute is not { } portrait)
        { GeneralUnitTargetUnavailable(preparation.UnresolvedInput ?? "general_unit_target.narration"); return; }
        var sprites = new LocalizedUiSpriteSource(_repository);
        var layers = portrait.LoadSprites(sprites.LoadTexturePackSprite);
        var narration = new DeploymentNarrationView { KeyboardInput = HandleGameplayNarrationKeyboard };
        owner.Narration = narration; AddChild(narration);
        try
        {
            narration.Configure(_repository, _ui, preparation.SpeakerName!, preparation.Text!, layers, () => RunPlayAction(() =>
            {
                if (!IsGeneralUnitTargetOwnerCurrent(owner) || owner.Narration != narration) return;
                var order = new DeployArmiesOrder(owner.Preparation.ForceId, owner.Preparation.PrimaryDistrictId,
                    armies.Select(army => new OrdinaryArmyDeployment(army.SourceLocationId,
                        army.Row.Request with { NavalFormationId = army.Row.NavalFormationId })).ToArray());
                if (ApplyPlayableOrder(order, () => ReturnGeneralUnitTarget(owner))) return;
                owner.Narration = null; narration.Abandon(); narration.QueueFree();
                owner.View.Show(); owner.Map.SetDetailsOpen(false, keepMapVisible: true);
                BindGeneralUnitTargetMap(owner); HideDeploymentHud();
            }), () => _preferences.AutomaticMessageAdvance,
                () => CanAdvanceNarration && IsGeneralUnitTargetOwnerCurrent(owner) && owner.Narration == narration);
            narration.BindPreparedVoices(preparation.Voices, _sceneAudioBinding);
            owner.View.Hide(); owner.Map.SetCommandTargetPicker(null);
            owner.Map.SetDetailsOpen(true, keepMapVisible: true);
        }
        catch { owner.Narration = null; narration.Abandon(); narration.QueueFree(); throw; }
    }

    private void GeneralUnitTargetUnavailable(string reason)
    {
        var message = _ui.Get("play_deployment_unavailable");
        _playMessage.Text = message; _generalUnitTarget?.View.ApplyHint(message); GD.PushWarning(reason);
    }
    private void RequestGeneralUnitTargetBack()
    {
        if (_generalUnitTarget is not { } owner) return;
        if (!IsGeneralUnitTargetOwnerCurrent(owner)) { CloseGeneralUnitTarget(); return; }
        if (owner.Narration is not null) return;
        if (owner.Presenter?.TryCancelOverlay() == true) return;
        owner.View.RequestBack();
    }
    private void ReturnGeneralUnitTarget(GeneralUnitTargetWorkspace owner)
    {
        if (!ReferenceEquals(_generalUnitTarget, owner)) return;
        var returned = owner.Returned;
        CloseGeneralUnitTarget();
        returned?.Invoke();
    }
    private void CloseGeneralUnitTarget()
    {
        var owner = _generalUnitTarget; _generalUnitTarget = null;
        if (owner is null) return;
        CloseFalseReportTarget(owner.View);
        owner.Presenter?.Dispose(); owner.Narration?.Abandon(); owner.Narration?.QueueFree();
        owner.View.Abandon(); owner.View.QueueFree();
        if (GodotObject.IsInstanceValid(owner.Map))
        {
            owner.Map.SetCommandTargetPicker(null);
            if (_residentMap == owner.Map) owner.Map.SetDetailsOpen(false);
        }
        if (!_failed && !_shuttingDown && _residentMap is not null) SetMapFocus(_mapFocused);
    }
}
