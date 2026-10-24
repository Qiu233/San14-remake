namespace San14.Data;

/// <summary>A real active Trap receiver and the physical Hex selected by the
/// false-report destination page. This is not the acting Army's route goal.</summary>
public sealed record ArmyFalseReportDestination(int ObjectPhysicalSlot, int HexId);

/// <summary>One page publication. Null destinations preserve missing current
/// inputs; a known empty list means that no inducement target is available.</summary>
public sealed record ArmyFalseReportDestinationSelection(bool? TargetAllowed,
    IReadOnlyList<ArmyFalseReportDestination>? Destinations, string? UnresolvedInput);

public sealed record ArmyFalseReportPointer(bool? ValidSurface, bool? CanPreviewRoute,
    bool? IsCandidate, string? UnresolvedInput);

/// <summary>A single Gihou page's current owner. The host retains the last
/// actual hover and segment; this query never captures another World.</summary>
public sealed class ArmyFalseReportTargetQuery
{
    private readonly Func<int, ArmyFalseReportPointer> pointer;
    private readonly Func<int, DeploymentDraftRouteSegment> route;
    internal ArmyFalseReportTargetQuery(ArmyFalseReportDestinationSelection selection, int? startHexId,
        Func<int, ArmyFalseReportPointer> pointer, Func<int, DeploymentDraftRouteSegment> route)
    { Selection = selection; StartHexId = startHexId; this.pointer = pointer; this.route = route; }
    public ArmyFalseReportDestinationSelection Selection { get; }
    public int? StartHexId { get; }
    public ArmyFalseReportPointer QueryPointer(int physicalHexId) => pointer(physicalHexId);
    public DeploymentDraftRouteSegment QueryRoute(int physicalHexId) => route(physicalHexId);
}
