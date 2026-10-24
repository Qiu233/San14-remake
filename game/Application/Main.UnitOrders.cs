using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private UnitOrderTargetView? _unitOrderTarget;
    private DeploymentTargetConfirmation? _unitOrderConfirmation;
    private GameplaySession? _unitOrderSession;
    private GameState? _unitOrderWorld;
    private MapSceneView? _unitOrderMap;
    private UnitTargetMenuPresenter? _unitTargetMenus;
    private UnitFacilitySelectionPresenter? _unitFacilitySelection;

    private bool CanOpenUnitFacilities() => _world is { Phase: TurnPhase.Strategy } world &&
        _session is { PlayerForceId: int player } && _residentMap is { IsPlayableMapReady: true } &&
        _repository is not null && _campaignComposition is not null && SelectionId(_playUnit) is int id &&
        world.Units.GetValueOrDefault(id) is { Mission: not (null or { Kind: UnitMissionKind.Transport }) } unit &&
        unit.ForceId == player && PlayableOutcome.Evaluate(world, player) == PlayableResult.Ongoing;

    private bool OpenSelectedUnitFacilities()
    {
        if (_world?.NativeArmySimulation is null) return false;
        if (CanOpenUnitFacilities()) OpenUnitRetarget(_playUnit.GetSelectedId(), _selectedCell?.Id);
        return true;
    }

    private void RefreshPlayRetargetAvailability()
    {
        var unit = _world?.Units.GetValueOrDefault(SelectionId(_playUnit) ?? -1);
        _playRetarget.Disabled = _world is not { Phase: TurnPhase.Strategy } || unit is null ||
            unit.ForceId != _playerForceId || unit.Mission is null ||
            (unit.Mission.Kind == UnitMissionKind.Transport && _world.NativeArmySimulation is null ? _selectedCell is null :
                _residentMap is null || _repository is null || _campaignComposition is null);
    }

    private void OpenUnitRetarget(int unitId, int? initialHex = null)
    {
        if (_failed || _shuttingDown || _advancingPeriod || IsReturningToTitle || HasMilitaryOrderPage ||
            HasAdministrationPage || HasCampaignSavePage || HasMapSystemMenu || HasActionReport || HasPersonnelConfirmation ||
            _detailsView is not null || _repository is null || _campaignComposition is not { } composition ||
            _residentMap is not { IsPlayableMapReady: true } map ||
            _session is not { PlayerForceId: int player } session || session.World is not { Phase: TurnPhase.Strategy } world ||
            !world.Units.TryGetValue(unitId, out var unit) || unit.ForceId != player ||
            unit.Mission is null || unit.Mission.Kind == UnitMissionKind.Transport && world.NativeArmySimulation is null) return;
        var commands = _militaryViews.Create(world, unitId, player);
        if (!commands.CanIssue) return;
        var armyRules = composition.PrepareArmyQueries(world);
        var policies = armyRules.QueryDeploymentPolicyOptions(world,
            unit.Mission.Kind == UnitMissionKind.Transport ? [] : [unit.OfficerId]);
        var construction = armyRules.QueryFacilityConstructionChoices();
        var structures = (world.Structures ?? []).Values.Where(item => item.ForceId == player).OrderBy(item => item.Id)
            .Select(item => new UnitFacilityTarget(item.Id, FacilityName(item), item.Durability)).ToArray();
        CloseCommandWorkspace();
        var view = new UnitOrderTargetView();
        _unitOrderTarget = view; _unitOrderSession = session; _unitOrderWorld = world; _unitOrderMap = map;
        int? selected = null;
        int? quotedHex = null;
        string? quotedDefinition = null;
        FacilityConstructionQuote? quote = null;
        var facilityCapacities = new Dictionary<int, int?>();
        AddChild(view);
        try
        {
            view.Configure(_repository, _ui, _ui.Format("play_unit_item", world.Officers[unit.OfficerId].Name, unit.Troops),
                TargetName(unit.DestinationHexId), RequestReturn, RequestConfirm, commands, RequestCommand,
                _playCommandDeck.Theme, policies, unit.Behavior, construction, structures, RefreshConstructionQuote, ReadFacilityCapacity,
                existingArmyMode: world.NativeArmySimulation is not null);
            if (world.NativeArmySimulation is not null)
            {
                _unitTargetMenus = new(view, _repository, _ui, world, armyRules, unitId,
                    map.ProjectDeploymentTarget, map.ProjectWaypointMarker, Current, construction.Count > 0, RequestSourceCommand,
                    routePreviewChanged:map.SetDeploymentRoutePreviews);
                view.ConfigureSourceDetails(() =>
                {
                    if (Current() && _unitTargetMenus is { } detailsMenus)
                        OpenSelectedUnitSourceDetails(detailsMenus.SelectedUnitIds, view);
                });
                view.ConfigureSourceWithdraw(unit.NativeTacticRuntime is { Abnormal23: 0, Abnormal24: 0, Abnormal25: 0 },
                    () => RequestCommand(UnitMapOrder.Withdraw, null));
                view.ConfigureSourceFacilities(() => OpenFacilitySelection());
                _unitTargetMenus.ConfigureSelection(map.ProjectWaypointMarker, SelectionInputAllowed,
                    RefreshSelection, CloseUnitRetarget);
                RefreshSelection();
            }
            SetMapFocus(true); map.SetDetailsOpen(false, keepMapVisible: true);
            _forceHud?.Hide(); _mapFocusToggle.Hide();
            BindTargetPickers();
            if (initialHex is int hex && Allowed(hex)) SelectTarget(hex);
        }
        catch { CloseUnitRetarget(); throw; }

        bool Current() => !_failed && !_shuttingDown && _detailsView is null && _unitOrderTarget == view &&
            ReferenceEquals(_session, session) && ReferenceEquals(_world, world) && _residentMap == map;
        bool Allowed(int hex) => Current() && _unitOrderConfirmation is null &&
            (uint)hex < (uint)(world.MapWidth * world.MapHeight) &&
            (hex != unit.HexId || _unitTargetMenus is not null);
        bool SelectionInputAllowed() => Current() && _falseReportTarget is null && _unitOrderConfirmation is null && _unitFacilitySelection is null && map.SourcePointerInputActive;
        void BindTargetPickers()
        {
            if(!Current())return;
            map.SetCommandTargetPicker(hex =>
            {
                if (_detailsView is not null) return;
                if (!Current()) { CloseUnitRetarget(); return; }
                if (_unitOrderConfirmation is not null || !Allowed(hex)) return;
                if (_unitFacilitySelection?.PickTarget(hex) == true) return;
                SelectTarget(hex);
            }, Allowed, hex =>
            {
                if (Current() && _unitOrderConfirmation is null && _unitFacilitySelection is null)
                    _unitTargetMenus?.HoverTarget(hex);
            });
            if (_unitTargetMenus is { } selectionMenus)
                map.SetUnitSelectionPicker(selectionMenus.ClickArmy, selectionMenus.SampleSelectionPointer, selectionMenus.ResetSelectionGesture);
        }
        void RefreshSelection()
        {
            if (_unitTargetMenus is not { } menus) return;
            var ids = menus.SelectedUnitIds;
            selected = null; quote = null; quotedHex = null; quotedDefinition = null;
            if (ids.Count > 0) { unitId = ids[0]; unit = world.Units[unitId]; }
            if (menus.SelectedPartition == UnitSelectionPartition.Transport || ids.Count == 0 && view.SourceTransportPoliciesActive)
            {
                var transport = menus.QueryTransportSelection();
                int? behavior = transport.SourceBehaviorChoice.Value;
                bool? returnOfficer = transport.ReturnOfficerToSource.Value;
                view.ConfigureSourceTransportPolicies(ids.Count > 0 && transport.CanEdit == true,
                    behavior, returnOfficer, CanSaveTransportDefaults(behavior, returnOfficer),
                    RequestTransportOption, RequestTransportDefaults);
                bool details = ids.Count > 0 && transport.Rows.Count == ids.Count &&
                    transport.Rows.All(row => row.DetailsAvailable == true);
                bool transportCanWithdraw = transport.Rows.Any(row => row.WithdrawalAvailable == true);
                view.ApplySourceSelection(ids.Count == 1, transportCanWithdraw, false, detailsAvailable: details);
                view.ApplySourceWithdrawalEligibility(transport.Rows.Any(row => row.WithdrawalAvailable == false));
                return;
            }
            policies = armyRules.QueryDeploymentPolicyOptions(world, ids.Select(id => world.Units[id].OfficerId).ToArray());
            view.ConfigureSourceSelectionPolicies(policies, ids.Select(id => world.Units[id].Behavior).ToArray(), RequestPolicySetting);
            bool canWithdraw = ids.Any(id => world.Units[id].NativeTacticRuntime is { Abnormal23: 0, Abnormal24: 0, Abnormal25: 0 });
            view.ApplySourceSelection(ids.Count == 1, canWithdraw, ids.Count == 1, detailsAvailable: ids.Count > 0);
            view.ApplySourceWithdrawalEligibility(ids.Any(id => world.Units[id].NativeTacticRuntime is { } runtime &&
                (runtime.Abnormal23 != 0 || runtime.Abnormal24 != 0 || runtime.Abnormal25 != 0)));
        }
        bool CanSaveTransportDefaults(int? behavior, bool? returnOfficer)
        {
            int? savedBehavior = TransportSetupCurrentInputs.Read(world, TransportSetupCurrentInputs.BehaviorPreferenceKey);
            int? savedReturn = TransportSetupCurrentInputs.Read(world, TransportSetupCurrentInputs.ReturnOfficerPreferenceKey);
            return behavior is 1 or 2 && returnOfficer.HasValue && savedBehavior.HasValue && savedReturn.HasValue &&
                (savedBehavior != behavior || (savedReturn == 1) != returnOfficer.Value);
        }
        void RequestTransportOption(TransportTargetOption option, int value)
        {
            if (!SelectionInputAllowed() || _unitTargetMenus is not { SelectedPartition: UnitSelectionPartition.Transport } menus ||
                menus.SelectedUnitIds.Count == 0) return;
            var previous = world;
            var order = new SetTransportArmiesOptionsOrder(player, menus.SelectedUnitIds.ToArray(),
                option == TransportTargetOption.BehaviorChoice ? value : null,
                option == TransportTargetOption.ReturnOfficer ? value == 1 : null);
            if (ApplyPlayableOrder(order, () =>
                {
                    world = session.World; unit = world.Units[unitId]; _unitOrderWorld = world;
                    menus.AcceptPolicyWorld(previous, world);
                }))
            { RefreshSelection(); menus.RefreshSelectedTarget(); }
            else { RefreshSelection(); view.ApplyHint(_playMessage.Text); }
        }
        void RequestTransportDefaults()
        {
            if (!SelectionInputAllowed() || _unitTargetMenus is not { SelectedPartition: UnitSelectionPartition.Transport } menus ||
                menus.SelectedUnitIds.Count == 0) return;
            var options = menus.QueryTransportSelection();
            if (options.SourceBehaviorChoice.Value is not int behavior || options.ReturnOfficerToSource.Value is not bool returns ||
                !CanSaveTransportDefaults(behavior, returns)) return;
            var previous = world;
            if (ApplyPlayableOrder(new SaveTransportDefaultsOrder(player, returns, behavior), () =>
                {
                    world = session.World; unit = world.Units[unitId]; _unitOrderWorld = world;
                    menus.AcceptPolicyWorld(previous, world);
                }))
            { RefreshSelection(); menus.RefreshSelectedTarget(); }
            else { RefreshSelection(); view.ApplyHint(_playMessage.Text); }
        }
        void RequestPolicySetting(DeploymentPolicySetting setting, bool first)
        {
            if (!SelectionInputAllowed() || _unitTargetMenus is not { } menus || menus.SelectedUnitIds.Count == 0) return;
            UnitBehaviorSetting field = setting switch
            {
                DeploymentPolicySetting.EnemyApproach => UnitBehaviorSetting.AttackNearby,
                DeploymentPolicySetting.Retreat => UnitBehaviorSetting.AllowRetreat,
                DeploymentPolicySetting.Pursuit => UnitBehaviorSetting.Pursue,
                DeploymentPolicySetting.AfterAction => UnitBehaviorSetting.AfterRetreat,
                _ => throw new ArgumentOutOfRangeException(nameof(setting))
            };
            var previous = world;
            if (ApplyPlayableOrder(new UpdateUnitsBehaviorSettingOrder(player, menus.SelectedUnitIds.ToArray(), field, first), () =>
                {
                    world = session.World; unit = world.Units[unitId]; _unitOrderWorld = world;
                    menus.AcceptPolicyWorld(previous, world);
                }))
            { RefreshSelection(); menus.RefreshSelectedTarget(); }
            else { RefreshSelection(); view.ApplyHint(_playMessage.Text); }
        }
        void RequestReturn()
        {
            if (TryCancelFalseReportTarget(view)) return;
            if (_unitFacilitySelection?.TryCancel() == true) return;
            CloseUnitRetarget();
        }
        void OpenFacilitySelection(int? capturedTarget = null, IReadOnlyList<int>? capturedPoints = null)
        {
            if (!Current() || _unitFacilitySelection is not null || _unitOrderConfirmation is not null || _unitTargetMenus is { SelectedUnitIds.Count: not 1 }) return;
            _unitTargetMenus?.CloseOverlay();
            selected = null; quote = null; quotedHex = null; quotedDefinition = null;
            var directory = armyRules.QueryUnitFacilityDirectory(world, unitId, capturedTarget,
                _campaignApplication?.TutorialRestrictsFacilities);
            var entries = directory.Rows.Where(choice => choice.OriginalIncluded != false)
                .Select(choice => new UnitFacilitySelectionEntry(
                    new(choice.DefinitionKey, choice.Name ?? _ui.Get("play_facility_name_unavailable")),
                    choice.OriginalIncluded == true ? choice.OriginalAllowed : null,
                    choice.OriginalReason, choice.CommandSupported && choice.Name is not null)).ToArray();
            UnitFacilitySelectionPresenter? flow = null;
            flow = new(view, _repository!, _ui, world, armyRules, unitId, entries,
                capturedPoints ?? _unitTargetMenus?.Waypoints ?? view.Waypoints, Current,
                modal => { if (_residentMap == map && GodotObject.IsInstanceValid(map)) map.SetDetailsOpen(modal, keepMapVisible: true); },
                (choice, hex, points) => Current() && ApplyPlayableOrder(new SetUnitActionOrder(player, unitId,
                    new("build", new(DeploymentTargetKind.Hex, hex), choice.DefinitionKey), points.ToArray()), CloseUnitRetarget),
                () =>
                {
                    if (_unitFacilitySelection != flow) return;
                    _unitFacilitySelection = null;
                    if (Current()) map.ReplayCommandTargetHover();
                }, capturedTarget);
            _unitFacilitySelection = flow;
            try { flow.Open(); }
            catch { flow.Dispose(); throw; }
        }
        int? ReadFacilityCapacity(int structureId)
        {
            if (!Current()) return null;
            if (!facilityCapacities.TryGetValue(structureId, out int? capacity))
            {
                capacity = armyRules.QueryFacilityRepairCapacity(world, structureId);
                facilityCapacities.Add(structureId, capacity);
            }
            return capacity;
        }
        void SelectTarget(int hex)
        {
            selected = hex;
            view.ApplyTarget(hex, _ui.Format("play_unit_target_selected", TargetName(hex)));
            RefreshConstructionQuote();
            _unitTargetMenus?.PickTarget(hex, map.SourceSelectionShiftHeld);
        }
        void RefreshConstructionQuote()
        {
            if (!Current() || selected is not int hex || view.FacilityDefinitionKey is not string definition ||
                quotedHex == hex && quotedDefinition == definition) return;
            quote = armyRules.QueryFacilityConstructionQuote(world, unitId, hex, definition);
            quotedHex = hex; quotedDefinition = definition;
            string? payer = quote.PayerLocationId is int location
                ? world.Cities.GetValueOrDefault(location)?.Name ?? world.Gates.GetValueOrDefault(location)?.Name : null;
            view.ApplyConstructionQuote(quote, payer);
        }
        string FacilityName(StructureState item) => _ui.Format("play_facility_target",
            composition.Construction.NameFor(item.KindId) ?? _ui.Get("detail_unknown"), TargetName(item.HexId),
            item.Durability, item.MaxDurability is > 0 ? item.MaxDurability.ToString() : _ui.Get("detail_unknown"));
        string TargetName(int hex)
        {
            string name = world.Cities.Values.FirstOrDefault(city => city.HexId == hex)?.Name ??
                world.Gates.Values.FirstOrDefault(gate => gate.HexId == hex)?.Name ??
                (world.Territory.GetValueOrDefault(hex)?.AreaId is int area ? world.Areas.GetValueOrDefault(area)?.Name : null) ??
                _ui.Get("detail_unknown");
            return $"{name} ({hex % world.MapWidth}, {hex / world.MapWidth})";
        }
        void RequestConfirm()
        {
            if (!Current() || selected is not int goal || _unitOrderConfirmation is not null) return;
            if (_unitTargetMenus is not null) return;
            var action = new DeploymentAction("move", new DeploymentTarget(DeploymentTargetKind.Hex, goal));
            ConfirmOrder(new SetUnitActionOrder(player, unitId, action,
                    view.Waypoints),
                _ui.Format("play_unit_target_confirm", world.Officers[unit.OfficerId].Name, TargetName(action.Target.Id)));
        }
        void RequestCommand(UnitMapOrder command, DeploymentTarget? target)
        {
            if (!Current() || _unitOrderConfirmation is not null) return;
            if (_unitTargetMenus is { } menus && command == UnitMapOrder.ChangeBehavior && view.Behavior is { } desired)
            {
                var previous = world;
                if (ApplyPlayableOrder(new UpdateUnitBehaviorOrder(player, unitId, desired), () =>
                    {
                        world = session.World;
                        unit = world.Units[unitId];
                        _unitOrderWorld = world;
                        menus.AcceptPolicyWorld(previous, world);
                    }))
                {
                    view.ApplyCommittedBehavior(unit.Behavior);
                    menus.RefreshSelectedTarget();
                }
                else
                {
                    view.ApplyCommittedBehavior(unit.Behavior);
                    view.ApplyHint(_playMessage.Text);
                }
                return;
            }
            GameOrder order;
            string description;
            switch (command)
            {
                case UnitMapOrder.Stay:
                    order = new SetUnitActionOrder(player, unitId, new("stay", new(DeploymentTargetKind.Hex, unit.HexId)), view.Waypoints);
                    description = _ui.Get("play_military_stay_action"); break;
                case UnitMapOrder.Withdraw:
                    order = _unitTargetMenus is { } withdrawals
                        ? new WithdrawUnitsOrder(player, withdrawals.SelectedUnitIds.ToArray(), withdrawals.WithdrawalSavedGroups) : new WithdrawUnitOrder(player, unitId);
                    description = _ui.Get("play_withdraw"); break;
                case UnitMapOrder.Enter when target is { Kind: DeploymentTargetKind.Location } location:
                    order = new SetUnitActionOrder(player, unitId, new("enter", location), view.Waypoints);
                    description = _ui.Format("play_unit_order_action_target", _ui.Get("play_military_enter"),
                        commands.EntryTargets.Single(entry => entry.Id == location.Id).Name); break;
                case UnitMapOrder.Attack when target is not null:
                    order = new SetUnitActionOrder(player, unitId, new("attack", target), view.Waypoints);
                    description = _ui.Format("play_unit_order_action_target", _ui.Get("play_military_attack"),
                        commands.AttackTargets.Single(entry => entry.Target == target).Name); break;
                case UnitMapOrder.ChangeBehavior when view.Behavior is { } behavior:
                    order = new UpdateUnitBehaviorOrder(player, unitId, behavior);
                    description = _ui.Get("play_military_apply_behavior"); break;
                case UnitMapOrder.ChangeWaypoints:
                    order = new SetUnitWaypointsOrder(player, unitId, view.Waypoints);
                    description = _ui.Get("play_military_apply_waypoints"); break;
                case UnitMapOrder.Build when selected is int site && view.FacilityDefinitionKey is string definition && quote?.Allowed == true:
                    order = new SetUnitActionOrder(player, unitId, new("build", new(DeploymentTargetKind.Hex, site), definition),
                        view.Waypoints);
                    description = _ui.Format("play_facility_build_confirm", view.FacilityDefinitionName, TargetName(site), quote.GoldCost); break;
                case UnitMapOrder.Repair or UnitMapOrder.Demolish when target is { Kind: DeploymentTargetKind.Structure } facility:
                    if (structures.FirstOrDefault(item => item.Id == facility.Id) is not { } choice) return;
                    string action = command == UnitMapOrder.Repair ? "repair" : "demolish";
                    order = new SetUnitActionOrder(player, unitId, new(action, facility), view.Waypoints);
                    description = _ui.Format("play_unit_order_action_target", _ui.Get("play_facility_" + action), choice.Caption); break;
                default: return;
            }
            if (_unitTargetMenus is not null)
            {
                if (!ApplyPlayableOrder(order, CloseUnitRetarget)) view.ApplyHint(_playMessage.Text);
            }
            else ConfirmOrder(order, _ui.Format("play_unit_order_confirm", world.Officers[unit.OfficerId].Name, description));
        }
        void RequestSourceCommand(UnitTargetMenuOption option, IReadOnlyList<int> waypoints, int? targetHex, int clickedHex)
        {
            if (!Current() || _unitOrderConfirmation is not null) return;
            if (option.ActionKey == "build")
            {
                if (targetHex is int target) OpenFacilitySelection(target, waypoints);
                return;
            }
            if (option.Action is not { } action) return;
            if (_unitTargetMenus is not { } menus || menus.SelectedUnitIds.Count == 0) return;
            if(action is {ActionKey:"capture",Target.Kind:DeploymentTargetKind.Unit}&&
                menus.SelectedPartition==UnitSelectionPartition.Ordinary)
            {
                var actors=menus.SelectedUnitIds.ToArray(); var points=waypoints.ToArray();
                if(!OpenFalseReportTarget(world,map,player,action.Target.Id,view,Current,BindTargetPickers,destination=>
                {
                    if(!ApplyPlayableOrder(new IssueArmiesFalseReportOrder(player,actors,action.Target.Id,destination,points),CloseUnitRetarget))
                        view.ApplyHint(_playMessage.Text);
                }))view.ApplyHint(_ui.Get("play_unit_selection_unavailable"));
                return;
            }
            GameOrder order;
            if (action is { ActionKey: "fire", Target.Kind: DeploymentTargetKind.Hex } &&
                menus.SelectedPartition == UnitSelectionPartition.Ordinary)
                order = new SetUnitsFireOrder(player, menus.SelectedUnitIds.ToArray(), clickedHex, action.Target.Id, waypoints.ToArray());
            else
            {
                if (!PlayableWaypointMarch.IsSupportedAction(action)) return;
                order = menus.SelectedPartition == UnitSelectionPartition.Transport
                    ? new SetTransportArmiesActionOrder(player, menus.SelectedUnitIds.ToArray(), action, waypoints.ToArray())
                    : new SetUnitsActionOrder(player, menus.SelectedUnitIds.ToArray(), action, waypoints.ToArray());
            }
            if (!ApplyPlayableOrder(order, CloseUnitRetarget))
                view.ApplyHint(_playMessage.Text);
        }
        void ConfirmOrder(GameOrder order, string prompt)
        {
            var question = new DeploymentTargetConfirmation(); _unitOrderConfirmation = question; AddChild(question);
            view.SetBusy(true); map.SetDetailsOpen(true, keepMapVisible: true);
            _unitTargetMenus?.SetBusy(true);
            try
            {
                question.ConfigureWrappedMessage(_repository!, _ui, prompt, accepted => RunPlayAction(() =>
                    {
                        if (_unitOrderConfirmation != question) return;
                        if (!Current()) { CloseUnitRetarget(); return; }
                        CloseUnitOrderConfirmation();
                        if (accepted && !ApplyPlayableOrder(order, CloseUnitRetarget))
                            view.ApplyHint(_playMessage.Text);
                    }));
                question.Name = "UnitOrderConfirmation";
            }
            catch { CloseUnitOrderConfirmation(); throw; }
        }
    }

    // This auxiliary editor begins a new withdrawal page from its own
    // ordered selection. It does not own the map mode3 retained row history.
    private void RequestMilitaryPanelOrder(GameOrder order)
    {
        if (order is WithdrawUnitsOrder { SavedGroups: null } withdrawals &&
            _world is { NativeArmySimulation: not null } world && _campaignComposition is { } composition)
        {
            var rows = composition.PrepareArmyQueries(world).QueryUnitSelectionPageRows(world, withdrawals.UnitIds);
            if (rows.UnresolvedInput is not null || rows.Rows.Any(row => row.UnresolvedInput is not null || row.SavedGroup is null))
            { _playMessage.Text = _ui.Get("play_unit_selection_unavailable"); return; }
            var groups = UnitSelectionSavedGroupOperations.SortAndMerge(rows.Rows.Select(row => row.SavedGroup!).ToArray());
            if (groups.UnresolvedInput is not null)
            { _playMessage.Text = _ui.Get("play_unit_selection_unavailable"); return; }
            order = withdrawals with { SavedGroups = groups.Groups.Select(group =>
                new ArmyWithdrawalSavedGroup(group.OrderedUnitIds, group.PhysicalPoints,
                    group.ActiveWaypointIndex, group.HasFinalTarget)).ToArray() };
        }
        ApplyPlayableOrder(order);
    }

    private void CloseUnitOrderConfirmation()
    {
        var question = _unitOrderConfirmation; _unitOrderConfirmation = null;
        if (question is not null) { question.Abandon(); question.QueueFree(); }
        _unitOrderTarget?.SetBusy(false);
        _unitTargetMenus?.SetBusy(false);
        if (_unitOrderMap is { } map && _residentMap == map && GodotObject.IsInstanceValid(map))
            map.SetDetailsOpen(false, keepMapVisible: true);
    }

    private void CancelUnitTarget()
    {
        if (TryCancelFalseReportTarget(_unitOrderTarget)) return;
        if (_unitOrderConfirmation is { } question) { question.RequestCancel(); return; }
        if (_unitFacilitySelection?.TryCancel() == true) return;
        if (_unitTargetMenus?.TryCancelOverlay() == true) return;
        CloseUnitRetarget();
    }

    private void CloseUnitRetarget()
    {
        if (_commandDetailsUnitParent is not null) CloseCommandDetails(resumeParent: false);
        var map = _unitOrderMap;
        var view = _unitOrderTarget;
        CloseFalseReportTarget(view);
        var facilities = _unitFacilitySelection; _unitFacilitySelection = null; facilities?.Dispose();
        if (map is not null && GodotObject.IsInstanceValid(map)) map.SetUnitSelectionPicker();
        if(map is not null&&GodotObject.IsInstanceValid(map)&&_residentMap==map)map.SetDeploymentRoutePreviews([]);
        _unitTargetMenus?.Dispose(); _unitTargetMenus = null;
        CloseUnitOrderConfirmation();
        _unitOrderTarget = null; _unitOrderSession = null; _unitOrderWorld = null; _unitOrderMap = null;
        if (view is not null) { view.Abandon(); view.QueueFree(); }
        if (map is not null && _residentMap == map && GodotObject.IsInstanceValid(map))
        { map.SetCommandTargetPicker(null); map.SetDetailsOpen(false); }
        if (!_failed && !_shuttingDown && map is not null && _residentMap == map) SetMapFocus(true);
    }
}
