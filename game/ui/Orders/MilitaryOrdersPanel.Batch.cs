using Godot;
using San14.Core;
using San14.Client.Gameplay;

public partial class MilitaryOrdersPanel
{
    private readonly CheckButton _useBatch = new() { Name = "MilitaryBatchEnabled" };
    private readonly Label _batchSummary = new() { Name = "MilitaryBatchSummary" };
    private readonly Button _batchMove = new() { Name = "MilitaryBatchMove", Visible = false };
    private readonly ItemList _batchUnits = new()
    {
        Name = "MilitaryBatchUnits", SelectMode = ItemList.SelectModeEnum.Multi,
        CustomMinimumSize = new(0, 120), Visible = false
    };
    private readonly HashSet<int> _batchSelectedIds = [];
    private readonly Dictionary<int, int> _batchKnownCommanders = [];
    private int? _batchAnchorUnitId;
    private int? _batchAnchorOfficerId;
    private int? _batchForceId;

    private void AddBatchSelectionControls()
    {
        AddChild(_useBatch); AddChild(_batchSummary); AddChild(_batchUnits); AddChild(_batchMove);
        _useBatch.Toggled += _ => { RefreshBatchSelection(); UpdateEnabled(); };
        _batchMove.Pressed += () =>
        {
            if (!_batchMove.Disabled && _selectedHex is int hex)
                RequestAction(new("move", new(DeploymentTargetKind.Hex, hex)));
        };
        _batchUnits.MultiSelected += (index, selected) =>
        {
            int id = _batchUnits.GetItemMetadata((int)index).AsInt32();
            if (selected) _batchSelectedIds.Add(id);
            else _batchSelectedIds.Remove(id);
            RenderBatchSummary(); UpdateEnabled();
        };
    }

    private void ConfigureBatchSelection()
    {
        if (_text is null) return;
        _useBatch.Text = _text("play_military_batch_enable");
        _useBatch.TooltipText = _text("play_military_batch_hint");
        _batchUnits.TooltipText = _useBatch.TooltipText;
        _batchMove.Text = _text("play_military_action_move");
        _batchMove.TooltipText = _useBatch.TooltipText;
        RefreshBatchSelection();
    }

    // Map focus starts a fresh selection. Ordinary command/period refreshes keep
    // the surviving IDs, so no unit from a different force is carried forward.
    private void RefreshBatchSelection()
    {
        MilitaryUnitView? focusedUnit = _view.FocusedUnit;
        int? focusedOfficer = _unitId is int focusedId ? _view.CombatUnit(focusedId)?.OfficerId : null;
        bool changedFocus = _batchAnchorUnitId != _unitId;
        bool previousAnchorSurvives = _batchAnchorUnitId is int priorId &&
            _view.CombatUnit(priorId) is { } prior && prior.OfficerId == _batchAnchorOfficerId;
        bool preserveWithoutCombatFocus = _batchForceId == _forceId && _batchAnchorUnitId is not null &&
            !previousAnchorSurvives && focusedUnit?.IsTransport == true;
        bool newFocus = _batchForceId != _forceId || _unitId is null && !preserveWithoutCombatFocus || _batchAnchorUnitId is null ||
            changedFocus && previousAnchorSurvives || !changedFocus && _batchAnchorOfficerId != focusedOfficer;
        if (newFocus)
        {
            _batchSelectedIds.Clear();
            if (_unitId is int selected) _batchSelectedIds.Add(selected);
        }
        // An automatically replaced focus after retirement leaves the surviving
        // group selected. Choosing another still-live unit starts a new group.
        if (!preserveWithoutCombatFocus)
        { _batchAnchorUnitId = _unitId; _batchForceId = _forceId; _batchAnchorOfficerId = focusedOfficer; }
        MilitaryRosterUnit[] candidates = _unitId is not null || preserveWithoutCombatFocus
            ? _view.CombatUnits.ToArray()
            : [];
        var existing = candidates.ToDictionary(unit => unit.Id, unit => unit.OfficerId);
        _batchSelectedIds.RemoveWhere(id => !existing.TryGetValue(id, out int officerId) ||
            !newFocus && _batchKnownCommanders.GetValueOrDefault(id) != officerId);
        if (!newFocus && changedFocus && _batchSelectedIds.Count == 0 && _unitId is int replacement)
            _batchSelectedIds.Add(replacement);
        _batchKnownCommanders.Clear();
        foreach (MilitaryRosterUnit candidate in candidates) _batchKnownCommanders[candidate.Id] = candidate.OfficerId;
        _batchUnits.Clear();
        foreach (MilitaryRosterUnit unit in candidates)
        {
            _batchUnits.AddItem(Format("play_military_batch_unit", unit.Id, unit.OfficerName, unit.Troops));
            int index = _batchUnits.ItemCount - 1;
            _batchUnits.SetItemMetadata(index, unit.Id);
            if (_batchSelectedIds.Contains(unit.Id)) _batchUnits.Select(index, false);
        }
        _batchUnits.Visible = _useBatch.ButtonPressed && _unitId is not null;
        _batchSummary.Visible = _batchUnits.Visible;
        _batchMove.Visible = _batchUnits.Visible;
        RenderBatchSummary();
    }

    public int[] CommandUnitIdsSnapshot() => _unitId is null ? [] : _useBatch.ButtonPressed
        ? _batchSelectedIds.Order().ToArray()
        : _unitId is int id ? [id] : [];

    private void RenderBatchSummary()
    {
        int count = CommandUnitIdsSnapshot().Length;
        _batchSummary.Text = Format("play_military_batch_count", count);
        SetMeta("military_command_unit_count", count);
    }

    private void UpdateBatchSelectionEnabled()
    {
        _useBatch.Disabled = !_strategy || _unitId is null;
        _batchMove.Disabled = !_strategy || CommandUnitIdsSnapshot().Length == 0 ||
            _selectedHex is not int hex || hex < 0 || hex >= _mapSize;
        for (int index = 0; index < _batchUnits.ItemCount; index++)
            _batchUnits.SetItemSelectable(index, _strategy);
    }
}
