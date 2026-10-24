namespace San14.Core;

/// <summary>Transient mode3 saved-route provenance for the complete retained
/// page. The first member may be unselected; points include the current Hex,
/// saved intermediate points and an optional final target.</summary>
public sealed record ArmyWithdrawalSavedGroup(IReadOnlyList<int> OrderedUnitIds,
    IReadOnlyList<int?> PhysicalPoints, int ActiveWaypointIndex, bool HasFinalTarget);

public sealed record ArmyWithdrawalDestination(bool UsesCurrentCommand, int? LocationId, int? HexId);

/// <summary>Current commander-home return order, shared by player and AI.</summary>
public interface IPlayableArmyWithdrawalCommandRules
{
    CommandResult IssueWithdrawal(GameState state, UnitState army);
    /// <summary>Finalize every selected ordinary row after the source footer
    /// checks that at least one member has no withdrawal-blocking abnormality.</summary>
    CommandResult IssueOrdinaryArmiesWithdrawal(GameState state, IReadOnlyList<UnitState> armies,
        IReadOnlyList<ArmyWithdrawalSavedGroup>? savedGroups = null);
    /// <summary>Reads the already selected saved target, without capture or nearest-Base search.</summary>
    ArmyWithdrawalDestination ReadWithdrawalDestination(GameState state, UnitState army);
}
