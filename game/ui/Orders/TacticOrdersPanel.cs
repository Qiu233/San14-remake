using Godot;
using San14.Core;
using System.Globalization;

/// <summary>Equipped source tactic identities and queue controls; every decision uses the shared tactic rules.</summary>
public partial class TacticOrdersPanel : VBoxContainer
{
    private readonly Label _title = new();
    private readonly Label _tacticLabel = new();
    private readonly OptionButton _tactic = new() { Name = "ActiveTactic" };
    private readonly Label _targetLabel = new();
    private readonly OptionButton _target = new() { Name = "TacticTargetUnit" };
    private readonly Label _availability = new() { Name = "TacticAvailability", AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Label _restriction = new() { Name = "TacticRestriction", AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Label _queue = new() { Name = "TacticQueueStatus", AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Button _use = new() { Name = "TacticUse" };
    private readonly Button _cancel = new() { Name = "TacticCancel" };
    private Func<string, string>? _text;
    private Func<string?, string>? _reason;
    private Action<GameOrder>? _submit;
    private GameState? _world;
    private UnitState? _unit;
    private int? _player;
    private int? _selectedHex;
    private IPlayableTacticRules _profiles = UnresolvedTacticRules.Instance;
    private PlayableLandRoutes? _routes;
    private TacticCommandAvailability[] _choices = [];

    public TacticOrdersPanel()
    {
        Name = "TacticOrdersPanel";
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 6);
        AddChild(_title);
        var choices = new HFlowContainer();
        choices.AddChild(_tacticLabel); choices.AddChild(_tactic); choices.AddChild(_targetLabel); choices.AddChild(_target);
        AddChild(choices); AddChild(_availability); AddChild(_restriction); AddChild(_queue);
        var actions = new HFlowContainer(); actions.AddChild(_use); actions.AddChild(_cancel); AddChild(actions);
        _tactic.ItemSelected += _ => { RefreshTargets(); RefreshStatus(); };
        _target.ItemSelected += _ => RefreshStatus();
        _use.Pressed += () =>
        {
            if (!_use.Disabled && _player is int force && _unit is { } unit && _tactic.Selected >= 0 && _target.Selected >= 0)
                _submit?.Invoke(new UseTacticOrder(force, unit.Id, _tactic.GetSelectedId(), _target.GetSelectedId()));
        };
        _cancel.Pressed += () =>
        {
            if (!_cancel.Disabled && _player is int force && _unit is { } unit)
                _submit?.Invoke(new CancelTacticUseOrder(force, unit.Id));
        };
        RefreshStatus();
    }

    public void Configure(Func<string, string> text, Func<string?, string> reason, Action<GameOrder> submit)
    {
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _reason = reason ?? throw new ArgumentNullException(nameof(reason));
        _submit = submit ?? throw new ArgumentNullException(nameof(submit));
        _title.Text = text("play_tactic_active_title"); _tacticLabel.Text = text("play_tactic_choice");
        _targetLabel.Text = text("play_tactic_target"); _use.Text = text("play_tactic_use"); _cancel.Text = text("play_tactic_cancel");
        RefreshStatus();
    }

    public void Refresh(GameState? world, int? unitId, int? playerForceId, IPlayableTacticRules profiles,
        PlayableLandRoutes? routes, int? selectedHex = null)
    {
        _world = world; _player = playerForceId; _profiles = profiles; _routes = routes; _selectedHex = selectedHex;
        _unit = world is not null && unitId is int id && world.Units.TryGetValue(id, out UnitState? unit) && unit.ForceId == playerForceId
            ? unit : null;
        int prior = _tactic.Selected >= 0 ? _tactic.GetSelectedId() : -1;
        _choices = world is not null && _unit is not null ? PlayableTacticAvailability.ForUnit(world, _unit, profiles) : [];
        _tactic.Clear();
        foreach (TacticCommandAvailability choice in _choices) _tactic.AddItem(choice.Name, choice.TacticId);
        int chosen = _tactic.GetItemIndex(_unit?.TacticRuntime?.Pending?.TacticId ?? prior);
        if (chosen >= 0) _tactic.Select(chosen);
        SetMeta("tactic_equipped_count", _choices.Length);
        SetMeta("tactic_unresolved_count", _choices.Count(choice => choice.Kind == TacticCommandAvailabilityKind.Unresolved));
        SetMeta("tactic_ready_count", _choices.Count(choice => choice.Kind == TacticCommandAvailabilityKind.Ready));
        SetMeta("tactic_unit_id", _unit?.Id ?? -1);
        RefreshTargets(); RefreshStatus();
    }

    private TacticCommandAvailability? SelectedTactic() => _tactic.Selected >= 0
        ? _choices.FirstOrDefault(choice => choice.TacticId == _tactic.GetSelectedId()) : null;

    private void RefreshTargets()
    {
        int prior = _target.Selected >= 0 ? _target.GetSelectedId() : -1;
        _target.Clear();
        if (_world is not { } world || _unit is not { } caster || SelectedTactic()?.TargetRelation is not { } relation) return;
        foreach (UnitState target in world.Units.Values.Where(unit => relation switch
                 {
                     PlayableTacticTargetRelation.Self => unit.Id == caster.Id,
                     PlayableTacticTargetRelation.Friendly => unit.ForceId == caster.ForceId || PlayableDiplomacy.AreAllied(world, caster.ForceId, unit.ForceId),
                     PlayableTacticTargetRelation.Hostile => PlayableDiplomacy.IsHostile(world, caster.ForceId, unit.ForceId),
                     _ => false
                 }).OrderBy(unit => unit.Id))
        {
            string name = world.Officers.TryGetValue(target.OfficerId, out OfficerState? officer) ? officer.Name : target.OfficerId.ToString();
            _target.AddItem(Format("play_tactic_target_item", name, target.Id), target.Id);
        }
        int pending = _unit.TacticRuntime?.Pending?.TargetUnitId ?? prior;
        int index = _target.GetItemIndex(pending);
        if (index >= 0) _target.Select(index);
        else if (_selectedHex is int hex && world.Units.Values.FirstOrDefault(unit => unit.HexId == hex && _target.GetItemIndex(unit.Id) >= 0) is { } picked)
            _target.Select(_target.GetItemIndex(picked.Id));
    }

    private void RefreshStatus()
    {
        bool strategy = _world is { Phase: TurnPhase.Strategy } world && _player is int force &&
            world.Forces.TryGetValue(force, out ForceState? owner) && owner.IsActive &&
            PlayableOutcome.Evaluate(world, force) == PlayableResult.Ongoing && _unit is not null;
        TacticCommandAvailability? choice = SelectedTactic();
        TacticUseEligibility eligibility = _world is not null && _unit is not null && choice is not null &&
            _target.Selected >= 0 && _routes is not null
            ? PlayableTacticAvailability.ForTarget(_world, _unit, choice.TacticId, _target.GetSelectedId(), _profiles, _routes)
            : new(null, choice?.Reason ?? "tactic.target_missing");
        _tactic.Disabled = !strategy || _choices.Length == 0;
        _target.Disabled = !strategy || _target.ItemCount == 0;
        _use.Disabled = !strategy || choice?.Kind != TacticCommandAvailabilityKind.Ready || eligibility.Allowed != true;
        _cancel.Disabled = !strategy || _unit?.TacticRuntime?.Pending is null;
        SetMeta("tactic_selected_id", choice?.TacticId ?? -1);
        SetMeta("tactic_availability", choice?.Kind.ToString() ?? "None");
        SetMeta("tactic_pending_id", _unit?.TacticRuntime?.Pending?.TacticId ?? -1);
        SetMeta("tactic_pending_target_id", _unit?.TacticRuntime?.Pending?.TargetUnitId ?? -1);
        SetMeta("tactic_target_allowed", eligibility.Allowed == true);
        SetMeta("tactic_target_rule_key", eligibility.MissingRuleKey ?? eligibility.Reason ?? "");
        if (_text is null || _reason is null) return;
        _availability.Text = choice is null ? _text("play_tactic_no_equipped") :
            Format("play_tactic_status", choice.Name, choice.TacticId, _text("play_tactic_state_" + choice.Kind.ToString().ToLowerInvariant()));
        if (choice?.ReadyAtPeriod is int deadline) _availability.Text += "  " + Format("play_tactic_cooldown_until", deadline);
        if (choice?.TargetRelation is { } relation && choice.RangeHexes is int range && choice.AreaRadiusHexes is int area)
            _availability.Text += "  " + Format("play_tactic_profile", _text("play_tactic_relation_" + relation.ToString().ToLowerInvariant()), range, area);
        string? restriction = choice?.Kind == TacticCommandAvailabilityKind.Ready
            ? eligibility.MissingRuleKey ?? eligibility.Reason : choice?.Reason;
        _use.TooltipText = _use.Disabled && restriction is not null ? _reason(restriction) : "";
        SetMeta("tactic_restriction_key", restriction ?? "");
        _restriction.Visible = choice is not null && restriction is not null;
        _restriction.Text = restriction is null ? "" : Format("play_tactic_restriction", _reason(restriction));
        _queue.Text = _unit?.TacticRuntime?.Pending is { } pending
            ? Format("play_tactic_queued", _world?.Tactics.GetValueOrDefault(pending.TacticId)?.Name ?? pending.TacticId.ToString(), pending.TargetUnitId)
            : _text("play_tactic_queue_empty");
    }

    private string Format(string key, params object?[] args) => _text is null ? "" :
        string.Format(CultureInfo.InvariantCulture, _text(key), args);
}
