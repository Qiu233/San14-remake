using Godot;
using San14.Core;
using San14.Client.Gameplay;
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
    private MilitaryCommandView _view = MilitaryCommandView.Empty;
    private long _bindingRevision;
    private int? _forceId;
    private int? _unitId;
    private int? _selectedHex;
    private int _mapSize;
    private bool _strategy;
    private Action<GameOrder>? _issue;

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
                ? _view.CombatUnit(units[0])?.HexId : null);
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

    public void Refresh(MilitaryCommandView view, int? selectedHexId = null)
    {
        _view = view;
        _forceId = view.ForceId;
        _unitId = null;
        _selectedHex = selectedHexId;
        _mapSize = view.MapSize;
        _strategy = view.CanIssue;
        MilitaryUnitView? unit = view.FocusedUnit;
        if (unit?.HasCombatMission == true) _unitId = unit.Id;
        RefreshBatchSelection();
        SetMeta("military_selected_unit_id", _unitId ?? -1);
        SetMeta("military_plan_waypoints", unit?.Plan?.Waypoints.Count ?? 0);
        SetMeta("military_plan_waypoint_cursor", unit?.Plan?.NextWaypointIndex ?? 0);
        SetMeta("military_plan_complete", unit?.Plan?.Completed ?? false);
        SetMeta("military_plan_action", unit?.Plan?.Action.ActionKey ?? "");
        SetMeta("military_plan_target_id", unit?.Plan?.Action.Target.Id ?? -1);
        SetMeta("military_plan_target_kind", unit?.Plan?.Action.Target.Kind.ToString() ?? "");
        if (_bindingRevision != view.BindingRevision)
        {
            _draft.Clear();
            if (unit?.Plan is { Completed: false } plan)
                _draft.AddRange(plan.Waypoints.Skip(plan.NextWaypointIndex));
            DeploymentBehavior selected = unit?.Behavior ?? new(true, true, false, DeploymentAfterAction.Stay);
            _nearby.ButtonPressed = selected.AttackNearbyEnemies;
            _retreat.ButtonPressed = selected.AllowRetreat;
            _pursuit.ButtonPressed = selected.PursueRetreatingEnemies;
            if (_after.ItemCount > 0) _after.Select(_after.GetItemIndex((int)selected.AfterAction));
            _bindingRevision = view.BindingRevision;
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
        foreach (MilitaryAttackChoice target in _view.AttackTargets)
        {
            string label = Format(target.Target.Kind == DeploymentTargetKind.Unit
                ? "play_military_target_unit" : "play_military_target_location", target.Name, target.Target.Id);
            AddAttack(target.Target, target.HexId, label);
        }
        foreach (MilitaryLocationChoice location in _view.EntryTargets) _enterTarget.AddItem(location.Name, location.Id);
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
        if (_selectedHex is not int hex) return;
        int attack = _attackTargets.FindIndex(item => item.HexId == hex);
        if (attack >= 0) _attackTarget.Select(attack);
        int? enter = _view.EntryAtHex(hex);
        if (enter is int id && _enterTarget.GetItemIndex(id) is int index && index >= 0) _enterTarget.Select(index);
    }

    private void RenderActionStatus()
    {
        if (_text is null) return;
        _unitSummary.Text = _view.FocusedUnit is { } unit ? Format("play_military_unit_summary", unit.Id,
            unit.OfficerName, unit.Troops, unit.Morale, unit.FormationName ?? _text("detail_unknown")) : "";
        if (_view.FocusedUnit?.Plan is not { } plan)
        { _actionStatus.Text = _text("play_military_no_action"); return; }
        _actionStatus.Text = Format(plan.Completed ? "play_military_action_complete" : "play_military_action_current",
            _text("play_military_action_" + plan.Action.ActionKey), plan.TargetName);
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
        _nearby.Disabled = !_strategy || _view.FocusedUnit?.IsTransport == true;
        _retreat.Disabled = _nearby.Disabled;
        _pursuit.Disabled = _nearby.Disabled;
        _after.Disabled = _nearby.Disabled;
        _add.Disabled = !_strategy || _selectedHex is not int hex || hex < 0 || hex >= _mapSize ||
            _draft.Count >= PlayableWaypointMarch.MaximumWaypoints;
        _remove.Disabled = !_strategy || _waypoints.GetSelectedItems().Length == 0;
        _clear.Disabled = !_strategy || _draft.Count == 0;
        _applyRoute.Disabled = !_strategy || !combatUnit || units.Any(id =>
            _view.CombatUnit(id)?.HasUnfinishedPlan != true);
        _applyBehavior.Disabled = !_strategy || !combatUnit;
        UpdateBatchSelectionEnabled();
        UpdateWithdrawalEnabled();
    }
}
