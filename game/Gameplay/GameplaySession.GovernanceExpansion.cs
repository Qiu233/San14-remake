using San14.Core;

namespace San14.Client.Gameplay;

public sealed partial class GameplaySession
{
    public GovernanceExpansionEvaluation EvaluateGovernanceExpansion(int group)
    {
        int player = RequirePlayer();
        if (HasPendingOperation || World.Phase != TurnPhase.Strategy)
            return new(false, null, null, null, null, "Department expansion requires the strategy phase.", null);
        return Rules.GovernanceRosterRules is IGovernanceExpansionRules expansion
            ? expansion.EvaluateGovernanceExpansion(World, World.Forces[player], group, new CommandContext(LocalActor))
            : new(null, null, null, null, null, null, "governance.expansion_rules");
    }

    public GovernanceExpansionEvaluation EvaluateGovernanceExpansionForPolicyLevelUp(GameState expectedOwner,
        long batchSequence, int requestIndex, int group)
    {
        int player = RequirePlayer();
        if (!_simulation.IsForcePolicyEditorOwner(expectedOwner, batchSequence, requestIndex, player))
            return new(false, null, null, null, null, "The policy editor no longer owns this expansion.", null);
        return Rules.GovernanceRosterRules is IGovernanceExpansionRules expansion
            ? expansion.EvaluateGovernanceExpansion(expectedOwner, expectedOwner.Forces[player], group, new CommandContext(LocalActor))
            : new(null, null, null, null, null, null, "governance.expansion_rules");
    }
}
