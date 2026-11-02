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
        return ApplyPriority(state, force.Id, priority, rules, context);
    }

    // Only this source-backed immediate inspector command is admitted outside
    // Strategy. The host must hold the actual presentation World exclusively.
    public static CommandResult ApplyPresentation(GameState state, SetOfficerPriorityOrder priority,
        IOfficerPriorityRules? rules, CommandContext context, IOrderAuthority authority)
    {
        var error = GameEngine.CheckPresentationOrder(state, priority, context, authority);
        return error ?? ApplyPriority(state, priority.ForceId, priority, rules, context);
    }

    private static CommandResult ApplyPriority(GameState state, int forceId, SetOfficerPriorityOrder priority,
        IOfficerPriorityRules? rules, CommandContext context)
    {
        if (state.Phase == TurnPhase.Initializing || context.DelegatedDistrictId is not null ||
            priority.Choice is not (0 or 1) || !state.Officers.ContainsKey(priority.OfficerId))
            return CommandResult.Reject("This officer priority cannot be changed.", "officer.priority_scope");
        return rules?.ApplyOfficerPriority(state, forceId, priority.OfficerId, priority.Choice, context) ??
            CommandResult.RuleUnavailable("officer.priority_rules", "Officer priority rules are unavailable.");
    }
}
