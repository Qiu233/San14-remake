namespace San14.Data;

/// <summary>One reset-descriptor segment, including both source endpoints.
/// A known empty search is NoPath, never an invented straight-line route.</summary>
public sealed record UnitRoutePreviewSegment(int UnitId, int StartHex, int GoalHex,
    DeploymentDraftRouteStatus Status, IReadOnlyList<int>? Path,
    int ExpandedNodes, string? UnresolvedInput);

/// <summary>Page-owned query bound to one immutable World and actual Army.
/// Reuse it for changed segments; recreate it when that World is replaced.
/// It owns one detached current capture, not a registered preview Army.</summary>
public sealed class UnitRoutePreviewQuery
{
    private readonly Func<int, int, UnitRoutePreviewSegment> query;
    public int UnitId { get; }
    public int? CurrentHexId { get; }
    public string? UnresolvedInput { get; }
    internal UnitRoutePreviewQuery(int unitId, int? currentHex, string? unresolved,
        Func<int, int, UnitRoutePreviewSegment> read)
        => (UnitId, CurrentHexId, UnresolvedInput, query) = (unitId, currentHex, unresolved, read);
    public UnitRoutePreviewSegment QuerySegment(int startHex, int goalHex) => query(startHex, goalHex);
}
