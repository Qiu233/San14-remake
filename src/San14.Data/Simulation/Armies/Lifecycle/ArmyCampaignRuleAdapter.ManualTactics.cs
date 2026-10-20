using San14.Core;

namespace San14.Data;

public interface IManualTacticPresentationAdmission
{
    bool? ApplicationPresent { get; }
    int? ApplicationUserIndex { get; }
    /// <summary>168C40's current unique-direction admission. False is an
    /// explicitly rejected route, not a missing camera or settings owner.</summary>
    bool? ReadUniqueDirectionAdmission(GameState state, UnitState actor, int tacticId);
}

public sealed record ManualTacticExecutionResult(GameState? State, string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>179D90's actual singleton command writer. Target validity is
    /// deliberately deferred until174020; issuing does not perform effects.</summary>
    public ManualTacticExecutionResult TryQueueManualTacticIntervention(GameState state,
        NativeArmyProgressStageMoment reached, ManualTacticRequest request, bool? progressAllowsReplacement)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(request);
        try
        {
            var manager = state.ManualTacticIntervention ?? throw ManualMissing("manager");
            if (manager.Pending is { } old && progressAllowsReplacement != true)
            {
                //A valid old first pointer already proves the native all-three-
                //invalid expression false, without inspecting later pointers.
                if (ReadManualArmy(state, old.Actor, out _))
                {
                    if (progressAllowsReplacement is null) throw ManualMissing("progress_replacement_gate");
                    return new(state, null);
                }
                throw ManualMissing("previous_pending_pointer_validity");
            }
            if (!ReadManualArmy(state, request.Actor, out UnitState? actor)) return new(state, null);
            var current = CaptureCurrentDefinitions(state) ?? throw ManualMissing("current_definitions");
            var context = Context(current);
            if (!ManualKnown(context.ReadManualTacticValidity(state, request.TacticId), "tactic_validity")) return new(state, null);
            if (!ManualKnown(context.ReadManualInterventionEligibility(state, actor!, request.TacticId,
                    ReadManualCurrentPeriodCount(state, reached)), "eligibility")) return new(state, null);
            //88DB00 publishes the selected Hex pointer. An Army is only the
            //command's input intent; later movement cannot retarget that pointer.
            //Reading its current Hex here does not admit/reject target validity.
            ManualTacticTarget? target = request.Target;
            if (target is { Kind: ManualTacticTargetKind.Army })
            {
                var receiver = state.ArmyLifecycle?.Receivers.Find(new(target.Id))
                    ?? throw ManualMissing("issue_target_receiver");
                UnitState? image = receiver.ActiveUnitId is int id ? state.Units.GetValueOrDefault(id) : receiver.PendingImage;
                if (image is null || image.HexId is < 0 or >= 48400) throw ManualMissing("issue_target_hex");
                target = new(ManualTacticTargetKind.Hex, image.HexId);
            }
            else if (target is { Kind: ManualTacticTargetKind.BaseLocation })
                throw ManualMissing("issue_base_hex");
            return new(state with { ManualTacticIntervention = manager with { Pending = request with { Target = target } } }, null);
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }

    /// <summary>172DE0 and the finite zero-chain Army nondamage174020 path.
    /// No Unit.Pending, ordinary automatic profile gate or target sorting is used.
    /// The enclosing scheduler owns effect/Direction waits and publication.</summary>
    public ManualTacticExecutionResult TryExecuteManualTacticIntervention(GameState state,
        NativeArmyProgressStageMoment moment, ManualTacticRequest request,
        IManualTacticPresentationAdmission? presentation)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(request);
        try
        {
            if (moment.SchedulerStageIndex != (byte)NativeArmyProgressStage.ManualTacticIntervention ||
                moment.Boundary != NativeArmyProgressStageBoundary.StageEntry)
                throw ManualMissing("stage6_moment");
            var manager = state.ManualTacticIntervention ?? throw ManualMissing("manager");
            if (manager.Pending != request) throw ManualMissing("pending_identity");
            if (!ReadManualArmy(state, request.Actor, out UnitState? actor)) return Cleared(state);
            var current = CaptureCurrentDefinitions(state) ?? throw ManualMissing("current_definitions");
            var context = Context(current);
            if (!ManualKnown(context.ReadManualTacticValidity(state, request.TacticId), "tactic_validity")) return Cleared(state);
            if (!ManualKnown(context.ReadManualInterventionEligibility(state, actor!, request.TacticId,
                    ReadManualCurrentPeriodCount(state, moment)), "eligibility")) return Cleared(state);

            //174020 can reject the explicit target after172DE0's eligibility;
            //the caller still consumes the pending request in that known case.
            if (!ReadManualPrimary(state, context, request.Target, out UnitState? primary)) return Cleared(state);
            if (context.CaptureManualInterventionUse(state, actor!, primary!, request.TacticId) is not { } use)
                throw ManualMissing("instruction_targets");
            if (use.Instructions.All(instruction => instruction.OrderedArmyIds.Length == 0)) return Cleared(state);
            var sampler = NativeArmyCombatCycleSampler.CreateAtManualIntervention(state, moment)
                ?? throw ManualMissing("reached_effect_sampler");
            context = Context(current, sampler);
            if (!context.HasManualEmptyChain(state, actor!, request.TacticId)) throw ManualMissing("linked_actors");
            if (context.ResolveManualActivationWaiting(state, actor!, request.TacticId) is not { } waiting)
                throw ManualMissing("waiting");
            if (context.ResolveManualCapturedEffects(state, actor!, request.TacticId, use) is not { } effects)
                throw ManualMissing("ordered_effects");

            GameState working = GameStateCopy.Copy(state);
            var units = new Dictionary<int, UnitState>(working.Units);
            ICampaignResourceState? resources = working.ResourceState;
            var orderedTargets = use.Program.Instructions.SelectMany(instruction =>
                use.Instructions[instruction.Index].OrderedArmyIds.Select(id => (instruction.Index, Id: id))).ToArray();
            if (orderedTargets.Length != effects.Steps.Length) throw ManualMissing("effect_instruction_order");
            var presentationEffects = new List<TacticEffectPresentation>();
            for (int ordinal = 0; ordinal < effects.Steps.Length; ordinal++)
            {
                var step = effects.Steps[ordinal];
                if (step.TargetUnitId != orderedTargets[ordinal].Id) throw ManualMissing("effect_instruction_order");
                if (!units.TryGetValue(step.TargetUnitId, out UnitState? target)) throw ManualMissing("effect_target");
                var targetReceiver = working.ArmyLifecycle?.Receivers.FindUnit(target.Id)?.ReceiverId
                    ?? throw ManualMissing("effect_target_receiver");
                foreach (var effect in step.Effects)
                {
                    target = ApplyManualEffectReceipt(target, effect);
                    presentationEffects.Add(new(orderedTargets[ordinal].Index, targetReceiver, effect.Kind, effect.Amount,
                        effect.HealingResult, effect.NativeAbnormalAfter, effect.CurrentMoraleAfter));
                }
                if (target.Troops <= 0 || target.MoraleState.CurrentValue is not > 0)
                    throw ManualMissing("effect_retirement");
                units[target.Id] = target;
                resources = step.ResourceStateAfter ?? resources;
            }
            working = working with { Units = units, RandomState = effects.RandomState, ResourceState = resources };
            var commander = context.ResolveOrdinaryActivationCommanderTail(working, units[actor!.Id],
                request.TacticId, TryApplyNativePendingMerit) ?? throw ManualMissing("commander_merit_tail");
            working = working with { Officers = new(working.Officers) { [commander.Id] = commander } };
            //The zero-chain branch skips32FED0, but31FE40 still needs the
            //actual App selection. This finite path owns only its proven skip.
            var admission = presentation ?? throw ManualMissing("presentation_admission");
            if (ManualKnown(admission.ApplicationPresent, "application_presence"))
            {
                if (admission.ApplicationUserIndex is not int selected) throw ManualMissing("application_user_index");
                if (selected != -1) throw ManualMissing("application_selected_notification");
            }
            UnitState currentActor = units[actor.Id];
            var native = currentActor.NativeTacticRuntime ?? throw ManualMissing("actor_waiting");
            units[actor.Id] = currentActor with { NativeTacticRuntime = native with
            {
                Waiting20 = waiting.Waiting20, Waiting21 = waiting.Waiting21, Waiting22 = waiting.Waiting22,
                PrimaryWaitingReadyAtUpdateOrdinal = waiting.Waiting20 == 0 ? working.NativeClock!.UpdateOrdinal : null
            } };
            working = working with { Units = units };

            manager = working.ManualTacticIntervention! with { Pending = null };
            //168C40 checks the ordinary effect count before either route.
            //The finite nondamage callbacks never set the independent busy98.
            if (manager.Effects.Length < 563)
            {
                bool direction = ManualKnown(admission.ReadUniqueDirectionAdmission(working, units[actor.Id],
                    request.TacticId), "unique_direction_admission");
                var vectors = use.Instructions.Select(instruction => instruction.OrderedArmyIds.Select(id =>
                    working.ArmyLifecycle?.Receivers.FindUnit(id)?.ReceiverId.Value
                        ?? throw ManualMissing("presentation_target_receiver")).ToArray()).ToArray();
                manager = manager.Enqueue(direction ? TacticPresentationKind.UniqueDirection : TacticPresentationKind.OrdinaryEffects,
                    [new(request.Actor, request.TacticId, request.Target, vectors) { Effects = presentationEffects.ToArray() }]);
            }
            return new(working with { ManualTacticIntervention = manager }, null);
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }

    private static ManualTacticExecutionResult Cleared(GameState state) => new(state with
        { ManualTacticIntervention = state.ManualTacticIntervention! with { Pending = null } }, null);

    private static bool ReadManualArmy(GameState state, ArmyReceiverId receiver, out UnitState? unit)
    {
        unit = null;
        var arena = state.ReadArmyLifecycle() ?? throw ManualMissing("army_allocation");
        var source = arena.ReceiverSlots().SingleOrDefault(slot => slot.ReceiverId == receiver)
            ?? throw ManualMissing("army_receiver");
        if (source.PhysicalSlot == 0 || source.Commander12 == 0) return false;
        if (source.Commander12 is null || source.Status10 is null) throw ManualMissing("army_validity");
        if (source.Status10 == 0) return false;
        if (source.ActiveUnitId is not int id || !state.Units.TryGetValue(id, out unit) ||
            unit.NativeArmyOrigin?.NativeSlotIndex != source.PhysicalSlot) throw ManualMissing("active_army");
        return true;
    }

    private static bool ReadManualPrimary(GameState state, TacticRemakeCurrentContext context,
        ManualTacticTarget? target, out UnitState? primary)
    {
        primary = null;
        if (target is null) return false;
        if (target.Kind == ManualTacticTargetKind.Hex)
            return ManualKnown(context.ResolveManualPrimaryHex(state, target.Id, out primary), "primary_hex");
        //The issuing owner canonicalizes supported intentions to a Hex. A
        //restored non-Hex request cannot invent the old selected Hex snapshot.
        throw ManualMissing("non_hex_primary_snapshot");
    }

    private int ReadManualCurrentPeriodCount(GameState state, NativeArmyProgressStageMoment moment)
    {
        var world = state.CampaignEventWorld ?? throw ManualMissing("world_period_offset");
        var season = state.NativeMapSeason ?? throw ManualMissing("world_calendar");
        if (season.SourceScenarioUri != _scenario.ScenarioSource.ResourceUri ||
            season.SourceScenarioSha256 != _scenario.ScenarioSource.Sha256 ||
            season.SourceWorldSha256 != _scenario.Snapshot.WorldParameters?.WorldSha256 ||
            moment.CycleWithinAtomicBlock is < 0 or >= NativeArmyProgressAtomicRuntime.CyclesPerSaveBlock ||
            moment.CalendarCycleOrdinal != checked(season.AdvancedCalendarSteps + moment.CycleWithinAtomicBlock))
            throw ManualMissing("reached_calendar");
        var date = season.ThirdDate;
        long periods = season.AdvancedPeriods;
        for (int index = 0; index < moment.CycleWithinAtomicBlock; index++)
        {
            var advanced = MapSeasonRuntime.StepCalendarWriter(date); date = advanced.Date;
            if (advanced.PeriodAdvanced) periods++;
        }
        if (date != moment.PreWriterThirdDate) throw ManualMissing("reached_date");
        return unchecked(world.ElapsedPeriodOffset + (int)periods);
    }

    private static UnitState ApplyManualEffectReceipt(UnitState target, PlayableTacticEffect effect)
    {
        if (effect.Kind == PlayableTacticEffectKind.ArmyHealing && effect.HealingResult is { } healing)
            return target with { Troops = healing.TroopsAfter, WoundedTroops = healing.WoundedAfter };
        if (effect.Kind == PlayableTacticEffectKind.NativeMoraleChange && effect.CurrentMoraleAfter is int morale)
            return target with { MoraleState = target.MoraleState with { CurrentValue = morale } };
        if (effect.Kind is not (PlayableTacticEffectKind.NativeAbnormalChange or PlayableTacticEffectKind.ClearAbnormalStatuses) ||
            effect.NativeAbnormalAfter is not { } after || target.NativeTacticRuntime is not { } native)
            throw ManualMissing("unsupported_effect");
        var result = ArmyOverrideTargetWrites.Apply(target with { NativeTacticRuntime = native with
        { Abnormal23 = after.Abnormal23, Abnormal24 = after.Abnormal24, Abnormal25 = after.Abnormal25,
            ProvocationOwner = after.ProvocationOwner } }, after.OverrideTarget);
        if (effect.Kind == PlayableTacticEffectKind.ClearAbnormalStatuses)
        {
            if (result.TacticRuntime is not { } runtime) throw ManualMissing("abnormal_status_owner");
            result = result with { TacticRuntime = runtime with { Statuses = runtime.Statuses.Where(status =>
                status.Kind is not (PlayableTacticEffectKind.Confused or PlayableTacticEffectKind.Immobilized or
                    PlayableTacticEffectKind.Provoked)).ToArray() } };
        }
        return result;
    }

    private static bool ManualKnown(bool? value, string owner) => value ?? throw ManualMissing(owner);
    private static InvalidDataException ManualMissing(string owner) => new($"manual_tactic.{owner}");
}
