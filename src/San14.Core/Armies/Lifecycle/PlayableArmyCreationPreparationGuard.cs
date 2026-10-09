namespace San14.Core;

/// <summary>Rejects an unavailable source constructor at the shared command
/// boundary, before its accepted preparation state is published. Cancellation
/// remains available even when an older pending queue has unknown owners.</summary>
public sealed class PlayableArmyCreationPreparationGuard(IOrderRules otherCommands,
    IPlayableArmyCreationPreparationRules preparationRules) : IOrderRules
{
    public const string MissingRuleKey = "rules.army_creation";

    public CommandResult Apply(GameState state, ForceState force, GameOrder order,
        CommandContext context)
    {
        CommandResult result = otherCommands.Apply(state, force, order, context);
        if (order is not PrepareDeploymentOrder || !result.Accepted) return result;
        DeploymentEligibility eligibility = preparationRules.CheckPreparedDeployments(
            GameStateSnapshot.Detach(result.State!));
        return eligibility.Status switch
        {
            DeploymentDecisionStatus.Allowed => result,
            DeploymentDecisionStatus.Denied => CommandResult.Reject(
                eligibility.Explanation ?? "The source constructor cannot create this pending deployment queue.",
                "deployment.native_creation_denied"),
            DeploymentDecisionStatus.Unavailable => CommandResult.RuleUnavailable(
                eligibility.MissingRuleKey ?? MissingRuleKey,
                eligibility.Explanation ?? "Current ordinary Army construction inputs are unresolved."),
            _ => throw new InvalidDataException("Army preparation rules returned an unknown decision.")
        };
    }
}
