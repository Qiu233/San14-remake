using Godot;
using San14.Core;
using San14.Client.Gameplay;
using System.Globalization;

/// <summary>Diplomatic selections and current state; command eligibility is decided by the shared core.</summary>
public partial class DiplomacyOrdersPanel : VBoxContainer
{
    private readonly Label _title = new();
    private readonly Label _executorLabel = new();
    private readonly OptionButton _executor = new() { Name = "DiplomacyExecutor" };
    private readonly Label _forceLabel = new();
    private readonly OptionButton _force = new() { Name = "DiplomacyTargetForce" };
    private readonly Label _monthsLabel = new();
    private readonly SpinBox _months = new() { Name = "DiplomacyAllianceMonths", MinValue = 1,
        MaxValue = int.MaxValue / 3, Step = 1, Value = 12, CustomMinimumSize = new(110, 0) };
    private readonly Label _targetLabel = new();
    private readonly OptionButton _target = new() { Name = "DiplomacyAttackCity" };
    private readonly Label _giftGoldLabel = new();
    private readonly SpinBox _giftGold = new() { Name = "DiplomacyGiftGold", MinValue = 0,
        MaxValue = int.MaxValue, Step = 1, CustomMinimumSize = new(150,0) };
    private readonly Label _giftSuppliesLabel = new();
    private readonly SpinBox _giftSupplies = new() { Name = "DiplomacyGiftSupplies", MinValue = 0,
        MaxValue = int.MaxValue, Step = 1, CustomMinimumSize = new(150,0) };
    private readonly Label _status = new() { Name = "DiplomacyStatus", AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Label _pending = new() { Name = "DiplomacyPending", AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Label _commandStatus = new() { Name = "DiplomacyCommandAvailability", AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Button _goodwill = new() { Name = "DiplomacyGoodwill" };
    private readonly Button _alliance = new() { Name = "DiplomacyAlliance" };
    private readonly Button _cancel = new() { Name = "DiplomacyCancelAlliance" };
    private readonly Button _request = new() { Name = "DiplomacyRequestAttack" };
    private readonly Button _officerDetails = new() { Name = "DiplomacyOfficerDetails" };
    private readonly Button _forceDetails = new() { Name = "DiplomacyForceDetails" };
    private readonly Button _cityDetails = new() { Name = "DiplomacyCityDetails" };
    private Action<int>? _inspectOfficer, _inspectForce, _inspectCity;
    private Func<string, string>? _text;
    private Action<GameOrder>? _submit;
    private Func<DiplomaticMissionKind,int?,int,int?,int,int,DiplomacyCommandAvailability>? _query;
    private GameState? _world;
    private int? _playerForceId, _sourceCityId;
    private bool _refreshing;
    private readonly Dictionary<DiplomaticMissionKind,DiplomacyCommandAvailability> _availability = [];
    private static readonly DiplomaticMissionKind[] Kinds = [DiplomaticMissionKind.Goodwill,
        DiplomaticMissionKind.Alliance,DiplomaticMissionKind.CancelAlliance,DiplomaticMissionKind.RequestAttack];
    private DiplomacyCommandView _view = DiplomacyCommandView.Empty;

    public DiplomacyOrdersPanel()
    {
        Name = "DiplomacyOrdersPanel";
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddChild(_title);
        AddRow(_executorLabel, _executor, _officerDetails);
        AddRow(_forceLabel, _force, _forceDetails);
        AddRow(_monthsLabel, _months);
        AddRow(_targetLabel, _target, _cityDetails);
        AddRow(_giftGoldLabel,_giftGold);
        AddRow(_giftSuppliesLabel,_giftSupplies);
        _officerDetails.Pressed += () => { if (_executor.Selected >= 0) _inspectOfficer?.Invoke(_executor.GetSelectedId()); };
        _forceDetails.Pressed += () => { if (_force.Selected >= 0) _inspectForce?.Invoke(_force.GetSelectedId()); };
        _cityDetails.Pressed += () => { if (_target.Selected >= 0) _inspectCity?.Invoke(_target.GetSelectedId()); };
        var actions = new HFlowContainer();
        actions.AddChild(_goodwill); actions.AddChild(_alliance);
        actions.AddChild(_cancel); actions.AddChild(_request);
        AddChild(actions);
        AddChild(_status); AddChild(_pending); AddChild(_commandStatus);
        _goodwill.Pressed += () => Submit(DiplomaticMissionKind.Goodwill);
        _alliance.Pressed += () => Submit(DiplomaticMissionKind.Alliance);
        _cancel.Pressed += () => Submit(DiplomaticMissionKind.CancelAlliance);
        _request.Pressed += () => Submit(DiplomaticMissionKind.RequestAttack);
        _force.ItemSelected += _ => RefreshAvailability();
        _executor.ItemSelected += _ => RefreshStatus();
        _target.ItemSelected += _ => RefreshAvailability();
        _months.ValueChanged += _ => RefreshAvailability();
        _giftGold.ValueChanged += _ => RefreshAvailability();
        _giftSupplies.ValueChanged += _ => RefreshAvailability();
        RefreshStatus();
    }

    public void Configure(Func<string, string> text, Action<GameOrder> submit,
        Action<int>? inspectOfficer = null, Action<int>? inspectForce = null, Action<int>? inspectCity = null,
        Func<DiplomaticMissionKind,int?,int,int?,int,int,DiplomacyCommandAvailability>? query = null)
    {
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _submit = submit ?? throw new ArgumentNullException(nameof(submit));
        _query = query;
        _inspectOfficer = inspectOfficer; _inspectForce = inspectForce; _inspectCity = inspectCity;
        _officerDetails.Text = text("detail_officer_open");
        _forceDetails.Text = text("play_force_information"); _cityDetails.Text = text("play_city_information_item");
        _title.Text = _text("play_diplomacy_title");
        _executorLabel.Text = _text("play_diplomacy_executor");
        _forceLabel.Text = _text("play_diplomacy_target_force");
        _monthsLabel.Text = _text("play_diplomacy_months");
        _targetLabel.Text = _text("play_diplomacy_attack_city");
        _giftGoldLabel.Text = _text("play_diplomacy_gift_gold");
        _giftSuppliesLabel.Text = _text("play_diplomacy_gift_supplies");
        _goodwill.Text = _text("play_diplomacy_goodwill");
        _alliance.Text = _text("play_diplomacy_alliance");
        _cancel.Text = _text("play_diplomacy_cancel_alliance");
        _request.Text = _text("play_diplomacy_request_attack");
        RefreshAvailability();
    }

    public void Refresh(GameState? world, int? playerForceId, int? sourceCityId)
    {
        _world = world; _playerForceId = playerForceId; _sourceCityId = sourceCityId;
        RefreshAvailability();
    }

    private void RefreshAvailability()
    {
        if (_refreshing) return;
        _refreshing = true;
        try
        {
        int oldExecutor = Selected(_executor), oldForce = Selected(_force), oldTarget = Selected(_target);
        ReadAvailability();
        _view = DiplomacyCommandView.Create(_world,_playerForceId,_sourceCityId,
            Kinds.SelectMany(kind => _availability[kind].ExecutorOfficerIds).Distinct().ToArray(),
            Kinds.SelectMany(kind => _availability[kind].TargetForceIds).Distinct().ToArray());
        _executor.Clear(); _force.Clear(); _target.Clear();
        foreach (NamedCommandChoice officer in _view.Executors) _executor.AddItem(officer.Name, officer.Id);
        foreach (DiplomaticForceChoice force in _view.Forces) _force.AddItem(force.Name, force.Id);
        foreach (NamedCommandChoice target in _view.TargetCities) _target.AddItem(target.Name, target.Id);
        Restore(_executor, oldExecutor); Restore(_force, oldForce); Restore(_target, oldTarget);
        // Initial list population can select a real target for the first time.
        // Quote that actual input, rather than enabling from a target-less query.
        if (oldForce != Selected(_force) || oldTarget != Selected(_target)) ReadAvailability();
        RefreshStatus();
        }
        finally { _refreshing = false; }
    }

    private void ReadAvailability()
    { foreach (var kind in Kinds) _availability[kind] = Query(kind); }

    private DiplomacyCommandAvailability Query(DiplomaticMissionKind kind)
    {
        if (_world is not { Phase: TurnPhase.Strategy } || _playerForceId is null || _sourceCityId is null || _query is null)
            return new([],[],null,"diplomacy.command.current_query",null);
        int force = Selected(_force), target = Selected(_target);
        return _query(kind,force >= 0 ? force : null,(int)_months.Value,
            kind == DiplomaticMissionKind.RequestAttack && target >= 0 ? target : null,
            (int)_giftGold.Value,(int)_giftSupplies.Value);
    }

    private void Submit(DiplomaticMissionKind kind)
    {
        if (!_view.InStrategyPhase || _view.PlayerForceId is not int player || _view.SourceCityId is not int city ||
            _executor.Selected < 0 || _force.Selected < 0 || kind == DiplomaticMissionKind.RequestAttack && _target.Selected < 0) return;
        var current = Query(kind); _availability[kind] = current; RefreshStatus();
        if (!Allowed(current)) return;
        _submit?.Invoke(new PrepareDiplomaticOrder(player, city, _executor.GetSelectedId(), _force.GetSelectedId(),
            kind, (int)_months.Value, kind == DiplomaticMissionKind.RequestAttack ? _target.GetSelectedId() : null)
            { GiftGold = (int)_giftGold.Value, GiftSupplies = (int)_giftSupplies.Value });
    }

    private bool Allowed(DiplomacyCommandAvailability value) => value.CanSubmit == true &&
        value.ExecutorOfficerIds.Contains(Selected(_executor)) && value.TargetForceIds.Contains(Selected(_force));

    private void RefreshStatus()
    {
        _officerDetails.Disabled = _inspectOfficer is null || _executor.Selected < 0;
        _forceDetails.Disabled = _inspectForce is null || _force.Selected < 0;
        _cityDetails.Disabled = _inspectCity is null || _target.Selected < 0;
        bool ready = _view.InStrategyPhase && _view.PlayerForceId is not null && _view.SourceCityId is not null;
        ApplyAvailability(_goodwill,DiplomaticMissionKind.Goodwill);
        ApplyAvailability(_alliance,DiplomaticMissionKind.Alliance);
        ApplyAvailability(_cancel,DiplomaticMissionKind.CancelAlliance);
        ApplyAvailability(_request,DiplomaticMissionKind.RequestAttack);
        _executor.Disabled = !ready || _executor.ItemCount == 0;
        _force.Disabled = !ready || _force.ItemCount == 0;
        _target.Disabled = !_view.InStrategyPhase || _target.ItemCount == 0;
        _months.Editable = ready;
        _giftGold.Editable = ready; _giftSupplies.Editable = ready;
        if (_text is null) return;
        _commandStatus.Text = string.Join("\n",Kinds.Where(kind => _availability.ContainsKey(kind)).Select(kind =>
            Format("play_diplomacy_command_books",KindCaption(kind),
                _availability[kind].CommandBooks?.ToString(CultureInfo.InvariantCulture) ?? _text("detail_unknown"))));
        if (_availability.Values.Any(value => value.CanSubmit is null))
            _commandStatus.Text += "\n" + _text("play_diplomacy_rule_unavailable");
        if (!_view.DiplomacyKnown || _view.PlayerForceId is null)
        {
            _status.Text = _text("play_diplomacy_unknown");
            _pending.Text = "";
            return;
        }
        int target = Selected(_force);
        DiplomaticForceChoice? selected = _view.FindForce(target);
        string relationship = selected?.Relation is DiplomaticRelation relation
            ? _text("play_diplomacy_relation_" + relation.ToString().ToLowerInvariant()) : _text("detail_unknown");
        string treaty = _text("play_diplomacy_no_treaty");
        if (selected?.TreatyKind is DiplomaticTreatyKind kind)
            treaty = Format(kind == DiplomaticTreatyKind.Alliance ? "play_diplomacy_treaty_alliance" : "play_diplomacy_treaty_truce",
                selected.TreatyRemainingPeriods!.Value);
        _status.Text = Format("play_diplomacy_relation_status", relationship, treaty);
        _pending.Text = Format("play_diplomacy_pending", _view.PendingMissions, _view.AttackRequests);
        SetMeta("diplomacy_pending_missions", _view.PendingMissions);
        SetMeta("diplomacy_selected_target_force", target);

        void ApplyAvailability(Button button,DiplomaticMissionKind kind)
        {
            bool known = _availability.TryGetValue(kind,out var value);
            button.Disabled = !ready || !known || !Allowed(value!) ||
                kind == DiplomaticMissionKind.RequestAttack && _target.Selected < 0;
            button.TooltipText = value?.MissingRuleKey is not null && _text is not null
                ? _text("play_diplomacy_rule_unavailable") : "";
            button.SetMeta("diplomacy_missing_rule",value?.MissingRuleKey ?? "");
        }
    }

    private string KindCaption(DiplomaticMissionKind kind) => _text!(kind switch
    {
        DiplomaticMissionKind.Goodwill => "play_diplomacy_goodwill",
        DiplomaticMissionKind.Alliance => "play_diplomacy_alliance",
        DiplomaticMissionKind.CancelAlliance => "play_diplomacy_cancel_alliance",
        DiplomaticMissionKind.RequestAttack => "play_diplomacy_request_attack",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    });

    private string Format(string key, params object[] values) => string.Format(CultureInfo.InvariantCulture, _text!(key), values);
    private static int Selected(OptionButton button) => button.Selected < 0 ? -1 : button.GetSelectedId();
    private static void Restore(OptionButton button, int id)
    {
        if (button.ItemCount == 0) return;
        int index = button.GetItemIndex(id); button.Select(index < 0 ? 0 : index);
    }
    private void AddRow(params Control[] controls)
    {
        var row = new HFlowContainer(); foreach (var control in controls) row.AddChild(control); AddChild(row);
    }
}
