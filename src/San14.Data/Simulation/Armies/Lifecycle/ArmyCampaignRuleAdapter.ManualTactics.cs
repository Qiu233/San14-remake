using San14.Core;

namespace San14.Data;

public interface IManualTacticPresentationAdmission
{
    /// <summary>Presence of the optional user-selection service returned by
    /// 1D3840, not the application window, Composition or campaign session.</summary>
    bool? ApplicationPresent { get; }
    /// <summary>1D2A90's current signed World.BC value, read only when the
    /// optional service exists. This is not a local-player or Force number.</summary>
    int? ApplicationUserIndex { get; }
    /// <summary>168C40's current unique-direction admission. False is an
    /// explicitly rejected route, not a missing camera or settings owner.</summary>
    bool? ReadUniqueDirectionAdmission(GameState state, UnitState actor, int tacticId);
    /// <summary>The same source receiver retained by an entered activation
    /// after Full releases its Unit image. Hosts use the actual source view
    /// lookup; they must not reconstruct an Army or a view from its Hex.</summary>
    bool? ReadUniqueDirectionAdmission(GameState state, int casterUnitId, int armyPhysical, int tacticId) => null;
}

public sealed record ManualTacticExecutionResult(GameState? State, string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>179D90's actual singleton command writer. Target validity is
    /// deliberately deferred until174020; issuing does not perform effects.</summary>
    public ManualTacticExecutionResult TryQueueManualTacticIntervention(GameState state,
        NativeArmyProgressStageMoment reached, ManualTacticRequest request, bool? progressAllowsReplacement)
    {
        try{return TryQueueManualTacticIntervention(state,ReadManualCurrentPeriodCount(state,reached),request,progressAllowsReplacement);}
        catch(InvalidDataException error){return new(null,error.Message);}
    }

    /// <summary>A stopped UI checkpoint already owns its current calendar.
    /// Submission reads that period directly instead of inventing a moment.</summary>
    public ManualTacticExecutionResult TryQueueManualTacticIntervention(GameState state,
        int currentPeriod, ManualTacticRequest request, bool? progressAllowsReplacement)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(request);
        try
        {
            var manager = state.ManualTacticIntervention ?? throw ManualMissing("manager");
            // A Full continuation retains the original request until its
            // destruction decision resumes. It is an execution owner, not
            // another queued command that a host pause may replace.
            if (state.ManualTacticExecutionContinuation is not null) return new(state, null);
            if (manager.Pending is { } old && progressAllowsReplacement != true)
            {
                // 179D90 checks the old Army, tactic and Hex pointers in that
                // order. Any valid pointer blocks a non-paused replacement;
                // an invalid old Army alone does not make the command empty.
                bool occupied = ReadManualArmy(state, old.Actor, out _);
                if (!occupied)
                {
                    var oldDefinitions = CaptureCurrentDefinitions(state) ?? throw ManualMissing("current_definitions");
                    occupied = ManualKnown(Context(oldDefinitions).ReadManualTacticValidity(state, old.TacticId),
                        "previous_pending_tactic_validity");
                    if (!occupied)
                        occupied = old.Target is { Kind: ManualTacticTargetKind.Hex, Id: >= 0 and < 48400 };
                }
                if (occupied)
                {
                    if (progressAllowsReplacement is null) throw ManualMissing("progress_replacement_gate");
                    return new(state, null);
                }
            }
            if (!ReadManualArmy(state, request.Actor, out UnitState? actor)) return new(state, null);
            var current = CaptureCurrentDefinitions(state) ?? throw ManualMissing("current_definitions");
            var context = Context(current);
            if (!ManualKnown(context.ReadManualTacticValidity(state, request.TacticId), "tactic_validity")) return new(state, null);
            if (!ManualKnown(context.ReadManualInterventionEligibility(state, actor!, request.TacticId,
                    currentPeriod), "eligibility")) return new(state, null);
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

    /// <summary>172DE0/174020 execute the already selected command with the
    /// shared ordered actor engine. Source stage6 owns its sampler and a Full
    /// continuation resumes only the retained instruction/settlement tail.</summary>
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
            var saved = state.ManualTacticExecutionContinuation;
            if ((saved?.Request ?? manager.Pending) != request) throw ManualMissing("pending_identity");
            if (saved is not null && (state.ArmyDefeatContinuation?.PendingCapturedRulerTreatment is not null ||
                state.BaseCaptureContinuation?.PendingCapturedRulerTreatment is not null ||
                state.PersonDeathContinuation is not null || state.AutomaticEmperorCapture?.PendingDecision is not null ||
                AutomaticEmperorCaptureLifecycle.IsPresentationPending(state))) return new(state, null);

            UnitState? initialActor = null;
            if (saved is null && !ReadManualArmy(state, request.Actor, out initialActor)) return Cleared(state);
            var current = CaptureWritableTransportRouteCurrent(state);
            if (saved is null)
            {
                var eligibility = Context(current);
                if (!ManualKnown(eligibility.ReadManualTacticValidity(state, request.TacticId), "tactic_validity") ||
                    !ManualKnown(eligibility.ReadManualInterventionEligibility(state, initialActor!, request.TacticId,
                        ReadManualCurrentPeriodCount(state, moment)), "eligibility")) return Cleared(state);
            }
            var sampler = saved is null ? NativeArmyCombatCycleSampler.CreateAtManualIntervention(state, moment)
                : NativeArmyCombatCycleSampler.RestoreManualIntervention(state, moment, saved.SampleDay, saved.SampleSalt);
            if (sampler is null) throw ManualMissing("reached_effect_sampler");
            var sample = sampler.Read(state) ?? throw ManualMissing("reached_sample");
            long revision = moment.CalendarCycleOrdinal;
            var working = state;
            var effects = new List<CampaignEffectRequest>();
            BorrowedAutomaticSelectedApplication applied;
            int selectedHex;
            int? primaryArmy, primaryBase;
            if (saved is not null)
            {
                if (working.ArmyDefeatContinuation is not null)
                    working = ResumeNormalCombatImmediateDefeatCurrent(working, current, moment.PreWriterThirdDate, revision);
                else if (working.BaseCaptureContinuation is { SettlementIndex: -1 } ||
                    working.CityCaptureEmperorTail is { SettlementIndex: -1 } || working.BaseWinnerEmperorTail is { SettlementIndex: -1 })
                {
                    var capture = ResumeImmediateBaseCaptureCurrent(working, current,
                        moment.PreWriterThirdDate, revision, effects);
                    working = capture.State;
                    if (capture.IsPaused) return new(working, null);
                    if (capture.NativeTerminal) return new(working with { ManualTacticExecutionContinuation = null,
                        ManualTacticIntervention = working.ManualTacticIntervention! with { Pending = null } }, null);
                }
                selectedHex = saved.SelectedHexId;
                primaryArmy = saved.PrimaryUnitId; primaryBase = saved.PrimaryBaseLocationId;
                applied = ResumeBorrowedSelectedManual(working, current, Context(current, sampler), saved,
                    sampler, moment.PreWriterThirdDate, revision, effects, presentation);
            }
            else
            {
                var actor = initialActor!;
                var context = Context(current, sampler);
                if (request.Target is not { Kind: ManualTacticTargetKind.Hex } target)
                    throw ManualMissing("selected_Hex_snapshot");
                selectedHex = target.Id;
                var captured = context.CaptureManualLinkedExecutionCurrent(state, current, actor, request.TacticId, selectedHex);
                if (captured is null) return Cleared(state);
                primaryArmy = captured.Primary.UnitId;
                primaryBase = captured.Primary.Kind == 5 ? captured.Primary.PhysicalSlot
                    : captured.Primary.Kind == 6 ? captured.Primary.PhysicalSlot + 51 : null;
                applied = ApplyBorrowedSelectedManual(state, current, context, actor.Id,
                    captured.Primary, captured.LinkedCenter, captured.MainSelected, sampler,
                    moment.PreWriterThirdDate, revision, effects, presentation);
            }
            working = FlushNormalCombatEffects(applied.State, revision, effects);
            var pool = current.ReadOwnedCurrentListPool() ?? throw ManualMissing("shared_pool");
            working = working.WithArmyLifecycle(working.ReadArmyLifecycle()! with { Indexes = pool.ProjectOwnedRoles() });
            if (applied.Continuation is { } execution)
                return new(working with { ManualTacticExecutionContinuation = new(request, selectedHex,
                    primaryArmy, primaryBase, execution, sample.Day450, sample.Salt45C) }, null);
            //172DE0 clears its three pending operands only after174020 really
            //returns. A pause never reaches this cleanup and never reissues it.
            return new(working with { ManualTacticExecutionContinuation = null,
                ManualTacticIntervention = working.ManualTacticIntervention! with { Pending = null } }, null);
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

    private static bool ManualKnown(bool? value, string owner) => value ?? throw ManualMissing(owner);
    private static InvalidDataException ManualMissing(string owner) => new($"manual_tactic.{owner}");
}
