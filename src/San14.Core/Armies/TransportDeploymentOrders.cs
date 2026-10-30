namespace San14.Core;

/// <summary>Explicit selections from the transport Make and Target pages.
/// Supplies and Gold are cargo. SourceBehaviorChoice preserves the selected
/// source option code0..2; it is independent of the officer-return selection.
/// Waypoints are player-selected intermediate hexes, not an automatic path.</summary>
public sealed record DispatchTransportArmyOrder(int ForceId, int SourceCityId, int OfficerId,
    int Troops, int Supplies, int Gold, int NavalFormationId, DeploymentAction Action,
    IReadOnlyList<int> WaypointHexIds, bool ReturnOfficerToSource, int SourceBehaviorChoice) : GameOrder(ForceId)
{
    /// <summary>Explicit commander district execution and payment. Null retains
    /// the ordinary Make/Target page path. Current-world rules verify the
    /// commander district independently from the source city's district.</summary>
    public int? DistrictId { get; init; }
    /// <summary>The created army's actual transport objective, published by
    /// its creation transaction. Null leaves the ordinary page result alone.</summary>
    public ArmyGroupPurpose? GroupPurpose { get; init; }
}

/// <summary>Explicit Save Defaults click; it does not submit an Army or save
/// quantities, naval formation, intermediate points or a target.</summary>
public sealed record SaveTransportDefaultsOrder(int ForceId, bool ReturnOfficerToSource,
    int SourceBehaviorChoice) : GameOrder(ForceId);

/// <summary>The source command owns its real command-book cost, packet,
/// creation, stocks and lifecycle publication in one command transaction.
/// The compatibility City-only creator remains a separate entry point.</summary>
public interface IPlayableTransportArmyCommandRules : IPlayableTransportCreationRules
{
    CommandResult IssueTransportArmy(GameState state, DispatchTransportArmyOrder order);
    CommandResult SaveTransportDefaults(GameState state, SaveTransportDefaultsOrder order);
}

/// <summary>The Make decision evaluates the source food warning once. Its
/// reached pending-retirement and grouping work remains even after cancellation.</summary>
public sealed record PrepareTransportFoodWarningOrder(int ForceId, int SourceCityId,
    int CargoSupplies) : GameOrder(ForceId);

/// <summary>Transient result of the Make decision; it is not campaign save state.</summary>
public sealed record TransportFoodWarningResult(bool Required, int Expense);

public interface IPlayableTransportFoodWarningRules
{
    CommandResult PrepareTransportFoodWarning(GameState state, PrepareTransportFoodWarningOrder order);
}
