namespace San14.Core;

public static partial class PlayableTacticRuntime
{
    /// <summary>Explicit initialization for a provisional session. Generic legacy loading must keep null unknown.</summary>
    public static GameState InitializePlayableState(GameState state) => state with
    {
        Units = state.Units.ToDictionary(pair => pair.Key, pair => pair.Value.TacticRuntime is null &&
            pair.Value.Mission?.Kind != UnitMissionKind.Transport
            ? pair.Value with { TacticRuntime = UnitTacticRuntime.Empty() } : pair.Value),
        TacticReports = state.TacticReports ?? []
    };

    /// <summary>Stable unit-ID order, before movement. No automatic trigger or arbitrary tactic balance is installed.</summary>
    public static GameState SettleBeforeMovement(GameState state, int nextPeriod,
        PlayableLandRoutes routes, IPlayableTacticRules profiles,
        out TacticReportEntry[] detailedReports, out PeriodReportEntry[] summaryReports)
        => SettleUses(state, nextPeriod, routes, profiles, null, true,
            out detailedReports, out summaryReports);

    internal static GameState SettleNativeUseAtCurrentTick(GameState state, int unitId,
        PlayableLandRoutes routes, IPlayableTacticRules profiles,
        out TacticReportEntry[] detailedReports, out PeriodReportEntry[] summaryReports)
        => SettleUses(state, checked(state.PeriodNumber + 1), routes, profiles, unitId, false,
            out detailedReports, out summaryReports);

    private static GameState SettleUses(GameState state, int nextPeriod,
        PlayableLandRoutes routes, IPlayableTacticRules profiles, int? selectedUnitId,
        bool periodBoundary, out TacticReportEntry[] detailedReports,
        out PeriodReportEntry[] summaryReports)
    {
        if (nextPeriod != checked(state.PeriodNumber + 1))
            throw new InvalidDataException("Tactic settlement period does not follow this state.");
        routes.ValidateWorld(state);
        PlayableTacticStateValidation.Validate(state);
        bool sourceTargeting = profiles is IPlayableTacticTargetSource;
        // Source queries can fail after inspecting mutable current state. Keep
        // the public runtime caller's complete snapshot owned by that caller.
        if (sourceTargeting || state.NativeClock is not null) state = GameStateSnapshot.Detach(state);
        var units = new Dictionary<int, UnitState>(state.Units);
        var officers = new Dictionary<int, OfficerState>(state.Officers);
        var details = new List<TacticReportEntry>();
        var summaries = new List<PeriodReportEntry>();
        ulong randomState = state.RandomState;
        NativeArmyLifecycleState? lifecycle = state.NativeArmyLifecycle;
        ICampaignResourceState? resourceState = state.ResourceState;

        foreach (UnitState unit in periodBoundary ? units.Values.OrderBy(unit => unit.Id).ToArray() : [])
        {
            if (unit.TacticRuntime is not { } runtime) continue;
            foreach (UnitTacticStatus expired in runtime.Statuses.Where(status => status.ExpiresAtPeriod <= nextPeriod))
                details.Add(new("expired", nextPeriod, unit.ForceId, unit.Id,
                    unit.OfficerId, expired.TacticId, unit.Id, expired.Kind));
            units[unit.Id] = unit with
            {
                TacticRuntime = runtime with
                {
                    ReadyAtPeriod = runtime.ReadyAtPeriod.Where(pair => pair.Value > nextPeriod)
                        .ToDictionary(pair => pair.Key, pair => pair.Value),
                    Statuses = runtime.Statuses.Where(status => status.ExpiresAtPeriod > nextPeriod).ToArray()
                }
            };
        }

        int[] actingIds = selectedUnitId is int selectedActorId ? [selectedActorId] : units.Keys.Order().ToArray();
        foreach (int actingId in actingIds)
        {
            if (!units.TryGetValue(actingId, out UnitState? acting) ||
                acting.TacticRuntime is not { Pending: { } pending } runtime) continue;
            units[acting.Id] = acting with { TacticRuntime = runtime with { Pending = null } };
            acting = units[acting.Id];
            GameState current = state with { Units = units, Officers = officers, RandomState = randomState,
                NativeArmyLifecycle = lifecycle, ResourceState = resourceState };
            PlayableTacticDefinition? definition = profiles is IPlayableTacticCasterSource casterSource
                ? casterSource.ResolveForCaster(current, acting, pending.TacticId)
                : profiles.Resolve(current, pending.TacticId);
            string? reason = null;
            string? missingRuleKey = null;
            if (definition is null) reason = "tactic.effect_unresolved";
            else
            {
                PlayableTacticStateValidation.ValidateDefinition(definition);
                if (!periodBoundary && definition.NativeActivationWaiting is null)
                    throw new InvalidDataException("Current-tick activation requires a resolved native waiting assignment.");
                if (definition.ProfileRevision != pending.ProfileRevision) reason = "tactic.profile_changed";
                else reason = CheckUse(current, acting, pending.TacticId, pending.TargetUnitId,
                    periodBoundary ? nextPeriod : state.PeriodNumber, definition, routes, profiles, out missingRuleKey);
            }
            if (reason is not null)
            {
                if (sourceTargeting && (definition is null || missingRuleKey is not null))
                    throw new InvalidDataException($"Source tactic rule is unavailable: {missingRuleKey ?? reason}.");
                details.Add(new("fizzled", nextPeriod, acting.ForceId, acting.Id,
                    acting.OfficerId, pending.TacticId, pending.TargetUnitId, Reason: reason));
                summaries.Add(new("tactic_fizzled", acting.ForceId, null, acting.OfficerId));
                continue;
            }
            if (definition!.NativeActivationWaiting is not null &&
                pending.NativeIssuedAtUpdateOrdinal != state.NativeClock?.UpdateOrdinal)
                throw new InvalidDataException("Native queued activation has no matching recorded strategy tick.");

            int center = units[pending.TargetUnitId].HexId;
            int[] targets = profiles is IPlayableTacticTargetSource targetSource
                ? (targetSource.ResolveTargetIds(current, acting, units[pending.TargetUnitId], definition!, routes)
                    ?? throw new InvalidDataException("Source tactic target query returned an unresolved collection."))
                    .Distinct().Order().ToArray()
                : units.Values.Where(target =>
                    RelationMatches(current, acting, target, definition!.TargetRelation) &&
                    (definition.TargetRelation != PlayableTacticTargetRelation.Self || target.Id == acting.Id) &&
                    WithinRadius(current, routes, center, target.HexId, definition.AreaRadiusHexes))
                .OrderBy(target => target.Id).Select(target => target.Id).ToArray();
            if (sourceTargeting)
                foreach (int targetId in targets)
                {
                    if (targetId < 0 || !units.TryGetValue(targetId, out UnitState? target) || target.Id != targetId ||
                        target.Troops <= 0 || target.Morale <= 0 || target.HexId < 0 ||
                        target.HexId >= checked(state.MapWidth * state.MapHeight) ||
                        !state.Forces.ContainsKey(target.ForceId) ||
                        !officers.TryGetValue(target.OfficerId, out OfficerState? officer) ||
                        officer.ForceId != target.ForceId || !officer.Deployed ||
                        officer.Presence is not (null or OfficerPresence.Present) || target.TacticRuntime is null)
                        throw new InvalidDataException("Source tactic selected a missing, inactive or unresolved unit.");
                }
            if (targets.Any(id => units[id].TacticRuntime is null))
            {
                details.Add(new("fizzled", nextPeriod, acting.ForceId, acting.Id,
                    acting.OfficerId, pending.TacticId, pending.TargetUnitId,
                    Reason: "tactic.target_runtime_unresolved"));
                summaries.Add(new("tactic_fizzled", acting.ForceId, null, acting.OfficerId));
                continue;
            }
            Dictionary<int, TacticTargetResolution>? preflight = null;
            TacticUseSourceResolution? sourceBatch = null;
            if (profiles is IPlayableTacticUseSource { UsesSourceEffectOrder: true } useSource)
            {
                var stagedUnits = new Dictionary<int, UnitState>(units);
                var stagedOfficers = new Dictionary<int, OfficerState>(officers);
                var stagedSummaries = new List<PeriodReportEntry>();
                NativeArmyLifecycleState? stagedLifecycle = lifecycle;
                ICampaignResourceState? stagedResourceState = resourceState;
                sourceBatch = useSource.ResolveUse(state with
                    { Units = stagedUnits, Officers = stagedOfficers, RandomState = randomState,
                        NativeArmyLifecycle = stagedLifecycle, ResourceState = stagedResourceState },
                    stagedUnits[acting.Id], stagedUnits[pending.TargetUnitId], definition, routes)
                    ?? throw new InvalidDataException("Complete native tactic use inputs are unresolved.");
                if (sourceBatch.Steps is null)
                    throw new InvalidDataException("Native tactic use returned an unresolved instruction sequence.");
                var permittedTargets = targets.ToHashSet();
                foreach (TacticSourceEffectStep step in sourceBatch.Steps)
                {
                    if (step is null || !permittedTargets.Contains(step.TargetUnitId) ||
                        !stagedUnits.TryGetValue(step.TargetUnitId, out UnitState? selected) ||
                        selected.Troops <= 0 || selected.Morale <= 0)
                        throw new InvalidDataException("Native tactic instruction selected an undeclared or inactive unit.");
                    PlayableTacticStateValidation.ValidateDefinition(definition with { Effects = step.Effects });
                    ApplyTargetEffects(step.TargetUnitId, new TacticTargetResolution(step.Effects, step.RandomState) { ResourceStateAfter = step.ResourceStateAfter },
                        stagedUnits, stagedOfficers, null, stagedSummaries, ref stagedLifecycle, ref stagedResourceState);
                }
                ApplyNativeCommanderTail(stagedUnits, stagedOfficers, sourceBatch.RandomState, stagedLifecycle, stagedResourceState);
                PreflightNativeActivation(stagedUnits);
                sourceBatch = sourceBatch with { Steps = sourceBatch.Steps.Select(step => step with
                    { Effects = step.Effects.ToArray() }).ToArray() };
            }
            else if (sourceTargeting)
            {
                preflight = [];
                var stagedUnits = new Dictionary<int, UnitState>(units);
                var stagedOfficers = new Dictionary<int, OfficerState>(officers);
                var stagedSummaries = new List<PeriodReportEntry>();
                NativeArmyLifecycleState? stagedLifecycle = lifecycle;
                ICampaignResourceState? stagedResourceState = resourceState;
                ulong stagedRandom = randomState;
                foreach (int targetId in targets)
                {
                    if (!stagedUnits.TryGetValue(targetId, out UnitState? stagedTarget) ||
                        !stagedUnits.TryGetValue(acting.Id, out UnitState? stagedCaster))
                        throw new InvalidDataException("Source tactic lost a selected unit during effect preflight.");
                    TacticTargetResolution staged = profiles.ResolveEffects(state with
                        { Units = stagedUnits, Officers = stagedOfficers, RandomState = stagedRandom,
                            NativeArmyLifecycle = stagedLifecycle, ResourceState = stagedResourceState },
                        stagedCaster, stagedTarget, definition!)
                        ?? throw new InvalidDataException("Source tactic target inputs are unresolved.");
                    PlayableTacticStateValidation.ValidateDefinition(definition! with { Effects = staged.Effects });
                    staged = staged with { Effects = staged.Effects.ToArray() };
                    preflight.Add(targetId, staged);
                    ApplyTargetEffects(targetId, staged, stagedUnits, stagedOfficers, null, stagedSummaries,
                        ref stagedLifecycle, ref stagedResourceState);
                    stagedRandom = staged.RandomState;
                }
                ApplyNativeCommanderTail(stagedUnits, stagedOfficers, stagedRandom, stagedLifecycle, stagedResourceState);
                PreflightNativeActivation(stagedUnits);
            }
            if (definition!.NativeActivationWaiting is null)
            {
                var cooldowns = new Dictionary<int, int>(units[acting.Id].TacticRuntime!.ReadyAtPeriod);
                cooldowns[pending.TacticId] = checked(nextPeriod + definition.CooldownPeriods);
                units[acting.Id] = units[acting.Id] with
                { TacticRuntime = units[acting.Id].TacticRuntime! with { ReadyAtPeriod = cooldowns } };
            }
            details.Add(new("used", nextPeriod, acting.ForceId, acting.Id,
                acting.OfficerId, pending.TacticId, pending.TargetUnitId)
                { NativeUpdateOrdinal = definition.NativeActivationWaiting is null ? null : state.NativeClock?.UpdateOrdinal });
            summaries.Add(new("tactic_used", acting.ForceId, null, acting.OfficerId,
                pending.TacticId, acting.HexId, center));

            if (sourceBatch is not null)
            {
                foreach (TacticSourceEffectStep step in sourceBatch.Steps)
                {
                    randomState = step.RandomState;
                    ApplyTargetEffects(step.TargetUnitId, new TacticTargetResolution(step.Effects, step.RandomState) { ResourceStateAfter = step.ResourceStateAfter }, units, officers,
                        details, summaries, ref lifecycle, ref resourceState);
                }
                randomState = sourceBatch.RandomState;
            }
            else foreach (int targetId in targets)
            {
                if (!units.TryGetValue(targetId, out UnitState? currentTarget) ||
                    !units.TryGetValue(acting.Id, out UnitState? currentCaster)) continue;
                TacticTargetResolution targetResult = preflight is not null ? preflight[targetId] : profiles.ResolveEffects(state with
                    { Units = units, Officers = officers, RandomState = randomState,
                        NativeArmyLifecycle = lifecycle, ResourceState = resourceState },
                    currentCaster, currentTarget, definition)
                    ?? throw new InvalidDataException("Tactic adapter returned an unresolved target resolution.");
                PlayableTacticStateValidation.ValidateDefinition(definition with { Effects = targetResult.Effects });
                randomState = targetResult.RandomState;
                ApplyTargetEffects(targetId, targetResult, units, officers, details, summaries, ref lifecycle, ref resourceState);
            }
            // Native dispatch executes all effects before assigning the new
            // waiting bytes. Effects read the former 21/22 factor inputs.
            if (definition.NativeActivationWaiting is not null)
            {
                ApplyNativeCommanderTail(units, officers, randomState, lifecycle, resourceState);
                if (!units.TryGetValue(acting.Id, out UnitState? currentCaster))
                    throw new InvalidDataException("Native tactic effects removed the activating Army.");
                UnitState activated = ApplyNativeActivation(currentCaster, definition);
                var cooldowns = new Dictionary<int, int>(activated.TacticRuntime!.ReadyAtPeriod);
                cooldowns.Remove(pending.TacticId);
                units[acting.Id] = activated with
                { TacticRuntime = activated.TacticRuntime with { ReadyAtPeriod = cooldowns } };
            }

            void ApplyNativeCommanderTail(Dictionary<int, UnitState> currentUnits,
                Dictionary<int, OfficerState> currentOfficers, ulong currentRandom,
                NativeArmyLifecycleState? currentLifecycle, ICampaignResourceState? currentResourceState)
            {
                if (definition.NativeActivationWaiting is null || profiles is not IPlayableNativeTacticActivationTailSource
                    { HasNativeActivationCommanderTail: true } tail)
                    return;
                if (!currentUnits.TryGetValue(acting.Id, out UnitState? currentCaster))
                    throw new InvalidDataException("Native tactic effects removed the actor before its commander tail.");
                OfficerState commander = tail.ResolveNativeActivationCommanderTail(state with
                    { Units = currentUnits, Officers = currentOfficers, RandomState = currentRandom,
                        NativeArmyLifecycle = currentLifecycle, ResourceState = currentResourceState }, currentCaster, definition)
                    ?? throw new InvalidDataException("The native activation commander tail has unresolved current inputs.");
                if (commander.Id != currentCaster.OfficerId || !currentOfficers.ContainsKey(commander.Id))
                    throw new InvalidDataException("Native activation tail returned another commander.");
                currentOfficers[commander.Id] = commander;
            }

            void PreflightNativeActivation(Dictionary<int, UnitState> stagedUnits)
            {
                if (definition.NativeActivationWaiting is null) return;
                if (!stagedUnits.TryGetValue(acting.Id, out UnitState? currentCaster))
                    throw new InvalidDataException("Native tactic effects removed the activating Army.");
                // Validate the final assignment only after effect receipts have
                // been staged; no source effect query sees the new counters.
                stagedUnits[acting.Id] = ApplyNativeActivation(currentCaster, definition);
            }

            void ApplyTargetEffects(int targetId, TacticTargetResolution targetResult,
                Dictionary<int, UnitState> targetUnits, Dictionary<int, OfficerState> targetOfficers,
                List<TacticReportEntry>? targetDetails, List<PeriodReportEntry> targetSummaries,
                ref NativeArmyLifecycleState? targetLifecycle, ref ICampaignResourceState? targetResourceState)
            {
                targetResourceState = targetResult.ResourceStateAfter ?? targetResourceState;
                foreach (PlayableTacticEffect effect in targetResult.Effects)
                {
                    if (!targetUnits.TryGetValue(targetId, out UnitState? target)) break;
                    int amount = effect.Amount;
                    int? expiration = null;
                    UnitState changed = target;
                    switch (effect.Kind)
                    {
                        case PlayableTacticEffectKind.ArmyHealing:
                        case PlayableTacticEffectKind.NativeAbnormalChange:
                        case PlayableTacticEffectKind.NativeMoraleChange:
                            changed = ApplyCurrentNativeEffect(state, target, effect);
                            break;
                        case PlayableTacticEffectKind.ArmyDamage:
                            amount = Math.Min(target.Troops, effect.Amount);
                            changed = target with { Troops = target.Troops - amount };
                            break;
                        case PlayableTacticEffectKind.MoraleChange:
                            int morale = checked((int)Math.Clamp((long)target.Morale + effect.Amount, 0, 100));
                            amount = morale - target.Morale;
                            changed = target with { Morale = morale };
                            break;
                        case PlayableTacticEffectKind.ClearHarmfulStatuses:
                        case PlayableTacticEffectKind.ClearAbnormalStatuses:
                            UnitTacticRuntime clearRuntime = target.TacticRuntime!;
                            UnitTacticStatus[] kept = clearRuntime.Statuses.Where(status =>
                                effect.Kind == PlayableTacticEffectKind.ClearAbnormalStatuses
                                    ? !Abnormal(status) : !Harmful(status)).ToArray();
                            amount = clearRuntime.Statuses.Length - kept.Length;
                            changed = target with { TacticRuntime = clearRuntime with { Statuses = kept } };
                            if (effect.NativeAbnormalAfter is not null) changed = ApplyNativeAbnormal(changed, effect);
                            break;
                        default:
                            expiration = checked(nextPeriod + effect.DurationPeriods);
                            UnitTacticRuntime targetRuntime = target.TacticRuntime!;
                            // The explicit period adapter replaces only the same caster/tactic/effect entry.
                            UnitTacticStatus[] statuses = targetRuntime.Statuses.Where(status =>
                                !(status.Kind == effect.Kind && status.SourceUnitId == acting.Id &&
                                  status.TacticId == pending.TacticId)).Append(new UnitTacticStatus(effect.Kind,
                                    effect.Amount, acting.Id, pending.TacticId, expiration.Value,
                                    definition.ProfileRevision)).ToArray();
                            if (statuses.Length > 1024)
                                throw new InvalidDataException("Explicit tactic status capacity was exceeded.");
                            changed = target with { TacticRuntime = targetRuntime with { Statuses = statuses } };
                            break;
                    }
                    targetUnits[targetId] = changed;
                    targetDetails?.Add(new("effect", nextPeriod, acting.ForceId, acting.Id,
                        acting.OfficerId, pending.TacticId, targetId, effect.Kind, amount, expiration)
                        { NativeUpdateOrdinal = definition.NativeActivationWaiting is null ? null : state.NativeClock?.UpdateOrdinal });
                    if (changed.Troops <= 0 || changed.Morale <= 0)
                    {
                        NativeArmyLifecycleState? retiredLifecycle = targetLifecycle;
                        Action<UnitState>? onRetired = retiredLifecycle is null ? null : image =>
                            retiredLifecycle = PlayableNativeArmyRetirement.PreservePendingImage(state with
                                { Units = targetUnits, Officers = targetOfficers,
                                    NativeArmyLifecycle = retiredLifecycle }, image).NativeArmyLifecycle;
                        PlayableUnitAftermath.Destroy(changed, state.Cities, state.Gates,
                            targetOfficers, targetUnits, targetSummaries, state.MapWidth, onRetired);
                        //Each source preflight owns a separate receiver table;
                        //its removals are published only by the actual effect pass.
                        targetLifecycle = retiredLifecycle;
                    }
                }
            }

            UnitState ApplyNativeActivation(UnitState caster, PlayableTacticDefinition profile)
            {
                if (state.NativeClock is not { } clock || caster.NativeTacticRuntime is not { } native ||
                    profile.NativeActivationWaiting is not { } waiting)
                    throw new InvalidDataException("Native tactic activation clock or current Army state is unresolved.");
                return caster with { NativeTacticRuntime = native with
                {
                    Waiting20 = waiting.Waiting20, Waiting21 = waiting.Waiting21, Waiting22 = waiting.Waiting22,
                    PrimaryWaitingReadyAtUpdateOrdinal = waiting.Waiting20 == 0 ? clock.UpdateOrdinal : null
                } };
            }

            UnitState ApplyNativeAbnormal(UnitState target, PlayableTacticEffect effect)
            {
                if (target.NativeTacticRuntime is not { } native || effect.NativeAbnormalAfter is not { } after)
                    throw new InvalidDataException("Current native abnormal state is unresolved.");
                NativeSubdayStateValidation.ValidateOwner(state, after.ProvocationOwner);
                return ArmyOverrideTargetWrites.Apply(target with { NativeTacticRuntime = native with
                {
                    Abnormal23 = after.Abnormal23, Abnormal24 = after.Abnormal24, Abnormal25 = after.Abnormal25,
                    ProvocationOwner = after.ProvocationOwner
                } }, after.OverrideTarget);
            }
        }

        detailedReports = details.ToArray();
        summaryReports = summaries.ToArray();
        return state with { Units = units, Officers = officers,
            TacticReports = periodBoundary ? detailedReports : (state.TacticReports ?? []).Concat(detailedReports).ToArray(),
            RandomState = randomState, NativeArmyLifecycle = lifecycle, ResourceState = resourceState };
    }

    internal static string? CheckUse(GameState state, UnitState acting, int tacticId,
        int targetId, int period, PlayableTacticDefinition definition, PlayableLandRoutes routes,
        IPlayableTacticRules profiles, out string? missingRuleKey)
    {
        missingRuleKey = null;
        if (definition.TacticId != tacticId || !state.Tactics.TryGetValue(tacticId, out TacticDefinition? source) ||
            acting.TacticIds is not { } equipped || !equipped.Contains(tacticId))
            return "tactic.not_equipped";
        if (acting.FormationId is not int formation || source.FormationIds is not { } permitted)
        { missingRuleKey = "tactic.formation"; return "tactic.formation_unresolved"; }
        if (!permitted.Contains(formation)) return "tactic.formation_denied";
        if (acting.TacticRuntime is not { } runtime)
        { missingRuleKey = "tactic.runtime"; return "tactic.runtime_unresolved"; }
        if (acting.Troops <= 0 || acting.Morale <= 0) return "tactic.unit_inactive";
        if (definition.NativeActivationWaiting is not null)
        {
            if (state.NativeClock is null || acting.NativeTacticRuntime is null)
            { missingRuleKey = "tactic.native_runtime"; return "tactic.native_runtime_unresolved"; }
            if (acting.NativeTacticRuntime.Waiting20 > 0) return "tactic.native_waiting";
        }
        else if (runtime.ReadyAtPeriod.GetValueOrDefault(tacticId) > period) return "tactic.cooldown";
        if (!PlayableTacticCombat.CanAttack(acting, period)) return "tactic.confused";
        if (!state.Units.TryGetValue(targetId, out UnitState? target)) return "tactic.target_missing";
        if (target.TacticRuntime is null)
        { missingRuleKey = "tactic.target_runtime"; return "tactic.target_runtime_unresolved"; }
        if (!RelationMatches(state, acting, target, definition.TargetRelation)) return "tactic.target_relation";
        if (!WithinRadius(state, routes, acting.HexId, target.HexId, definition.RangeHexes)) return "tactic.target_range";
        TacticUseEligibility eligibility = profiles.CanUse(state, acting, definition);
        if (eligibility.Allowed is null)
        { missingRuleKey = eligibility.MissingRuleKey ?? "tactic.eligibility"; return eligibility.Reason ?? "tactic.eligibility_unresolved"; }
        if (eligibility.Allowed != true) return eligibility.Reason ?? "tactic.eligibility_denied";
        if (profiles is IPlayableTacticUseSource { UsesSourceEffectOrder: true } useSource)
        {
            TacticUseEligibility targetEligibility = useSource.CanUseTarget(state, acting, target, definition, routes);
            if (targetEligibility.Allowed is null)
            { missingRuleKey = targetEligibility.MissingRuleKey ?? "tactic.current_target_inputs"; return targetEligibility.Reason ?? "tactic.target_inputs_unresolved"; }
            if (targetEligibility.Allowed != true) return targetEligibility.Reason ?? "tactic.target_eligibility_denied";
        }
        return null;
    }

    private static bool RelationMatches(GameState state, UnitState acting, UnitState target,
        PlayableTacticTargetRelation relation) => relation switch
        {
            PlayableTacticTargetRelation.Self => target.Id == acting.Id,
            PlayableTacticTargetRelation.Friendly => target.ForceId == acting.ForceId ||
                PlayableDiplomacy.AreAllied(state, acting.ForceId, target.ForceId),
            PlayableTacticTargetRelation.Hostile => PlayableDiplomacy.IsHostile(state, acting.ForceId, target.ForceId),
            _ => throw new InvalidDataException("Tactic target relation is invalid.")
        };

    private static bool Abnormal(UnitTacticStatus status) => status.Kind is
        PlayableTacticEffectKind.Immobilized or PlayableTacticEffectKind.Confused or
        PlayableTacticEffectKind.Provoked;
    private static bool Harmful(UnitTacticStatus status) => Abnormal(status) || status.Amount < 0;

    private static bool WithinRadius(GameState state, PlayableLandRoutes routes,
        int origin, int target, int radius) => routes.WithinRadius(origin, target,
            Math.Min(radius, checked(state.MapWidth * state.MapHeight) - 1));
}
