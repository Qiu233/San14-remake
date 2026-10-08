namespace San14.Core;

public sealed record UseTacticOrder(int ForceId, int UnitId, int TacticId, int TargetUnitId) : GameOrder(ForceId);
public sealed record CancelTacticUseOrder(int ForceId, int UnitId) : GameOrder(ForceId);

/// <summary>Queues a use for the provisional period scheduler; original action-phase commander timing is a separate adapter.</summary>
public sealed class PlayableTacticOrders(IOrderRules otherCommands, PlayableLandRoutes routes,
    IPlayableTacticRules profiles) : IOrderRules
{
    public CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context)
    {
        if (order is not UseTacticOrder and not CancelTacticUseOrder)
            return otherCommands.Apply(state, force, order, context);
        if (state.Phase != TurnPhase.Strategy)
            return CommandResult.Reject("Tactic queue orders require the strategy phase.", "tactic.phase");
        routes.ValidateWorld(state);
        int unitId = order is UseTacticOrder use ? use.UnitId : ((CancelTacticUseOrder)order).UnitId;
        if (!state.Units.TryGetValue(unitId, out UnitState? unit) || unit.ForceId != force.Id)
            return CommandResult.Reject("Unit does not belong to this force.", "tactic.control");
        if (context.DelegatedDistrictId is int district &&
            (!state.Officers.TryGetValue(unit.OfficerId, out OfficerState? officer) || officer.DistrictId != district))
            return CommandResult.Reject("Unit is outside the delegated district.", "tactic.district");
        if (unit.TacticRuntime is not { } runtime)
            return CommandResult.RuleUnavailable("tactic.runtime", "Unit tactic runtime has not been initialized.");
        if (order is CancelTacticUseOrder)
            return CommandResult.Accept(runtime.Pending is null ? state : state with
            {
                Units = new Dictionary<int, UnitState>(state.Units)
                { [unit.Id] = unit with { TacticRuntime = runtime with { Pending = null } } }
            });
        var requested = (UseTacticOrder)order;
        if (requested.TacticId < 1 || !state.Tactics.ContainsKey(requested.TacticId))
            return CommandResult.Reject("Tactic does not exist.", "tactic.tactic_missing");
        if (unit.TacticIds is not { } equipped || !equipped.Contains(requested.TacticId))
            return CommandResult.Reject("Tactic is not equipped by this unit.", "tactic.not_equipped");
        if (requested.TargetUnitId < 0 || !state.Units.ContainsKey(requested.TargetUnitId))
            return CommandResult.Reject("Target unit does not exist.", "tactic.target_missing");
        PlayableTacticDefinition? definition = profiles is IPlayableTacticCasterSource casterSource
            ? casterSource.ResolveForCaster(state, unit, requested.TacticId)
            : profiles.Resolve(state, requested.TacticId);
        if (definition is null)
            return CommandResult.RuleUnavailable($"tactic.effect.{requested.TacticId}",
                "This tactic has no resolved effect profile.");
        PlayableTacticStateValidation.ValidateDefinition(definition);
        string? rejection = PlayableTacticRuntime.CheckUse(state, unit, requested.TacticId,
            requested.TargetUnitId, state.PeriodNumber, definition, routes, profiles, out string? missing);
        if (rejection is not null)
            return missing is null ? CommandResult.Reject(rejection, "tactic.use") :
                CommandResult.RuleUnavailable(missing, rejection);
        if (runtime.Pending is not null)
            return CommandResult.Reject("Unit already has a queued tactic use.", "tactic.pending");
        QueuedTacticUse pending = new(requested.TacticId, requested.TargetUnitId,
            state.PeriodNumber, definition.ProfileRevision)
        { NativeIssuedAtUpdateOrdinal = definition.NativeActivationWaiting is null ? null : state.NativeClock!.UpdateOrdinal };
        return CommandResult.Accept(state with
        {
            Units = new Dictionary<int, UnitState>(state.Units)
            { [unit.Id] = unit with { TacticRuntime = runtime with { Pending = pending } } }
        });
    }
}
