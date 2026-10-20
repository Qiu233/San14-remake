namespace San14.Core;

/// <summary>Save the local player's four ordinary deployment preferences.
/// This does not edit an active unit or submit the current deployment draft.</summary>
public sealed record SaveDeploymentDefaultsOrder(int ForceId, DeploymentBehavior Behavior) : GameOrder(ForceId);

internal static class DeploymentDefaultsCommand
{
    public static CommandResult Apply(GameState state, SaveDeploymentDefaultsOrder order, CommandContext context)
    {
        //The session's ordinary force authority and strategy-phase checks
        //run before dispatch. A global player preference is not delegated.
        if (context.DelegatedDistrictId is not null)
            return CommandResult.Reject("Deployment defaults require direct player control.", "deployment.defaults_scope");
        if (order.Behavior is null || !Enum.IsDefined(order.Behavior.AfterAction))
            return CommandResult.Reject("Deployment defaults are missing or invalid.", "deployment.defaults_behavior");
        return CommandResult.Accept(state.DefaultDeploymentBehavior == order.Behavior
            ? state : state with { DefaultDeploymentBehavior = order.Behavior });
    }
}
