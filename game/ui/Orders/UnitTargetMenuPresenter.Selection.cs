using Godot;
using San14.Data;

internal sealed partial class UnitTargetMenuPresenter
{
    private int selectionDistrict;
    private Action? selectionChanged, emptySelection;
    private Func<bool>? selectionInputAllowed;
    private UnitSelectionBoxView? selectionBox;
    private Func<int, Vector2?> selectionProjection = null!;
    private bool selectionArmed, selectionDragging, selectionRejected;
    private Vector2 selectionStart, selectionEndpoint;
    private IReadOnlyList<UnitSelectionMember> selectionCandidates = [];
    private readonly List<UnitSelectionMember> selectionHits = [];

    private void InitializeSelection(int unitId, OrdinaryArmyMapEntry? initial)
    {
        if (initial is not null)
        {
            if (initial.CanOpen != true || initial.PageDistrictId is not int pageDistrict || initial.Rows.Count == 0 ||
                initial.Rows[0].Army.UnitId != unitId)
                throw new InvalidDataException("The ordinary Army entry has no current initial vector.");
            var members = initial.Rows.Select(row => Member(row.Army.UnitId,
                row.Page.Partition ?? throw new InvalidDataException("Initial Army partition is unavailable."),
                row.Army.CurrentHexId ?? throw new InvalidDataException("Initial Army Hex is unavailable."), row.Page)
                ?? throw new InvalidDataException("Initial Army route seed is unavailable.")).ToArray();
            selectionDistrict = pageDistrict;
            Selection.Initialize(members, initial.InitiallySelected);
            return;
        }
        var query = rules.QueryUnitSelectionRows(world, [unitId]);
        var row = query.Rows.SingleOrDefault() ?? throw new InvalidDataException(query.UnresolvedInput ?? "Army selection row is missing.");
        if (row.NativeValid != true || row.DistrictPhysicalSlot is not int district || Member(row) is not { } member)
            throw new InvalidDataException(row.UnresolvedInput ?? "Current Army selection row is unavailable.");
        selectionDistrict = district;
        Selection.Click(member, false);
    }
    internal void ConfigureSelection(Func<int, Vector2?> projectGround,
        Func<bool> allowed, Action changed, Action empty)
    {
        selectionProjection = projectGround; selectionInputAllowed = allowed;
        selectionChanged = changed; emptySelection = empty;
        selectionBox = view.ConfigureSourceSelectionBox(repository);
    }
    private UnitSelectionMember? Member(UnitSelectionRow row)
    {
        if (Selection.RetainedMember(row.UnitId) is { } retained) return retained;
        if (row.CurrentHexId is not int hex || row.Partition is not { } partition) return null;
        var pageRow = rules.QueryUnitSelectionPageRows(world, [row.UnitId]).Rows.SingleOrDefault();
        return Member(row.UnitId, partition, hex, pageRow);
    }
    private static UnitSelectionMember? Member(int unitId, UnitSelectionPartition partition, int hex,
        UnitSelectionPageRow? row) => row is { UnresolvedInput: null, GroupKey: { } key, SavedGroup: { } saved }
            ? new(unitId, partition, hex, key, saved) : null;

    internal bool ClickArmy(int unitId, bool control)
    {
        if (disposed || !current() || selectionInputAllowed?.Invoke() != true || selectionDragging) return false;
        // 7E4890's caller gate: an existing selection without Control treats
        // the Army hit as a target. Only no selection or Control calls7E0070.
        if (Selection.SelectedCount > 0 && !control) return false;
        var query = rules.QueryUnitSelectionRows(world, [unitId], selectionDistrict);
        var row = query.Rows.SingleOrDefault();
        if (row is null || row.ClickSelectionAllowed is null)
        { view.ApplyHint(text.Get("play_unit_selection_unavailable")); return true; }
        if (row.ClickSelectionAllowed != true || Member(row) is not { } member) return false;
        if (!Selection.Click(member, control)) return true;
        SelectionDidChange();
        if (Selection.SelectedCount == 0) emptySelection?.Invoke();
        return true;
    }
    private void SelectionDidChange()
    {
        transportSegments.Clear();
        candidateRoutes.Clear();routePublicationDirty=true;
        CloseMenu(); selectedHex = null; previewHex = null;
        RefreshPoints(); selectionChanged?.Invoke();
    }
    internal bool SampleSelectionPointer(Vector2 cursor, int leftState, bool shift, bool control)
    {
        if (disposed || !current() || selectionInputAllowed?.Invoke() != true)
        { ResetSelectionGesture(); return false; }
        if (selectionRejected)
        {
            if (!shift || leftState is 0 or 1) selectionRejected = false;
            return true;
        }
        if (leftState == 3 && shift && !selectionArmed && !selectionDragging)
        { selectionArmed = true; selectionStart = selectionEndpoint = cursor; }
        bool consumed = selectionDragging;
        if ((selectionArmed || selectionDragging) && (!shift || leftState is 0 or 1))
        {
            bool wasDragging = selectionDragging;
            ResetSelectionGesture();
            if (wasDragging && Selection.SelectedCount == 0) emptySelection?.Invoke();
            return consumed;
        }
        if (!selectionArmed && !selectionDragging) return false;
        var delta = cursor - selectionStart;
        if (!selectionDragging && leftState == 2 && delta.LengthSquared() >= 101)
        {
            selectionArmed = false; selectionDragging = true; consumed = true;
            // One query/capture at drag start. Per-frame projection visits only
            // the real registry candidates, never every world Hex or a clone.
            var candidates = rules.QueryUnitSelectionCandidates(world);
            if (!candidates.Completed || candidates.Rows.Any(row => row.Included is null ||
                row.Included == true && (row.UnitId is null || row.CurrentHexId is null || row.Partition is null ||
                    row.ActionCode is null || row.TargetKind is null || row.TargetIdentity is null || row.RowUnresolvedInput is not null)))
            {
                ResetSelectionGesture();
                selectionRejected = true;
                view.ApplyHint(text.Get("play_unit_selection_unavailable"));
                return true;
            }
            var visible = candidates.Rows.Where(row => row.Included == true && row.UnitId is not null &&
                row.CurrentHexId is not null && row.Partition is not null && row.RowUnresolvedInput is null &&
                selectionProjection(row.CurrentHexId.Value) is { } point && OnScreen(point)).ToArray();
            // Capture original saved-route seeds once at drag start. Per-frame
            // rectangle updates retain these rows and never reread the world.
            var newIds = visible.Where(row => Selection.RetainedMember(row.UnitId!.Value) is null)
                .Select(row => row.UnitId!.Value).ToArray();
            var pageRows = rules.QueryUnitSelectionPageRows(world, newIds);
            var byId = pageRows.Rows.ToDictionary(row => row.UnitId);
            var members = visible.Select(row => Selection.RetainedMember(row.UnitId!.Value) ??
                Member(row.UnitId.Value, row.Partition!.Value,
                    row.CurrentHexId!.Value, byId.GetValueOrDefault(row.UnitId.Value))).ToArray();
            if (pageRows.UnresolvedInput is not null || members.Any(member => member is null))
            {
                ResetSelectionGesture(); selectionRejected = true;
                view.ApplyHint(text.Get("play_unit_selection_unavailable")); return true;
            }
            selectionCandidates = members.Select(member => member!.Value).ToArray();
            selectionHits.Clear(); Selection.Replace([]); SelectionDidChange();
        }
        if (!selectionDragging) return shift; // arm owns Shift-left before camera pan
        // The source artwork consumes the prior endpoint before the selector
        // stores this update's endpoint. Hit testing uses the new endpoint.
        selectionBox?.Apply(new Rect2(selectionStart, selectionEndpoint - selectionStart));
        if (selectionEndpoint == cursor) return true;
        selectionEndpoint = cursor;
        float left = Math.Min(selectionStart.X, cursor.X), right = Math.Max(selectionStart.X, cursor.X);
        float top = Math.Min(selectionStart.Y, cursor.Y), bottom = Math.Max(selectionStart.Y, cursor.Y);
        bool Hit(UnitSelectionMember member) => selectionProjection(member.CurrentHexId) is { } point && OnScreen(point) &&
            point.X >= left && point.X <= right && point.Y >= top && point.Y <= bottom;
        selectionHits.RemoveAll(member => !Hit(member));
        foreach (var member in selectionCandidates)
            if (Hit(member) && (selectionHits.Count == 0 || selectionHits[0].Partition == member.Partition) &&
                !selectionHits.Any(hit => hit.UnitId == member.UnitId)) selectionHits.Add(member);
        var selected = selectionHits.Take(UnitSelectionDraft.MaximumSelected).ToArray();
        if (!Selection.UnitIds.Order().SequenceEqual(selected.Select(member => member.UnitId).Order()))
        { Selection.Replace(selected); SelectionDidChange(); }
        return true;
    }
    private static bool OnScreen(Vector2 point) => point.X >= 0 && point.X <= 1920 && point.Y >= 0 && point.Y <= 1080;
    internal void ResetSelectionGesture()
    {
        selectionArmed = selectionDragging = selectionRejected = false;
        selectionCandidates = []; selectionHits.Clear(); selectionBox?.Apply(null);
    }
}
