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
    private readonly Label _status = new() { Name = "DiplomacyStatus", AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Label _pending = new() { Name = "DiplomacyPending", AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Button _goodwill = new() { Name = "DiplomacyGoodwill" };
    private readonly Button _alliance = new() { Name = "DiplomacyAlliance" };
    private readonly Button _cancel = new() { Name = "DiplomacyCancelAlliance" };
    private readonly Button _request = new() { Name = "DiplomacyRequestAttack" };
    private Func<string, string>? _text;
    private Action<GameOrder>? _submit;
    private DiplomacyCommandView _view = DiplomacyCommandView.Empty;

    public DiplomacyOrdersPanel()
    {
        Name = "DiplomacyOrdersPanel";
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddChild(_title);
        AddRow(_executorLabel, _executor);
        AddRow(_forceLabel, _force);
        AddRow(_monthsLabel, _months);
        AddRow(_targetLabel, _target);
        var actions = new HFlowContainer();
        actions.AddChild(_goodwill); actions.AddChild(_alliance);
        actions.AddChild(_cancel); actions.AddChild(_request);
        AddChild(actions);
        AddChild(_status); AddChild(_pending);
        _goodwill.Pressed += () => Submit(DiplomaticMissionKind.Goodwill);
        _alliance.Pressed += () => Submit(DiplomaticMissionKind.Alliance);
        _cancel.Pressed += () => Submit(DiplomaticMissionKind.CancelAlliance);
        _request.Pressed += () => Submit(DiplomaticMissionKind.RequestAttack);
        _force.ItemSelected += _ => RefreshStatus();
        _executor.ItemSelected += _ => RefreshStatus();
        RefreshStatus();
    }

    public void Configure(Func<string, string> text, Action<GameOrder> submit)
    {
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _submit = submit ?? throw new ArgumentNullException(nameof(submit));
        _title.Text = _text("play_diplomacy_title");
        _executorLabel.Text = _text("play_diplomacy_executor");
        _forceLabel.Text = _text("play_diplomacy_target_force");
        _monthsLabel.Text = _text("play_diplomacy_months");
        _targetLabel.Text = _text("play_diplomacy_attack_city");
        _goodwill.Text = _text("play_diplomacy_goodwill");
        _alliance.Text = _text("play_diplomacy_alliance");
        _cancel.Text = _text("play_diplomacy_cancel_alliance");
        _request.Text = _text("play_diplomacy_request_attack");
        RefreshStatus();
    }

    public void Refresh(GameState? world, int? playerForceId, int? sourceCityId)
    {
        _view = DiplomacyCommandView.Create(world, playerForceId, sourceCityId);
        int oldExecutor = Selected(_executor), oldForce = Selected(_force), oldTarget = Selected(_target);
        _executor.Clear(); _force.Clear(); _target.Clear();
        foreach (NamedCommandChoice officer in _view.Executors) _executor.AddItem(officer.Name, officer.Id);
        foreach (DiplomaticForceChoice force in _view.Forces) _force.AddItem(force.Name, force.Id);
        foreach (NamedCommandChoice target in _view.TargetCities) _target.AddItem(target.Name, target.Id);
        Restore(_executor, oldExecutor); Restore(_force, oldForce); Restore(_target, oldTarget);
        RefreshStatus();
    }

    private void Submit(DiplomaticMissionKind kind)
    {
        if (!_view.InStrategyPhase || _view.PlayerForceId is not int player || _view.SourceCityId is not int city ||
            _executor.Selected < 0 || _force.Selected < 0 || kind == DiplomaticMissionKind.RequestAttack && _target.Selected < 0) return;
        _submit?.Invoke(new PrepareDiplomaticOrder(player, city, _executor.GetSelectedId(), _force.GetSelectedId(),
            kind, (int)_months.Value, kind == DiplomaticMissionKind.RequestAttack ? _target.GetSelectedId() : null));
    }

    private void RefreshStatus()
    {
        bool ready = _view.InStrategyPhase && _view.DiplomacyKnown && _view.PlayerForceId is not null && _view.SourceCityId is not null &&
            _force.Selected >= 0 && _executor.Selected >= 0;
        _goodwill.Disabled = !ready; _alliance.Disabled = !ready;
        _cancel.Disabled = !ready; _request.Disabled = !ready || _target.Selected < 0;
        _executor.Disabled = !ready && _executor.ItemCount == 0 || !_view.InStrategyPhase;
        _force.Disabled = !ready && _force.ItemCount == 0 || !_view.InStrategyPhase;
        _target.Disabled = !_view.InStrategyPhase || _target.ItemCount == 0;
        _months.Editable = _view.InStrategyPhase;
        if (_text is null) return;
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
    }

    private string Format(string key, params object[] values) => string.Format(CultureInfo.InvariantCulture, _text!(key), values);
    private static int Selected(OptionButton button) => button.Selected < 0 ? -1 : button.GetSelectedId();
    private static void Restore(OptionButton button, int id)
    {
        if (button.ItemCount == 0) return;
        int index = button.GetItemIndex(id); button.Select(index < 0 ? 0 : index);
    }
    private void AddRow(Control label, Control field)
    {
        var row = new HFlowContainer(); row.AddChild(label); row.AddChild(field); AddChild(row);
    }
}
