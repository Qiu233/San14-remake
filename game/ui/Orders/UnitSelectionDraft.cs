using San14.Data;

internal readonly record struct UnitSelectionMember(int UnitId, UnitSelectionPartition Partition,
    int CurrentHexId, UnitSelectionGroupKey GroupKey, UnitSelectionSavedGroup SavedGroup);

/// <summary>Retained page rows own saved display groups independently of the
/// selected rows and their fresh active routes. Deselecting a row keeps its
/// saved group and real first-member provenance.</summary>
internal sealed class UnitSelectionDraft
{
    internal const int MaximumSelected = 30;
    private sealed record Row(UnitSelectionMember Member) { internal bool Selected { get; set; } }
    internal sealed class Group(UnitSelectionMember first)
    {
        internal UnitSelectionMember First { get; } = first;
        internal List<int> UnitIds { get; } = [first.UnitId];
        // The screen hit anchor is Army2A. A route starts at the separate
        // PersonE4-resolved receiver captured by the saved seed (or is empty).
        internal List<int> Points { get; } = first.SavedGroup.PhysicalPoints
            .Take(1).Select(point => point ?? throw new InvalidDataException("unit.selection.active-start")).ToList();
    }
    private readonly List<Row> rows = [];
    private readonly Dictionary<int, Row> byUnit = [];
    private readonly List<Group> groups = [];
    private IReadOnlyList<UnitSelectionSavedGroup> savedGroups = [];
    internal IReadOnlyList<UnitSelectionSavedGroup> SavedGroups => savedGroups;
    internal int SelectedCount { get; private set; }
    internal UnitSelectionPartition? Partition => rows.FirstOrDefault(row => row.Selected)?.Member.Partition;
    internal IReadOnlyList<Group> Groups => groups;
    internal int FirstWaypointIndex => groups.Count == 0 ? 0 : Math.Max(0, groups[0].Points.Count - 1);
    internal int[] UnitIds => rows.Where(row => row.Selected).Select(row => row.Member.UnitId).ToArray();
    internal IReadOnlyList<int> RetainedUnitIds => rows.Select(row => row.Member.UnitId).ToArray();
    internal int[] Waypoints => groups.Count == 0 ? [] : groups[0].Points.Skip(1).ToArray();
    internal UnitSelectionMember? RetainedMember(int unitId) =>
        byUnit.TryGetValue(unitId, out var row) ? row.Member : null;

    //Initial group membership comes from the ordinary Scene entry vector.
    //The thirty-selection limit applies to later Ctrl/box additions only.
    internal void Initialize(IReadOnlyList<UnitSelectionMember> members, bool selected)
    {
        if (rows.Count != 0) throw new InvalidOperationException("Army selection is already initialized.");
        foreach (var member in members)
        {
            var row = Retain(member);
            row.Selected = selected;
            if (selected) SelectedCount++;
        }
        Rebuild();
    }

    internal bool Click(UnitSelectionMember member, bool control)
    {
        byUnit.TryGetValue(member.UnitId, out var row);
        if (control)
        {
            if (rows.Any(item => item.Selected && item.Member.Partition != member.Partition)) return false;
            if (row?.Selected == true) { row.Selected = false; SelectedCount--; Rebuild(); return true; }
            if (SelectedCount >= MaximumSelected)
            {
                // The original still rebuilds active groups for a retained
                // unselected row; a new thirty-first row stops before append.
                if (row is null) return false;
                Rebuild(); return true;
            }
        }
        else { foreach (var item in rows) item.Selected = false; SelectedCount = 0; }
        row ??= Retain(member);
        row.Selected = true; SelectedCount++; Rebuild(); return true;
    }

    internal void Replace(IReadOnlyList<UnitSelectionMember> members)
    {
        foreach (var row in rows) row.Selected = false;
        SelectedCount = 0;
        foreach (var member in members.Take(MaximumSelected))
        {
            var row = byUnit.GetValueOrDefault(member.UnitId) ?? Retain(member);
            if (row.Selected) continue;
            row.Selected = true; SelectedCount++;
        }
        Rebuild();
    }
    private Row Retain(UnitSelectionMember member)
    {
        var sorted = UnitSelectionSavedGroupOperations.SortAndMerge([.. savedGroups, member.SavedGroup]);
        if (sorted.UnresolvedInput is not null) throw new InvalidDataException(sorted.UnresolvedInput);
        var row = new Row(member); rows.Add(row); byUnit.Add(member.UnitId, row);
        savedGroups = sorted.Groups;
        return row;
    }
    private void Rebuild()
    {
        // Active groups are rebuilt from selected retained rows in page order.
        // They use the same comparator and numeric row key, but only a current
        // start point. The saved groups above remain intact.
        var fresh = rows.Where(row => row.Selected).Select(row =>
            new UnitSelectionSavedGroup(row.Member.UnitId, row.Member.GroupKey,
                new[] { row.Member.UnitId }, row.Member.SavedGroup.PhysicalPoints.Take(1).ToArray(), 0, false)).ToArray();
        var sorted = UnitSelectionSavedGroupOperations.SortAndMerge(fresh);
        if (sorted.UnresolvedInput is not null) throw new InvalidDataException(sorted.UnresolvedInput);
        groups.Clear();
        foreach (var source in sorted.Groups)
        {
            var group = new Group(byUnit[source.FirstUnitId].Member);
            group.UnitIds.Clear(); group.UnitIds.AddRange(source.OrderedUnitIds);
            groups.Add(group);
        }
    }
    internal bool Append(int hex)
    {
        if (groups.Count == 0 || groups.Any(group => group.Points.Count == 0) || FirstWaypointIndex >= 5) return false;
        foreach (var group in groups) group.Points.Add(hex);
        return true;
    }
    internal bool Undo()
    {
        if (groups.Count == 0 || FirstWaypointIndex == 0) return false;
        foreach (var group in groups) if (group.Points.Count > 1) group.Points.RemoveAt(group.Points.Count - 1);
        return true;
    }
}
