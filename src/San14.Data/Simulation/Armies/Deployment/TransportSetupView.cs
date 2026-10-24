using San14.Core;

namespace San14.Data;

public sealed record TransportSetupView(int SourceCityId, int? SourceHexId,
    int? SourceTroops, int? SourceGold, int? SourceSupplies, int? CommandBooks,
    IReadOnlyList<DeploymentPersonCandidate>? Persons, int? SelectedOfficerId,
    int? DefaultNavalFormationId, bool? DefaultReturnOfficerToSource,
    int? DefaultSourceBehaviorChoice, TransportQuantityView? InitialQuantities,
    IReadOnlyList<string> UnresolvedInputs)
{
    public int? TroopsMinimum { get; init; }
    public int? AmountStep { get; init; }
    public int? CommandBookCost { get; init; }
    public int? CommandBooksAfter { get; init; }
    public int? DefaultHintMessageId { get; init; }
}

/// <summary>Quantities are editable local values, not reservations. Each
/// maximum/remaining group keeps its own missing owner.</summary>
public sealed record TransportQuantityView(int? MaximumTroops, int? MaximumGold,
    int? MaximumSupplies, int? Troops, int? Gold, int? Supplies,
    int? RemainingTroops, int? RemainingGold, int? RemainingSupplies,
    bool? CanDecide, IReadOnlyList<string> UnresolvedInputs)
{
    public bool? IsPersonValid { get; init; }
    public bool? LowFoodWarningRequired { get; init; }
    public string? LowFoodWarningUnresolvedInput { get; init; }
}

public sealed record TransportTargetMenu(int ClickedHexId, int? TargetHexId,
    bool? CanOpen, UnitTargetMenuKind? Kind, UnitTargetMenuRelation? Relation,
    IReadOnlyList<UnitTargetMenuOption> Options, string? UnresolvedInput);

/// <summary>The page's actual first group. Status entries are native search
/// path counts from each cached segment; null is an unresolved search owner.
/// An empty list is meaningful only for a genuinely segment-free group.</summary>
public sealed record TransportTargetRouteContext(bool HasActiveRouteGroup,
    int? FirstActiveWaypointIndex, IReadOnlyList<int?> SegmentStatuses);

public sealed record TransportDraftMarch(DeploymentDraftRouteStatus Status,
    IReadOnlyList<int>? Path, int? SegmentStatus, int? Days,
    int ExpandedNodes, string? UnresolvedInput, string? EtaUnresolvedInput)
{
    public int? StartSurfaceCode { get; init; }
    public int? EndSurfaceCode { get; init; }
    public int? ControlStartHex { get; init; }
    public int? ControlGoalHex { get; init; }
    public int? ControlStartSurfaceCode { get; init; }
    public int? ControlGoalSurfaceCode { get; init; }
    public string? PresentationUnresolvedInput { get; init; }
}

public sealed record TransportFoodWarning(bool? Required, int? Expense, string? UnresolvedInput);
