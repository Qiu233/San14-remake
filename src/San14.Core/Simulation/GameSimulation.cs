namespace San14.Core;

/// <summary>
/// Owns a campaign world. Rules work on a private transaction; only a completed
/// operation publishes it. Views borrow State and must not mutate its collections.
/// Loading and campaign construction validate once, before gameplay begins.
/// </summary>
public sealed class GameSimulation
{
    public GameSimulation(GameState initialState)
    {
        StateValidator.Validate(initialState);
        State = GameStateCopy.Copy(initialState);
    }

    public GameState State { get; private set; }

    public CommandResult Apply(GameOrder order, IOrderRules rules,
        CommandContext context, IOrderAuthority authority)
    {
        CommandResult result = GameEngine.ApplyCandidate(State, order, rules, context, authority);
        if (result.Accepted) State = result.State!;
        return result;
    }

    /// <summary>AI planning, simulation and calendar updates form one publication.</summary>
    public GameState AdvancePeriod(IAdvancementRules rules,
        ICommandPlanner? planner = null, Func<GameState, GameState>? complete = null)
    {
        GameState working = GameStateCopy.Copy(State);
        var planning = new CommandPlanningContext(working);
        planner?.Plan(planning);
        GameState prepared = planning.Current;
        CheckAuxiliaryTransition(State, prepared);
        GameState advanced = GameEngine.AdvancePeriodOwned(State, prepared, rules);
        GameState result = advanced.NativePlayableTerminal is null && complete is not null
            ? complete(advanced) : advanced;
        CheckAuxiliaryTransition(advanced, result);
        GameEngine.ValidateWorldIdentity(State, result);
        State = result;
        return State;
    }

    public GameState FinishReport(IReportingRules rules)
    {
        GameState result = GameEngine.FinishReportOwned(State, GameStateCopy.Copy(State), rules);
        State = result;
        return State;
    }

    private static void CheckAuxiliaryTransition(GameState previous, GameState next)
    {
        GameEngine.ValidateWorldIdentity(previous, next);
        if (next.Phase != previous.Phase || next.Date != previous.Date ||
            next.PeriodNumber != previous.PeriodNumber || next.NativeClock != previous.NativeClock ||
            next.NativePlayableTerminal != previous.NativePlayableTerminal)
            throw new InvalidDataException("Planning or calendar rules changed the simulation boundary.");
    }
}
