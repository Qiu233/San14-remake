using Godot;
using San14.Core;
using System.Globalization;

/// <summary>Reusable command editor. Simulation authority and validation remain in GameEngine.</summary>
public partial class MilitaryOrdersPanel : VBoxContainer
{
    private readonly Label _title = new();
    private readonly Label _unitSummary = new() { Name = "MilitaryUnitSummary", AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Label _nativeWaiting = new() { Name = "MilitaryNativeWaiting", AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Label _departureSource = new() { Name = "MilitaryDepartureSource", AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Label _nativeLogistics = new() { Name = "MilitaryNativeLogistics", AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Label _actionStatus = new() { Name = "MilitaryActionStatus", AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Label _attackTitle = new();
    private readonly OptionButton _attackTarget = new() { Name = "MilitaryAttackTarget" };
    private readonly Button _attack = new() { Name = "MilitaryAttack" };
    private readonly Label _enterTitle = new();
    private readonly OptionButton _enterTarget = new() { Name = "MilitaryEnterTarget" };
    private readonly Button _enter = new() { Name = "MilitaryEnter" };
    private readonly Button _stay = new() { Name = "MilitaryStay" };
    private readonly Label _waypointTitle = new();
    private readonly ItemList _waypoints = new() { Name = "MilitaryWaypointList", CustomMinimumSize = new(0, 112) };
    private readonly Button _add = new() { Name = "MilitaryWaypointAdd" };
    private readonly Button _remove = new() { Name = "MilitaryWaypointRemove" };
    private readonly Button _clear = new() { Name = "MilitaryWaypointClear" };
    private readonly Button _applyRoute = new() { Name = "MilitaryWaypointApply" };
    private readonly CheckButton _nearby = new() { Name = "MilitaryAttackNearby", ButtonPressed = true };
    private readonly CheckButton _retreat = new() { Name = "MilitaryAllowRetreat", ButtonPressed = true };
    private readonly CheckButton _pursuit = new() { Name = "MilitaryPursueRetreating" };
    private readonly Label _afterTitle = new();
    private readonly OptionButton _after = new() { Name = "MilitaryAfterAction" };
    private readonly Button _applyBehavior = new() { Name = "MilitaryBehaviorApply" };
    private readonly List<int> _draft = [];
    private readonly List<(DeploymentTarget Target, int HexId)> _attackTargets = [];
    private Func<string, string>? _text;
    private UnitState? _boundUnit;
    private int? _forceId;
    private int? _unitId;
    private int? _selectedHex;
    private int _mapSize;
    private bool _strategy;
    private Action<GameOrder>? _issue;
    private GameState? _world;

    public MilitaryOrdersPanel()
    {
        Name = "MilitaryOrdersPanel";
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 6);
        AddChild(_title);
        AddBatchSelectionControls();
        AddWithdrawalControls();
        AddChild(_unitSummary);
        AddChild(_nativeWaiting); AddChild(_departureSource); AddChild(_nativeLogistics);
        var attackRow = new HFlowContainer();
        attackRow.AddChild(_attackTitle); attackRow.AddChild(_attackTarget); attackRow.AddChild(_attack);
        AddChild(attackRow);
        var enterRow = new HFlowContainer();
        enterRow.AddChild(_enterTitle); enterRow.AddChild(_enterTarget); enterRow.AddChild(_enter); enterRow.AddChild(_stay);
        AddChild(enterRow); AddChild(_actionStatus);
        AddChild(_nearby);
        AddChild(_retreat);
        AddChild(_pursuit);
        var afterRow = new HBoxContainer();
        afterRow.AddChild(_afterTitle);
        afterRow.AddChild(_after);
        AddChild(afterRow);
        AddChild(_applyBehavior);
        AddChild(_waypointTitle);
        AddChild(_waypoints);
        var editRow = new HBoxContainer();
        editRow.AddChild(_add); editRow.AddChild(_remove); editRow.AddChild(_clear);
        AddChild(editRow);
        AddChild(_applyRoute);
        _add.Pressed += () => { if (_selectedHex is int hex) AddWaypoint(hex); };
        _remove.Pressed += RemoveSelectedWaypoint;
        _clear.Pressed += ClearWaypoints;
        _waypoints.ItemSelected += _ => UpdateEnabled();
        _attackTarget.ItemSelected += _ => UpdateEnabled();
        _enterTarget.ItemSelected += _ => UpdateEnabled();
        _attack.Pressed += () =>
        {
            if (!_attack.Disabled && _attackTarget.Selected >= 0)
                RequestAction(new DeploymentAction("attack", _attackTargets[_attackTarget.Selected].Target));
        };
        _enter.Pressed += () =>
        {
            if (!_enter.Disabled && _enterTarget.Selected >= 0)
                RequestAction(new DeploymentAction("enter", new DeploymentTarget(DeploymentTargetKind.Location, _enterTarget.GetSelectedId())));
        };
        _stay.Pressed += () =>
        {
            int[] units = CommandUnitIdsSnapshot();
            int? destination = _selectedHex ?? (units.Length == 1
                ? _world?.Units.GetValueOrDefault(units[0])?.HexId : null);
            if (!_stay.Disabled && destination is int hex)
                RequestAction(new DeploymentAction("stay", new DeploymentTarget(DeploymentTargetKind.Hex, hex)));
        };
        _applyRoute.Pressed += () =>
        {
            int[] units = CommandUnitIdsSnapshot();
            if (_forceId is int force && units.Length > 0 && _strategy && !_applyRoute.Disabled)
                _issue?.Invoke(units.Length == 1
                    ? new SetUnitWaypointsOrder(force, units[0], WaypointSnapshot())
                    : new SetUnitsWaypointsOrder(force, units, WaypointSnapshot()));
        };
        _applyBehavior.Pressed += () =>
        {
            int[] units = CommandUnitIdsSnapshot();
            if (_forceId is int force && units.Length > 0 && _strategy)
                _issue?.Invoke(units.Length == 1
                    ? new UpdateUnitBehaviorOrder(force, units[0], BehaviorSelection)
                    : new UpdateUnitsBehaviorOrder(force, units, BehaviorSelection));
        };
        UpdateEnabled();
    }

    public DeploymentBehavior BehaviorSelection => new(_nearby.ButtonPressed,
        _retreat.ButtonPressed, _pursuit.ButtonPressed,
        _after.Selected < 0 ? DeploymentAfterAction.Stay : (DeploymentAfterAction)_after.GetSelectedId());
    public int[] WaypointSnapshot() => _draft.ToArray();

    /// <summary>Use for both deployment composition and an already deployed unit. No order is emitted during binding.</summary>
    public void Configure(Func<string, string> text, Action<GameOrder> issue)
    {
        _issue = issue ?? throw new ArgumentNullException(nameof(issue));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _title.Text = _text("play_military_title");
        _attackTitle.Text = _text("play_military_attack_target");
        _enterTitle.Text = _text("play_military_enter_target");
        _attack.Text = _text("play_military_attack"); _enter.Text = _text("play_military_enter");
        _stay.Text = _text("play_military_stay_action"); _stay.TooltipText = _text("play_military_stay_hint");
        _nearby.Text = _text("play_military_attack_nearby");
        _retreat.Text = _text("play_military_allow_retreat");
        _pursuit.Text = _text("play_military_pursuit");
        _afterTitle.Text = _text("play_military_after_action");
        DeploymentAfterAction selected = BehaviorSelection.AfterAction;
        _after.Clear();
        _after.AddItem(_text("play_military_stay"), (int)DeploymentAfterAction.Stay);
        _after.AddItem(_text("play_military_retreat"), (int)DeploymentAfterAction.Retreat);
        _after.Select(_after.GetItemIndex((int)selected));
        _add.Text = _text("play_military_add_selected_hex");
        _remove.Text = _text("play_military_remove");
        _clear.Text = _text("play_military_clear");
        _applyRoute.Text = _text("play_military_apply_waypoints");
        _applyBehavior.Text = _text("play_military_apply_behavior");
        ConfigureBatchSelection();
        ConfigureWithdrawal();
        RenderWaypoints();
        RenderActionStatus();
        RenderNativeState();
    }

    public void Refresh(GameState? world, int? selectedUnitId, int? playerForceId, int? selectedHexId = null)
    {
        _forceId = playerForceId;
        _world = world;
        _unitId = null;
        _selectedHex = selectedHexId;
        _mapSize = world is null ? 0 : checked(world.MapWidth * world.MapHeight);
        _strategy = world?.Phase == TurnPhase.Strategy && playerForceId is int force &&
            world.Forces.TryGetValue(force, out ForceState? owner) && owner.IsActive &&
            PlayableOutcome.Evaluate(world, force) == PlayableResult.Ongoing;
        UnitState? unit = world is not null && selectedUnitId is int id &&
            world.Units.TryGetValue(id, out UnitState? candidate) && candidate.ForceId == playerForceId
            ? candidate : null;
        if (unit?.Mission is not null and not { Kind: UnitMissionKind.Transport }) _unitId = unit.Id;
        RefreshBatchSelection(unit);
        SetMeta("military_selected_unit_id", _unitId ?? -1);
        SetMeta("military_plan_waypoints", unit?.MilitaryPlan?.WaypointHexIds.Length ?? 0);
        SetMeta("military_plan_waypoint_cursor", unit?.MilitaryPlan?.NextWaypointIndex ?? 0);
        SetMeta("military_plan_complete", unit?.MilitaryPlan?.Completed ?? false);
        SetMeta("military_plan_action", unit?.MilitaryPlan?.Action.ActionKey ?? "");
        SetMeta("military_plan_target_id", unit?.MilitaryPlan?.Action.Target.Id ?? -1);
        SetMeta("military_plan_target_kind", unit?.MilitaryPlan?.Action.Target.Kind.ToString() ?? "");
        if (!ReferenceEquals(_boundUnit, unit))
        {
            _draft.Clear();
            if (unit?.MilitaryPlan is { Completed: false } plan)
                _draft.AddRange(plan.WaypointHexIds.Skip(plan.NextWaypointIndex));
            DeploymentBehavior selected = unit?.Behavior ?? new(true, true, false, DeploymentAfterAction.Stay);
            _nearby.ButtonPressed = selected.AttackNearbyEnemies;
            _retreat.ButtonPressed = selected.AllowRetreat;
            _pursuit.ButtonPressed = selected.PursueRetreatingEnemies;
            if (_after.ItemCount > 0) _after.Select(_after.GetItemIndex((int)selected.AfterAction));
            _boundUnit = unit;
            RenderWaypoints();
        }
        RefreshActionTargets();
        RenderActionStatus();
        RenderNativeState();
        UpdateEnabled();
    }

    public void SetSelectedHex(int? selectedHexId)
    {
        _selectedHex = selectedHexId;
        SelectMapTargets();
        UpdateEnabled();
    }

    public bool AddWaypoint(int hexId)
    {
        if (!_strategy || hexId < 0 || hexId >= _mapSize || _draft.Count >= PlayableWaypointMarch.MaximumWaypoints) return false;
        _draft.Add(hexId);
        RenderWaypoints();
        return true;
    }

    public void ClearWaypoints() { _draft.Clear(); RenderWaypoints(); }

    /// <summary>Issue a selected terminal command with the current intermediate destinations.</summary>
    public bool RequestAction(DeploymentAction action)
    {
        int[] units = CommandUnitIdsSnapshot();
        if (!_strategy || _forceId is not int force || units.Length == 0 ||
            action is null || !PlayableWaypointMarch.IsSupportedAction(action)) return false;
        _issue?.Invoke(units.Length == 1
            ? new SetUnitActionOrder(force, units[0], action, WaypointSnapshot())
            : new SetUnitsActionOrder(force, units, action, WaypointSnapshot()));
        return _issue is not null;
    }

    private void RemoveSelectedWaypoint()
    {
        int[] selected = _waypoints.GetSelectedItems();
        if (selected.Length == 0) return;
        int index = selected[0];
        if (index >= 0 && index < _draft.Count) _draft.RemoveAt(index);
        RenderWaypoints();
    }

    private void RefreshActionTargets()
    {
        DeploymentTarget? priorAttack = _attackTarget.Selected >= 0 && _attackTarget.Selected < _attackTargets.Count
            ? _attackTargets[_attackTarget.Selected].Target : null;
        int priorEnter = _enterTarget.Selected >= 0 ? _enterTarget.GetSelectedId() : -1;
        _attackTarget.Clear(); _enterTarget.Clear(); _attackTargets.Clear();
        if (_world is { } world && _forceId is int force)
        {
            foreach (UnitState enemy in world.Units.Values.Where(unit => PlayableDiplomacy.IsHostile(world, force, unit.ForceId)).OrderBy(unit => unit.Id))
            {
                string name = world.Officers.TryGetValue(enemy.OfficerId, out OfficerState? officer) ? officer.Name : enemy.OfficerId.ToString();
                AddAttack(new(DeploymentTargetKind.Unit, enemy.Id), enemy.HexId, Format("play_military_target_unit", name, enemy.Id));
            }
            foreach (CityState city in world.Cities.Values.OrderBy(city => city.Id))
                if (city.HexId is int hex) AddLocation(city.Id, hex, city.ForceId, city.Name);
            foreach (GateState gate in world.Gates.Values.OrderBy(gate => gate.Id))
                if (gate.HexId is int hex) AddLocation(gate.Id, hex, gate.ForceId, gate.Name);
            void AddLocation(int id, int hex, int? owner, string name)
            {
                if (owner == force) _enterTarget.AddItem(name, id);
                else if (PlayableDiplomacy.IsHostile(world, force, owner))
                    AddAttack(new(DeploymentTargetKind.Location, id), hex, Format("play_military_target_location", name, id));
            }
        }
        int attackIndex = _attackTargets.FindIndex(item => item.Target == priorAttack);
        if (attackIndex >= 0) _attackTarget.Select(attackIndex);
        int enterIndex = _enterTarget.GetItemIndex(priorEnter);
        if (enterIndex >= 0) _enterTarget.Select(enterIndex);
        SelectMapTargets();

        void AddAttack(DeploymentTarget target, int hex, string name)
        {
            _attackTargets.Add((target, hex));
            _attackTarget.AddItem(name, target.Id);
            _attackTarget.SetItemMetadata(_attackTarget.ItemCount - 1, target.Kind.ToString());
        }
    }

    private void SelectMapTargets()
    {
        if (_selectedHex is not int hex || _world is null) return;
        int attack = _attackTargets.FindIndex(item => item.HexId == hex);
        if (attack >= 0) _attackTarget.Select(attack);
        int? enter = _world.Cities.Values.FirstOrDefault(city => city.HexId == hex && city.ForceId == _forceId)?.Id ??
            _world.Gates.Values.FirstOrDefault(gate => gate.HexId == hex && gate.ForceId == _forceId)?.Id;
        if (enter is int id && _enterTarget.GetItemIndex(id) is int index && index >= 0) _enterTarget.Select(index);
    }

    private void RenderActionStatus()
    {
        if (_text is null) return;
        _unitSummary.Text = _boundUnit is { } unit ? Format("play_military_unit_summary", unit.Id,
            _world?.Officers.GetValueOrDefault(unit.OfficerId)?.Name ?? unit.OfficerId.ToString(), unit.Troops,
            unit.NativeMorale1A is byte nativeMorale ? nativeMorale : unit.Morale,
            unit.FormationId is int formation ? _world?.Formations.GetValueOrDefault(formation)?.Name ?? _text("detail_unknown") : _text("detail_unknown")) : "";
        if (_boundUnit?.MilitaryPlan is not { } plan)
        { _actionStatus.Text = _text("play_military_no_action"); return; }
        string target = plan.Action.Target.Kind switch
        {
            DeploymentTargetKind.Unit => _world?.Units.TryGetValue(plan.Action.Target.Id, out UnitState? targetUnit) == true &&
                _world.Officers.TryGetValue(targetUnit.OfficerId, out OfficerState? officer) ? officer.Name : plan.Action.Target.Id.ToString(),
            DeploymentTargetKind.Location => _world?.Cities.GetValueOrDefault(plan.Action.Target.Id)?.Name ??
                _world?.Gates.GetValueOrDefault(plan.Action.Target.Id)?.Name ?? plan.Action.Target.Id.ToString(),
            _ => plan.Action.Target.Id.ToString()
        };
        _actionStatus.Text = Format(plan.Completed ? "play_military_action_complete" : "play_military_action_current",
            _text("play_military_action_" + plan.Action.ActionKey), target);
    }

    private string Format(string key, params object?[] args) => _text is null ? "" :
        string.Format(CultureInfo.InvariantCulture, _text(key), args);

    private void RenderWaypoints()
    {
        _waypoints.Clear();
        for (int index = 0; index < _draft.Count; index++)
            _waypoints.AddItem(_text is null ? $"{index + 1}: {_draft[index]}" :
                string.Format(CultureInfo.InvariantCulture, _text("play_military_waypoint"), index + 1, _draft[index]));
        _waypointTitle.Text = _text is null ? "" : string.Format(CultureInfo.InvariantCulture,
            _text("play_military_waypoints"), _draft.Count, PlayableWaypointMarch.MaximumWaypoints);
        SetMeta("military_draft_waypoints", _draft.Count);
        UpdateEnabled();
    }

    private void UpdateEnabled()
    {
        int[] units = CommandUnitIdsSnapshot();
        bool combatUnit = units.Length > 0;
        _attackTarget.Disabled = !_strategy || !combatUnit || _attackTarget.ItemCount == 0;
        _enterTarget.Disabled = !_strategy || !combatUnit || _enterTarget.ItemCount == 0;
        _attack.Disabled = _attackTarget.Disabled || _attackTarget.Selected < 0;
        _enter.Disabled = _enterTarget.Disabled || _enterTarget.Selected < 0;
        _stay.Disabled = !_strategy || !combatUnit || units.Length > 1 && _selectedHex is null;
        _nearby.Disabled = !_strategy || _boundUnit?.Mission?.Kind == UnitMissionKind.Transport;
        _retreat.Disabled = _nearby.Disabled;
        _pursuit.Disabled = _nearby.Disabled;
        _after.Disabled = _nearby.Disabled;
        _add.Disabled = !_strategy || _selectedHex is not int hex || hex < 0 || hex >= _mapSize ||
            _draft.Count >= PlayableWaypointMarch.MaximumWaypoints;
        _remove.Disabled = !_strategy || _waypoints.GetSelectedItems().Length == 0;
        _clear.Disabled = !_strategy || _draft.Count == 0;
        _applyRoute.Disabled = !_strategy || !combatUnit || units.Any(id =>
            _world?.Units.GetValueOrDefault(id)?.MilitaryPlan is not { Completed: false });
        _applyBehavior.Disabled = !_strategy || !combatUnit;
        UpdateBatchSelectionEnabled();
        UpdateWithdrawalEnabled();
    }
}
