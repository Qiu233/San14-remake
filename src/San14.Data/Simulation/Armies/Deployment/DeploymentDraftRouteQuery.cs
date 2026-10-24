namespace San14.Data;

/// <summary>One explicit adjacent-point segment from7C4160. Path includes
/// Start and Goal. EtaPath omits exactly this segment's final point. It has no
/// per-row days: the complete ordered ETA list is integrated once per row.</summary>
public sealed record DeploymentDraftRouteSegment(DeploymentDraftRouteStatus Status,
    IReadOnlyList<int>? Path, IReadOnlyList<int>? EtaPath, int ExpandedNodes, string? UnresolvedInput)
{
    //1932E0 reads Surface10 through the same path lookup table; coordinates
    //still come from1888D0's grid index. Presentation gaps do not erase Path.
    public int? StartSurfaceCode { get; init; }
    public int? EndSurfaceCode { get; init; }
    public string? PresentationUnresolvedInput { get; init; }
    public int? ControlStartHex { get; init; }
    public int? ControlGoalHex { get; init; }
    public int? ControlStartSurfaceCode { get; init; }
    public int? ControlGoalSurfaceCode { get; init; }
}

/// <summary>A target page's read-only draft-route current owner. Captured
/// lazily by the first real segment search or reached ETA integration. Replace
/// it when accepting another World. UI owns group order, segment caches and
/// invalidation; selection and row inputs are live call arguments.</summary>
public sealed class DeploymentDraftRouteQuery
{
    private readonly Func<IReadOnlyList<DeploymentDraftRow>, int, int, DeploymentDraftRouteSegment> segment;
    private readonly Func<IReadOnlyList<DeploymentDraftRow>, IReadOnlyList<int>, IReadOnlyList<DeploymentDraftMarchRow>> eta;

    internal DeploymentDraftRouteQuery(
        Func<IReadOnlyList<DeploymentDraftRow>, int, int, DeploymentDraftRouteSegment> segment,
        Func<IReadOnlyList<DeploymentDraftRow>, IReadOnlyList<int>, IReadOnlyList<DeploymentDraftMarchRow>> eta)
    { this.segment = segment; this.eta = eta; }

    /// <summary>Actual candidate-change/Append/final-target/Undo consumer only. Existing draft
    /// WaypointHexIds remain intact and are not interpreted as this segment.
    /// First row chooses the shared path, retaining caller's original order.</summary>
    public DeploymentDraftRouteSegment QuerySegment(IReadOnlyList<DeploymentDraftRow> groupRows,
        int startHex, int goalHex)
    { ArgumentNullException.ThrowIfNull(groupRows); return segment(groupRows, startHex, goalHex); }

    /// <summary>Concatenate each cached segment's EtaPath in order before
    /// calling. Do not omit endpoints again, deduplicate, or add segment days.
    /// This integrates but does not search or inspect the segment cache.</summary>
    public IReadOnlyList<DeploymentDraftMarchRow> QueryEta(IReadOnlyList<DeploymentDraftRow> rows,
        IReadOnlyList<int> assembledEtaPath)
    {
        ArgumentNullException.ThrowIfNull(rows); ArgumentNullException.ThrowIfNull(assembledEtaPath);
        return eta(rows, assembledEtaPath);
    }
}
