namespace San14.Data;

/// <summary>7C31E0's three numeric UI-row values. Ordinary rows use action,
/// land formation and naval formation; transport rows use action, cargo gold
/// and cargo food. Tag/partition and target kind/identity are not compared.</summary>
public sealed record UnitSelectionGroupKey(byte ActionCode, int PrimaryValue, int SecondaryValue);

/// <summary>A transient page-owned saved group. OrderedUnitIds includes
/// retained unselected members; FirstUnitId is its real first row after the
/// original sort/merge. PhysicalPoints preserves CHex pointer equality,
/// including a known null waypoint pointer. ActiveWaypointIndex counts saved
/// intermediate words only; the optional final target does not increment it.</summary>
public sealed record UnitSelectionSavedGroup(int FirstUnitId, UnitSelectionGroupKey Key,
    IReadOnlyList<int> OrderedUnitIds, IReadOnlyList<int?> PhysicalPoints,
    int ActiveWaypointIndex, bool HasFinalTarget);

/// <summary>One real DA5D0 row in the caller's page-retain order. SavedGroup
/// is a single-row D2770(param0) seed; missing inputs never become an empty
/// route. Native words and target fields retain independent knowledge.</summary>
public sealed record UnitSelectionPageRow(int SourceRowOrdinal, int UnitId,
    UnitSelectionPartition? Partition, UnitSelectionGroupKey? GroupKey,
    UnitSelectionSavedGroup? SavedGroup, IReadOnlyList<ushort>? NativeWaypointWords,
    byte? TargetKind, ushort? TargetIdentity, string? UnresolvedInput);

public sealed record UnitSelectionPageRows(IReadOnlyList<UnitSelectionPageRow> Rows,
    string? UnresolvedInput);
public sealed record UnitSelectionSavedGroups(IReadOnlyList<UnitSelectionSavedGroup> Groups,
    string? UnresolvedInput);
