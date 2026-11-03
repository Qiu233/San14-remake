using Godot;
using San14.Core;
using San14.Client.Gameplay;
using System.Globalization;

/// <summary>Local composition only. A single prepared batch reaches GameEngine
/// after confirmation; no simulation state or resource is changed while editing.</summary>
public partial class MilitaryDeploymentBatchPanel : VBoxContainer
{
    private readonly DeploymentCompositionDraft _draft = new();
    private readonly Label _title = new();
    private readonly Label _hint = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Label _summary = new() { Name = "DeploymentBatchSummary", AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly OptionButton _action = new() { Name = "DeploymentBatchAction" };
    private readonly OptionButton _target = new() { Name = "DeploymentBatchTarget" };
    private readonly ItemList _units = new() { Name = "DeploymentBatchUnits", CustomMinimumSize = new(0, 140) };
    private readonly Button _add = new() { Name = "DeploymentBatchAdd" };
    private readonly Button _remove = new() { Name = "DeploymentBatchRemove" };
    private readonly Button _clear = new() { Name = "DeploymentBatchClear" };
    private readonly Button _submit = new() { Name = "DeploymentBatchSubmit" };
    private readonly List<(DeploymentTarget Target, int HexId, string Name)> _targets = [];
    private static readonly string[] ActionKeys = ["move", "attack", "enter", "stay", "occupy", "build"];
    private Func<string, string>? _text;
    private Func<DeploymentAction, DeploymentUnitRequest?>? _capture;
    private Func<string?>? _constructionKind;
    private Func<PrepareDeploymentOrder, bool>? _issue;
    private DeploymentBatchView _view = DeploymentBatchView.Empty;
    internal bool HasDraft => _draft.Count != 0;

    public MilitaryDeploymentBatchPanel()
    {
        Name = "MilitaryDeploymentBatchPanel";
        AddThemeConstantOverride("separation", 6);
        AddChild(_title); AddChild(_hint); AddChild(_summary);
        var options = new HFlowContainer(); options.AddChild(_action); options.AddChild(_target); options.AddChild(_add);
        AddChild(options); AddChild(_units);
        var buttons = new HFlowContainer(); buttons.AddChild(_remove); buttons.AddChild(_clear); buttons.AddChild(_submit);
        AddChild(buttons);
        for (int index = 0; index < ActionKeys.Length; index++) _action.AddItem("", index);
        _action.Select(0);
        _action.ItemSelected += _ => { RefreshTargets(); UpdateEnabled(); };
        _target.ItemSelected += _ => UpdateEnabled();
        _units.ItemSelected += _ => UpdateEnabled();
        _add.Pressed += () =>
        {
            if (_add.Disabled || ResolveAction() is not { } action || _capture?.Invoke(action) is not { } request) return;
            _draft.Put(request); RenderDraft();
        };
        _remove.Pressed += () =>
        {
            int[] selection = _units.GetSelectedItems();
            if (!_remove.Disabled && selection.Length > 0) _draft.Remove(_units.GetItemMetadata(selection[0]).AsInt32());
            RenderDraft();
        };
        _clear.Pressed += ResetDraft;
        _submit.Pressed += () =>
        {
            if (_submit.Disabled || _issue is null) return;
            //A rejection or late native eligibility failure leaves every row
            //available for correction. Ordinary refreshes never clear a draft.
            _draft.Submit(_issue);
            RenderDraft();
        };
        UpdateEnabled();
    }

    public void Configure(Func<string, string> text,
        Func<DeploymentAction, DeploymentUnitRequest?> capture,
        Func<string?> constructionKind, Func<PrepareDeploymentOrder, bool> issue)
    {
        _text = text; _capture = capture; _constructionKind = constructionKind; _issue = issue;
        _title.Text = text("play_deployment_batch_title"); _hint.Text = text("play_deployment_batch_hint");
        _add.Text = text("play_deployment_batch_add"); _remove.Text = text("play_military_remove");
        _clear.Text = text("play_military_clear"); _submit.Text = text("play_deployment_batch_submit");
        for (int index = 0; index < ActionKeys.Length; index++)
            _action.SetItemText(index, text("play_military_action_" + ActionKeys[index]));
        RefreshTargets(); RenderDraft();
    }

    public void Refresh(GameState? world, int? forceId, int? cityId, int? selectedHex)
    {
        DeploymentBatchView next = DeploymentBatchView.Create(world, forceId, cityId, selectedHex);
        if (_view.ScenarioKey != next.ScenarioKey || _view.PeriodNumber != next.PeriodNumber || !next.InStrategyPhase)
            _draft.Clear();
        _view = next;
        _draft.BindSource(forceId, cityId);
        RefreshTargets(); RenderDraft();
    }

    /// <summary>Call explicitly on scenario replacement or save loading, even
    /// when that world has the same force, source and period as the current one.</summary>
    public void ResetDraft() { _draft.Clear(); RenderDraft(); }

    // Final browser retirement is distinct from a live draft reset. Release
    // its model and Main callbacks before the owning tree frees child controls.
    // Ordinary CommandWorkspace deck reparenting does not retire this owner.
    internal void Retire()
    {
        _draft.BindSource(null, null);
        _targets.Clear();
        _view = DeploymentBatchView.Empty;
        _text = null; _capture = null; _constructionKind = null; _issue = null;
    }

    private string ActionKey => ActionKeys[_action.GetSelectedId()];
    private DeploymentAction? ResolveAction()
    {
        if (!_view.CanCompose) return null;
        string key = ActionKey;
        if (key is "attack" or "enter")
            return _target.Selected >= 0 && _target.Selected < _targets.Count ? new(key, _targets[_target.Selected].Target) : null;
        if (_view.ActionHex is not int hex) return null;
        if (key == "occupy")
            return _view.OccupationAreaId is int area ? new(key, new(DeploymentTargetKind.Area, area)) : null;
        if (key == "build")
            return _constructionKind?.Invoke() is { } kind ? new(key, new(DeploymentTargetKind.Hex, hex), kind) : null;
        return new(key, new(DeploymentTargetKind.Hex, hex));
    }

    private void RefreshTargets()
    {
        DeploymentTarget? previous = _target.Selected >= 0 && _target.Selected < _targets.Count ? _targets[_target.Selected].Target : null;
        _target.Clear(); _targets.Clear();
        string key = ActionKey;
        _target.Visible = key is "attack" or "enter";
        if (_draft.ForceId is null || !_target.Visible) return;
        foreach (DeploymentTargetChoice choice in key == "attack" ? _view.AttackTargets : _view.EntryTargets)
        {
            string name = Format(choice.Target.Kind == DeploymentTargetKind.Unit ? "play_military_target_unit" : "play_military_target_location",
                choice.Name, choice.Target.Id);
            _targets.Add((choice.Target, choice.HexId, name)); _target.AddItem(name, choice.Target.Id);
        }
        int index = _targets.FindIndex(item => item.Target == previous);
        if (_view.SelectedHex is int selected)
        {
            int mapIndex = _targets.FindIndex(item => item.HexId == selected);
            if (mapIndex >= 0 && (index < 0 || _targets[index].HexId != selected)) index = mapIndex;
        }
        if (index >= 0) _target.Select(index);

    }

    private void RenderDraft()
    {
        _units.Clear();
        foreach (DeploymentUnitRequest unit in _draft.Snapshot())
        {
            string officer = _view.OfficerName(unit.OfficerId);
            string formation = _view.FormationName(unit.FormationId);
            DeploymentAction action = unit.Actions.Single();
            _units.AddItem(Format("play_deployment_batch_unit", officer, formation, unit.Troops,
                _text?.Invoke("play_military_action_" + action.ActionKey) ?? action.ActionKey, action.Target.Id));
            int index = _units.ItemCount - 1; _units.SetItemMetadata(index, unit.OfficerId);
            _units.SetItemTooltip(index, Format("play_deployment_batch_configuration", string.Join(", ", unit.TacticIds.Select(id =>
                _view.TacticName(id))),
                string.Join(" → ", unit.WaypointHexIds), Setting(unit.Behavior?.AttackNearbyEnemies), Setting(unit.Behavior?.AllowRetreat),
                Setting(unit.Behavior?.PursueRetreatingEnemies), _text?.Invoke(unit.Behavior?.AfterAction == DeploymentAfterAction.Retreat
                    ? "play_military_retreat" : "play_military_stay")));
        }
        string troops = "", books = _text?.Invoke("detail_unknown") ?? "";
        if (_view.AvailableTroops is long remainingTroops) troops = (remainingTroops - _draft.Troops).ToString(CultureInfo.InvariantCulture);
        if (_view.AvailableBooks is long remainingBooks) books = (remainingBooks - _draft.Count).ToString(CultureInfo.InvariantCulture);
        _summary.Text = Format("play_deployment_batch_summary", _draft.Count, DeploymentCommandRules.MaximumUnitsPerBatch, _draft.Troops, troops, books);
        SetMeta("deployment_draft_count", _draft.Count); SetMeta("deployment_draft_troops", _draft.Troops);
        UpdateEnabled();
    }

    private void UpdateEnabled()
    {
        _action.Disabled = !_view.CanCompose; _target.Disabled = !_view.CanCompose || _target.ItemCount == 0;
        DeploymentAction? action = ResolveAction();
        DeploymentUnitRequest? request = action is null ? null : _capture?.Invoke(action);
        _add.Disabled = !_view.CanCompose || request is null || _draft.Count >= DeploymentCommandRules.MaximumUnitsPerBatch &&
            !_draft.ContainsOfficer(request.OfficerId);
        _remove.Disabled = !_view.CanCompose || _units.GetSelectedItems().Length == 0;
        _clear.Disabled = _draft.Count == 0; _submit.Disabled = !_view.CanCompose || _draft.Count == 0;
    }
    private string Format(string key, params object?[] args) => _text is null ? "" :
        string.Format(CultureInfo.InvariantCulture, _text(key), args);
    private string Setting(bool? value) => _text?.Invoke(value is null ? "detail_unknown" :
        value.Value ? "new_game_yes" : "new_game_no") ?? "";
}
