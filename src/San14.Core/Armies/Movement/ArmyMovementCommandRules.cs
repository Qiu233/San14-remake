namespace San14.Core;

/// <summary>Commits the actual current action, target and five waypoints for
/// a shared player/AI movement, Base-entry or Unit/Base attack order.</summary>
public interface IPlayableArmyMovementCommandRules
{
    CommandResult IssueArmyMovementCommand(GameState state, UnitState army,
        DeploymentAction action, IReadOnlyList<int> waypointHexIds);
}
