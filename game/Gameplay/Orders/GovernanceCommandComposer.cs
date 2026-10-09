using San14.Core;

namespace San14.Client.Gameplay;

/// <summary>Builds a complete target from the current configuration and one edit.
/// Simulation rules remain responsible for appointment and cost decisions.</summary>
public static class GovernanceCommandComposer
{
    public static SetGovernanceOrder? ReplaceOfficer(GameState world, int player,
        int slotId, int? officerId)
    {
        if (world.Forces[player].Governance is not GovernanceState governance) return null;
        GovernanceAssignment[] assignments = governance.Configured.Assignments
            .Where(row => row.SlotId != slotId)
            .Concat(officerId is int officer ? [new GovernanceAssignment(slotId, officer)] : [])
            .ToArray();
        return new(player, Array.AsReadOnly(assignments));
    }
}
