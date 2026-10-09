namespace San14.Core;

/// <summary>Plans orders through the same authority and rule pipeline as player commands.</summary>
public interface ICommandPlanner
{
    // Current and command results are borrowed for this operation. A planner must
    // not mutate their collections directly or retain them after Plan returns.
    void Plan(CommandPlanningContext context);
}

/// <summary>
/// Owns the accepted order sequence of one planning operation. Each command has
/// its own candidate, so rejected rules cannot alter earlier accepted orders.
/// The simulation publishes the world only after the whole period succeeds.
/// </summary>
public sealed class CommandPlanningContext
{
    internal CommandPlanningContext(GameState state) => Current = state;

    /// <summary>Borrowed current world; changes go through Apply.</summary>
    public GameState Current { get; private set; }

    public CommandResult Apply(GameOrder order, IOrderRules rules,
        CommandContext actor, IOrderAuthority authority)
    {
        CommandResult result = GameEngine.ApplyCandidate(Current, order, rules, actor, authority);
        if (result.Accepted) Current = result.State!;
        return result;
    }
}
