namespace San14.Core;

/// <summary>Explicit selections from the transport Make and Target pages.
/// Supplies and Gold are cargo. SourceBehaviorChoice preserves the selected
/// source option code0..2; it is independent of the officer-return selection.
/// Waypoints are player-selected intermediate hexes, not an automatic path.</summary>
public sealed record DispatchTransportArmyOrder(int ForceId, int SourceCityId, int OfficerId,
    int Troops, int Supplies, int Gold, int NavalFormationId, DeploymentAction Action,
    IReadOnlyList<int> WaypointHexIds, bool ReturnOfficerToSource, int SourceBehaviorChoice) : GameOrder(ForceId);

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
