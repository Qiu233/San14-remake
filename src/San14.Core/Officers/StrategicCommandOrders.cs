namespace San14.Core;

/// <summary>A strategic personnel command. Kind selects the actual target
/// catalogue; independence is an explicit source choice, never an inferred default.</summary>
public sealed record IssueStrategicCommandOrder(int ForceId, int Kind, int ExecutorId,
    int TargetId, int MilitaryIntelligence, int IndependenceChoice,
    int? SecondForceId = null) : GameOrder(ForceId);

public interface IStrategicCommandRules
{
    CommandResult ApplyStrategicCommand(GameState state, ForceState force,
        IssueStrategicCommandOrder order, CommandContext context);
}

public sealed class StrategicCommandOrders(IOrderRules next, IStrategicCommandRules? rules) : IOrderRules
{
    public CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context)
    {
        if (order is not IssueStrategicCommandOrder strategic) return next.Apply(state, force, order, context);
        if (state.Phase != TurnPhase.Strategy || !force.IsActive || context.DelegatedDistrictId is not null)
            return CommandResult.Reject("Strategic commands require direct strategy control.", "strategic.scope");
        if (strategic.Kind is not (10 or 11 or 12 or 13 or 14 or 20 or 24) ||
            strategic.TargetId < 0 || strategic.MilitaryIntelligence < 0 ||
            strategic.SecondForceId is < 0 or > 51 ||
            !state.Officers.TryGetValue(strategic.ExecutorId, out var executor) || executor.ForceId != force.Id)
            return CommandResult.Reject("The strategic command selection is invalid.", "strategic.input");
        return rules?.ApplyStrategicCommand(state, force, strategic, context) ??
            CommandResult.RuleUnavailable("strategic.command_rules", "Strategic command rules are unavailable.");
    }
}
