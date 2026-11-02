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
        if (initialState.NativePeriodContinuation is not null || initialState.NativeNormalTurnContinuation is not null ||
            initialState.ManualTacticExecutionContinuation is not null)
            throw new InvalidDataException("A normal period candidate requires its session continuation owner.");
        StateValidator.Validate(initialState);
        State = GameStateCopy.Copy(initialState);
        LastStorySequence = State.CampaignEventCommandPlayback?.Invocation.Story.Sequence ?? State.EditedEventPlayback?.Story.Sequence ?? 0;
    }

    public GameState State { get; private set; }

    public CommandResult Apply(GameOrder order, IOrderRules rules,
        CommandContext context, IOrderAuthority authority)
        => Apply(order, rules, context, authority, null);

    public CommandResult Apply(GameOrder order, IOrderRules rules,
        CommandContext context, IOrderAuthority authority, CampaignAppRandomState? appRandomSnapshot,
        GameState? preparedCandidate = null)
    {
        if (_normalPeriod is not null)
            return ConfirmNormalPeriod(order, rules, context, authority, appRandomSnapshot);
        if (_progress is null && State.ArmyReturnCaptiveContinuation is not null &&
            order is (ConfirmArmyReturnCaptiveBatchOrder or ConfirmArmyReturnCaptiveExecutionOrder or
                CompleteArmyReturnCaptivePresentationOrder or ConfirmRulerSuccessionOrder))
        {
            // A world-owned return can be nested under its existing event or
            // Emperor caller. A formal reply changes this same candidate only.
            var answered = GameEngine.ApplyCandidate(appRandomSnapshot is null ? State :
                State with { CampaignAppRandom32 = appRandomSnapshot }, order, rules, context, authority);
            if (answered.Accepted) State = answered.State!;
            return answered;
        }
        if (_progress?.ArmyProgress is { } movement)
            return ConfirmArmyProgressDecision(movement, order, rules, context, authority, appRandomSnapshot);
        if (State.CampaignEventCommandPlayback is not null)
        {
            if (State.PersonDeathContinuation?.PendingSuccession is not { } request ||
                order is not ConfirmRulerSuccessionOrder decision || decision.ForceId != request.ForceId)
                return CommandResult.Reject("The campaign is awaiting its ordinary event command.", "command.pending_event");
            var decisionResult = GameEngine.ApplyCandidate(State, order, rules, context, authority);
            if (decisionResult.Accepted) State = decisionResult.State!;
            return decisionResult;
        }
        if (_progress?.ManualTactic is { Candidate.ManualTacticExecutionContinuation: not null } manual)
            return ConfirmManualTacticDecision(manual, order, rules, context, authority, appRandomSnapshot);
        RequireNoProgress();
        // The live host lends its current App stream to this same private
        // command transaction. Rejection retains the exact committed world.
        GameState source;
        if (preparedCandidate is not null)
        {
            CheckAuxiliaryTransition(State, preparedCandidate);
            source = appRandomSnapshot is null ? preparedCandidate :
                preparedCandidate with { CampaignAppRandom32 = appRandomSnapshot };
        }
        else source = appRandomSnapshot is null ? State : State with { CampaignAppRandom32 = appRandomSnapshot };
        CommandResult result = GameEngine.ApplyCandidate(source, order, rules, context, authority);
        if (result.Accepted) State = result.State!;
        return result;
    }

    /// <summary>AI planning, simulation and calendar updates form one publication.</summary>
    public GameState AdvancePeriod(IAdvancementRules rules,
        ICommandPlanner? planner = null, Func<GameState, GameState>? complete = null)
    {
        if (State.ArmyReturnCaptiveContinuation is not null)
            throw new InvalidOperationException("The army return is awaiting captive treatment.");
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
        if (NormalPeriodOperation.HasContinuation(advanced))
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

    public GameState FinishReport(IReportingRules rules, CampaignAppRandomState? appRandomSnapshot = null,
        GameState? preparedCandidate = null)
    {
        RequireNoProgress();
        GameState source = State;
        if (preparedCandidate is not null)
        {
            CheckAuxiliaryTransition(State, preparedCandidate);
            source = preparedCandidate;
        }
        if (appRandomSnapshot is not null) source = source with { CampaignAppRandom32 = appRandomSnapshot };
        GameState result = GameEngine.FinishReportOwned(State, GameStateCopy.Copy(source), rules);
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
