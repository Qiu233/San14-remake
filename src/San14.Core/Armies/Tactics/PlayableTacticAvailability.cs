namespace San14.Core;

public enum TacticCommandAvailabilityKind { Unresolved, Ready, Queued, CoolingDown, Disabled }

public sealed record TacticCommandAvailability(int TacticId, string Name,
    TacticCommandAvailabilityKind Kind, string? Reason, int? ReadyAtPeriod,
    PlayableTacticTargetRelation? TargetRelation, int? RangeHexes, int? AreaRadiusHexes,
    string? ProfileRevision);

/// <summary>Client-facing command metadata with explicit source gaps, without synthesizing an effect profile.</summary>
public static class PlayableTacticAvailability
{
    /// <summary>Queries the same unit/target eligibility as the shared command path.</summary>
    public static TacticUseEligibility ForTarget(GameState state, UnitState unit,
        int tacticId, int targetUnitId, IPlayableTacticRules profiles, PlayableLandRoutes routes) =>
        ForTarget(state, unit.Id, unit.ForceId, tacticId, targetUnitId, profiles, routes);

    /// <summary>Resolves a current caster by logical identity without retaining a borrowed unit.</summary>
    public static TacticUseEligibility ForTarget(GameState state, int unitId, int forceId,
        int tacticId, int targetUnitId, IPlayableTacticRules profiles, PlayableLandRoutes routes)
    {
        if (state.Phase != TurnPhase.Strategy) return new(false, "tactic.phase");
        routes.ValidateWorld(state);
        if (!state.Units.TryGetValue(unitId, out UnitState? acting) || acting.ForceId != forceId)
            return new(false, "tactic.unit_missing");
        if (tacticId < 1 || !state.Tactics.ContainsKey(tacticId))
            return new(false, "tactic.tactic_missing");
        if (acting.TacticIds is not { } equipped || !equipped.Contains(tacticId))
            return new(false, "tactic.not_equipped");
        if (acting.TacticRuntime is not { } runtime)
            return new(null, "tactic.runtime_unresolved", "tactic.runtime");
        if (targetUnitId < 0 || !state.Units.ContainsKey(targetUnitId))
            return new(false, "tactic.target_missing");
        PlayableTacticDefinition? definition = profiles is IPlayableTacticCasterSource casterSource
            ? casterSource.ResolveForCaster(state, acting, tacticId)
            : profiles.Resolve(state, tacticId);
        if (definition is null)
            return new(null, "tactic.effect_unresolved", $"tactic.effect.{tacticId}");
        PlayableTacticStateValidation.ValidateDefinition(definition);
        string? reason = PlayableTacticRuntime.CheckUse(state, acting, tacticId,
            targetUnitId, state.PeriodNumber, definition, routes, profiles, out string? missing);
        if (reason is not null) return new(missing is null ? false : null, reason, missing);
        return runtime.Pending is null ? new(true) : new(false, "tactic.pending");
    }

    public static TacticCommandAvailability[] ForUnit(GameState state, UnitState unit,
        IPlayableTacticRules profiles)
    {
        if (unit.TacticIds is null) return [];
        var result = new List<TacticCommandAvailability>(unit.TacticIds.Length);
        foreach (int tacticId in unit.TacticIds)
        {
            if (!state.Tactics.TryGetValue(tacticId, out TacticDefinition? tactic))
                throw new InvalidDataException("Equipped tactic is missing from the catalog.");
            PlayableTacticDefinition? definition = profiles is IPlayableTacticCasterSource casterSource
                ? casterSource.ResolveForCaster(state, unit, tacticId)
                : profiles.Resolve(state, tacticId);
            TacticCommandAvailabilityKind kind;
            string? reason = null;
            int? ready = null;
            if (definition is null)
            { kind = TacticCommandAvailabilityKind.Unresolved; reason = "tactic.effect_unresolved"; }
            else
            {
                PlayableTacticStateValidation.ValidateDefinition(definition);
                if (definition.TacticId != tacticId)
                    throw new InvalidDataException("Resolved tactic profile belongs to another definition.");
                if (unit.TacticRuntime is not { } runtime)
                { kind = TacticCommandAvailabilityKind.Unresolved; reason = "tactic.runtime_unresolved"; }
                else if (runtime.Pending is { } pending && pending.TacticId == tacticId)
                { kind = TacticCommandAvailabilityKind.Queued; reason = "tactic.pending"; }
                else if (definition.NativeActivationWaiting is not null &&
                         (state.NativeClock is null || unit.NativeTacticRuntime is null))
                { kind = TacticCommandAvailabilityKind.Unresolved; reason = "tactic.native_runtime_unresolved"; }
                else if (definition.NativeActivationWaiting is not null && unit.NativeTacticRuntime!.Waiting20 > 0)
                { kind = TacticCommandAvailabilityKind.CoolingDown; reason = "tactic.native_waiting"; }
                else if (definition.NativeActivationWaiting is null &&
                         runtime.ReadyAtPeriod.TryGetValue(tacticId, out int deadline) && deadline > state.PeriodNumber)
                { kind = TacticCommandAvailabilityKind.CoolingDown; ready = deadline; reason = "tactic.cooldown"; }
                else
                {
                    TacticUseEligibility eligibility = profiles.CanUse(state, unit, definition);
                    if (eligibility.Allowed is null)
                    { kind = TacticCommandAvailabilityKind.Unresolved; reason = eligibility.Reason ?? "tactic.eligibility_unresolved"; }
                    else if (state.Phase != TurnPhase.Strategy || eligibility.Allowed != true || unit.Troops <= 0 || unit.Morale <= 0 ||
                             !PlayableTacticCombat.CanAttack(unit, state.PeriodNumber))
                    { kind = TacticCommandAvailabilityKind.Disabled; reason = eligibility.Reason ?? "tactic.unit_inactive"; }
                    else if (unit.FormationId is not int formation || tactic.FormationIds is not { } permitted)
                    { kind = TacticCommandAvailabilityKind.Unresolved; reason = "tactic.formation_unresolved"; }
                    else if (!permitted.Contains(formation))
                    { kind = TacticCommandAvailabilityKind.Disabled; reason = "tactic.formation_denied"; }
                    else if (runtime.Pending is not null)
                    { kind = TacticCommandAvailabilityKind.Disabled; reason = "tactic.pending_other"; }
                    else kind = TacticCommandAvailabilityKind.Ready;
                }
            }
            result.Add(new(tacticId, tactic.Name, kind, reason, ready, definition?.TargetRelation,
                definition?.RangeHexes, definition?.AreaRadiusHexes, definition?.ProfileRevision));
        }
        return result.ToArray();
    }
}
