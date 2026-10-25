namespace San14.Core;

public sealed record SetOfficerPriorityOrder(int ForceId, int OfficerId, int Choice) : GameOrder(ForceId);

public interface IOfficerPriorityRules
{
    CommandResult ApplyOfficerPriority(GameState state, int forceId, int officerId,
        int choice, CommandContext context);
}

public sealed class OfficerPriorityOrders(IOrderRules next, IOfficerPriorityRules? rules) : IOrderRules
{
    public CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context)
    {
        if (order is not SetOfficerPriorityOrder priority) return next.Apply(state, force, order, context);
        if (state.Phase == TurnPhase.Initializing || context.DelegatedDistrictId is not null ||
            priority.Choice is not (0 or 1) || !state.Officers.ContainsKey(priority.OfficerId))
            return CommandResult.Reject("This officer priority cannot be changed.", "officer.priority_scope");
        return rules?.ApplyOfficerPriority(state, force.Id, priority.OfficerId, priority.Choice, context) ??
            CommandResult.RuleUnavailable("officer.priority_rules", "Officer priority rules are unavailable.");
    }
}
