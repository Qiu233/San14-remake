namespace San14.Core;

/// <summary>
/// Owns a campaign world. Rules work on a private transaction; only a completed
/// operation publishes it. Views borrow State and must not mutate its collections.
/// Loading and campaign construction validate once, before gameplay begins.
/// </summary>
public sealed partial class GameSimulation
{
    public GameSimulation(GameState initialState)
    {
        if (initialState.NativePeriodContinuation is not null)
            throw new InvalidDataException("A normal period candidate requires its session continuation owner.");
        StateValidator.Validate(initialState);
        State = GameStateCopy.Copy(initialState);
    }

    public GameState State { get; private set; }

    public CommandResult Apply(GameOrder order, IOrderRules rules,
        CommandContext context, IOrderAuthority authority)
        => Apply(order, rules, context, authority, null);

    public CommandResult Apply(GameOrder order, IOrderRules rules,
        CommandContext context, IOrderAuthority authority, CampaignAppRandomState? appRandomSnapshot)
    {
        if (_normalPeriod is not null)
            return ConfirmNormalPeriod(order, rules, context, authority, appRandomSnapshot);
        RequireNoProgress();
        // The live host lends its current App stream to this same private
        // command transaction. Rejection retains the exact committed world.
        GameState source = appRandomSnapshot is null ? State : State with { CampaignAppRandom32 = appRandomSnapshot };
        CommandResult result = GameEngine.ApplyCandidate(source, order, rules, context, authority);
        if (result.Accepted) State = result.State!;
        return result;
    }

    /// <summary>AI planning, simulation and calendar updates form one publication.</summary>
    public GameState AdvancePeriod(IAdvancementRules rules,
        ICommandPlanner? planner = null, Func<GameState, GameState>? complete = null)
    {
        GameState prepared;
        if (_normalPeriod is { } pending)
        {
            RequireNormalPeriodConfirmed(pending);
            prepared = GameStateCopy.Copy(pending.Candidate);
        }
        else
        {
            RequireNoProgress();
            var planning = new CommandPlanningContext(GameStateCopy.Copy(State));
            planner?.Plan(planning);
            prepared = planning.Current;
            CheckAuxiliaryTransition(State, prepared);
        }
        GameState advanced = GameEngine.AdvancePeriodOwned(State, prepared, rules);
        if (advanced.NativePeriodContinuation?.Cursor is not null)
        {
            _normalPeriod = new(advanced);
            return State;
        }
        GameState result = advanced.NativePlayableTerminal is null && advanced.CampaignTurnStop is null && complete is not null
            ? complete(advanced) : advanced;
        // Check final identity against the committed input, including a
        // calendar writer that mutates its own transaction's content ledger.
        CheckAuxiliaryTransition(advanced, result, State);
        State = result;
        _normalPeriod = null;
        return State;
    }

    public GameState FinishReport(IReportingRules rules)
    {
        RequireNoProgress();
        GameState result = GameEngine.FinishReportOwned(State, GameStateCopy.Copy(State), rules);
        result = result with { PreparedActionReport = null };
        State = result;
        return State;
    }

    private static void CheckAuxiliaryTransition(GameState previous, GameState next,
        GameState? identity = null)
    {
        GameEngine.ValidateWorldIdentity(identity ?? previous, next);
        if (next.Phase != previous.Phase || next.Date != previous.Date ||
            next.PeriodNumber != previous.PeriodNumber || next.NativeClock != previous.NativeClock ||
            next.NativePlayableTerminal != previous.NativePlayableTerminal || next.CampaignTurnStop != previous.CampaignTurnStop)
            throw new InvalidDataException("Planning or calendar rules changed the simulation boundary.");
    }
}
