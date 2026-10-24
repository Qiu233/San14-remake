namespace San14.Core;

/// <summary>Mode3 changes an existing transport receiver, never its cargo,
/// commander or source stocks. Waypoints are explicit ordered player points.</summary>
public sealed record SetTransportArmyActionOrder(int ForceId, int UnitId,
    DeploymentAction Action, IReadOnlyList<int> WaypointHexIds) : GameOrder(ForceId);
public sealed record SetTransportArmiesActionOrder(int ForceId, IReadOnlyList<int> UnitIds,
    DeploymentAction Action, IReadOnlyList<int> WaypointHexIds) : GameOrder(ForceId);

/// <summary>Null retains that unit's own value. The source behavior choice
/// and the officer-return bit are independent, with no Arrival projection.</summary>
public sealed record SetTransportArmyOptionsOrder(int ForceId, int UnitId,
    int? SourceBehaviorChoice, bool? ReturnOfficerToSource) : GameOrder(ForceId);
public sealed record SetTransportArmiesOptionsOrder(int ForceId, IReadOnlyList<int> UnitIds,
    int? SourceBehaviorChoice, bool? ReturnOfficerToSource) : GameOrder(ForceId);

public interface IPlayableTransportUnitCommandRules
{
    CommandResult IssueTransportArmyAction(GameState state, UnitState army,
        DeploymentAction action, IReadOnlyList<int> waypointHexIds);
    CommandResult IssueTransportArmiesWithdrawal(GameState state, IReadOnlyList<UnitState> armies,
        IReadOnlyList<ArmyWithdrawalSavedGroup>? savedGroups = null);
    CommandResult SetTransportArmyOptions(GameState state, UnitState army,
        int? sourceBehaviorChoice, bool? returnOfficerToSource);
}
