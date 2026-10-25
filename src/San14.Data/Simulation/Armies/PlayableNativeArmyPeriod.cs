using San14.Core;

namespace San14.Data;

internal sealed record NativeArmyPeriodCompletion(GameState State,
    IPlayableNativeArmyProgressStageRules Stages);

public sealed partial class ArmyCampaignRuleAdapter
{
    //Normal Turn resumes after all forty blocks. Restore only the saved
    //operation's grouping; do not enter Progress or its changed-route prefix.
    internal IPlayableNativeArmyProgressStageRules RestoreCompletedNormalTurnArmyStages(
        GameState state, CampaignGroupingCheckpoint grouping)
    {
        var stages = new OrdinaryArmyCombatStages(this, new NativePeriodRemainder(this),
            new NormalArmyForceResultDefinitions(CaptureWritableTransportRouteCurrent(state)));
        stages.RestoreCheckpoint(grouping);
        return stages;
    }

    /// <summary>Consumes an already private period transaction. Every source
    /// stage runs at its actual pre-writer date; there is no period-end world
    /// replay, reusable stage-ready flag or per-cut full-world copy.</summary>
    internal NativeArmyPeriodCompletion CompleteNativeArmyPeriod(GameState state,
        IPlayableNativeSubdayRules waitingRules)
    {
        var season = state.NativeMapSeason ?? throw PeriodMissing("calendar_owner");
        var clock = state.NativeClock ?? throw PeriodMissing("waiting_owner");
        var resume = state.NativePeriodContinuation?.Cursor;
        long expectedWaiting = season.ProcessedNativeOrdinal;
        if (resume?.StageIndex is (byte)NativeArmyProgressStage.Defeat or (byte)NativeArmyProgressStage.Combat
            or (byte)NativeArmyProgressStage.DuelOpportunity or (byte)NativeArmyProgressStage.ManualTacticIntervention)
        {
            var reachedDate = season.ThirdDate;
            for (int cycle = 0; cycle <= resume.CycleIndex; cycle++)
            {
                if (NativeArmyProgressAtomicRuntime.IsArmyWaitingDue(reachedDate)) expectedWaiting++;
                reachedDate = MapSeasonRuntime.StepCalendarWriter(reachedDate).Date;
            }
        }
        if (expectedWaiting != clock.UpdateOrdinal || season.ThirdDate.Hour % 6 != 0 ||
            clock.UpdatesIntoPeriod is < 0 or > NativeSubdayStateValidation.UpdatesPerPeriod)
            throw PeriodMissing("entry_boundary");
        NativeArmyProgressAtomicRuntime.ValidateCursor(state.NativeNormalArmyCombatCalendar);
        var calendar = state.NativeNormalArmyCombatCalendar ?? new(NormalArmyCombatSourcePlanner.ProfileId,
            season.SourceScenarioUri, season.SourceScenarioSha256, season.SourceWorldSha256,
            clock.Revision, season.AdvancedCalendarSteps, season.AdvancedCalendarSteps,
            0, [], NormalArmyCombatCalendarRuntime.Revision);
        if (calendar.NextCalendarCycleOrdinal != season.AdvancedCalendarSteps)
            throw PeriodMissing("combat_calendar_boundary");

        if (resume is null && state.ReadArmyLifecycle()?.Indexes.ChangedProcessing is not null)
        {
            var changed = CompleteChangedTransportRoutes(state);
            state = changed.State ?? throw PeriodMissing(changed.UnresolvedOwner ?? "changed_route_worker");
        }

        //Existing stage leaves own their current views and publish their pool
        //changes before returning. Movement7/8 and combat13 each borrow one
        //view for their whole actor/pair batch. No stale pool crosses another
        //stage leaf which can replace that pool's saved owners.
        var stages = new OrdinaryArmyCombatStages(this, new NativePeriodRemainder(this),
            new NormalArmyForceResultDefinitions(CaptureWritableTransportRouteCurrent(state)));
        if (resume is not null) stages.RestoreCheckpoint(resume.Grouping);
        var diagnostics = resume?.Diagnostics.ToList() ?? new List<NativeNormalArmyCombatPhaseDiagnostic>();
        GameState working = state;
        _periodPresentation?.Invoke(working);
        while (resume is not null || working.NativeClock!.UpdatesIntoPeriod < NativeSubdayStateValidation.UpdatesPerPeriod)
        {
            season = working.NativeMapSeason!;
            var date = resume?.Date ?? season.ThirdDate;
            int dueBeforeBlock = resume?.DueBeforeBlock ?? diagnostics.Count;
            for (int cycle = resume?.CycleIndex ?? 0; cycle < NativeArmyProgressAtomicRuntime.CyclesPerSaveBlock; cycle++)
            {
                if (resume?.StageIndex != (byte)NativeArmyProgressStage.DuelOpportunity)
                    RequireInactiveDuelServiceForProgress(working);
                else if (working.DuelAftermathContinuation is null)
                    throw PeriodMissing("stage5_resume_owner");
                long ordinal = checked(season.AdvancedCalendarSteps + cycle);
                ArmyMovementQueue? queue = null;
                ArmyMarchSourceRuntime? movement = null;
                SourceWorldContext? movementCurrent = null;
                NativeArmyCombatCycleSampler? sampler = null;
                for (byte stage = resume?.StageIndex ?? 0; stage < (byte)NativeArmyProgressStage.Count; stage++)
                {
                    var moment = new NativeArmyProgressStageMoment(ordinal, date, stage, cycle,
                        NativeArmyProgressStageBoundary.StageEntry);
                    switch ((NativeArmyProgressStage)stage)
                    {
                        case NativeArmyProgressStage.PersonnelReturn:
                            var returned = ContinuePersonReturnCommands(working, date,
                                resume?.PersonnelAfterSlot ?? -1, resume?.AwaitingDiplomacyTaskId);
                            working = returned.State ?? throw PeriodMissing(returned.UnresolvedOwner ?? "stage0_personnel");
                            if (returned.NextAfterSlot is int afterSlot && returned.AwaitingDiplomacyTaskId is int taskId)
                            {
                                var continuation = working.NativePeriodContinuation
                                    ?? throw PeriodMissing("stage0_normal_period_preparation");
                                var cursor = new NativePeriodCursor(cycle, stage, date, afterSlot, taskId,
                                    stages.CaptureCheckpoint(), diagnostics.ToArray(), dueBeforeBlock);
                                return new(working with { NativePeriodContinuation = continuation with { Cursor = cursor } }, stages);
                            }
                            resume = null;
                            working = working with { NativePeriodContinuation = working.NativePeriodContinuation is { } active
                                ? active with { Cursor = null } : null };
                            break;
                        case NativeArmyProgressStage.DuelOpportunity:
                            if (date.Hour != 10) break;
                            working = resume?.StageIndex == (byte)NativeArmyProgressStage.DuelOpportunity
                                ? ResumeDuelAftermathDuringArmyOperation(working, moment)
                                : AdvanceDuelOpportunity(working, moment);
                            if (working.DuelAftermathContinuation is not null)
                            {
                                var suspendedDuel = working.NativePeriodContinuation
                                    ?? throw PeriodMissing("stage5_preparation");
                                var cursor = new NativePeriodCursor(cycle, stage, date, -1, -1,
                                    stages.CaptureCheckpoint(), diagnostics.ToArray(), dueBeforeBlock);
                                return new(working with { NativePeriodContinuation = suspendedDuel with { Cursor = cursor } }, stages);
                            }
                            resume = null;
                            working = working with { NativePeriodContinuation = working.NativePeriodContinuation is { } completedDuel
                                ? completedDuel with { Cursor = null } : null };
                            RequireInactiveDuelServiceForProgress(working);
                            break;
                        case NativeArmyProgressStage.ManualTacticIntervention:
                            var intervened = stages.AdvanceStage(working, moment);
                            working = intervened?.State ?? throw PeriodMissing("stage6_manual_manager");
                            var tacticManager = working.ManualTacticIntervention
                                ?? throw PeriodMissing("stage6_manual_manager_owner");
                            if (tacticManager.EffectBusy && tacticManager.Effects.Length != 0)
                            {
                                //17AA00's busy branch waits for the actual ordinary
                                //effect nodes. Resuming starts at this same call;
                                //the already reached waiting/duel prefix is not replayed.
                                var suspendedTactic = working.NativePeriodContinuation
                                    ?? throw PeriodMissing("stage6_preparation");
                                var cursor = new NativePeriodCursor(cycle, stage, date, -1, -1,
                                    stages.CaptureCheckpoint(), diagnostics.ToArray(), dueBeforeBlock);
                                return new(working with { NativePeriodContinuation = suspendedTactic with { Cursor = cursor } }, stages);
                            }
                            resume = null;
                            working = working with { NativePeriodContinuation = working.NativePeriodContinuation is { } completedTactic
                                ? completedTactic with { Cursor = null } : null };
                            break;
                        case NativeArmyProgressStage.PrepareMovement:
                            movementCurrent = CaptureWritableTransportRouteCurrent(working);
                            var queries = new ArmyMarchCurrentProducer(_repository, this, _map,
                                operationCurrent: movementCurrent);
                            movement = new(queries, new(this, movementCurrent), movementCurrent);
                            queue = movement.PrepareCurrentStage7(working, moment)
                                ?? throw PeriodMissing("stage7_current_inputs");
                            break;
                        case NativeArmyProgressStage.Movement:
                            var moved = ArmyMovementStage.AdvanceCurrentQueueOwned(working, moment,
                                queue ?? throw PeriodMissing("same_cycle_stage7_queue"),
                                movement ?? throw PeriodMissing("same_cycle_movement_rules"));
                            if (!moved.Complete)
                            {
                                //The source runtime names each unresolved rule exit.
                                //Only queue/receiver publication failures reach here.
                                if (moved.UnresolvedArmyId is int failedId && working.Units.TryGetValue(failedId, out var failedArmy))
                                    throw ArmyMarchSourceRuntime.MissingBranch(working, failedArmy, moment,
                                        "stage8.queue_or_receiver_publication");
                                throw PeriodMissing("stage8_queue_missing_actor_Army" + moved.UnresolvedArmyId +
                                    "_cycle" + moment.CalendarCycleOrdinal);
                            }
                            working = moved.State;
                            var pool = movementCurrent!.ReadOwnedCurrentListPool()
                                ?? throw PeriodMissing("stage8_current_pool");
                            working = working.WithArmyLifecycle(working.ReadArmyLifecycle()! with
                                { Indexes = pool.ProjectOwnedRoles() });
                            queue = null; movement = null; movementCurrent = null;
                            break;
                        case NativeArmyProgressStage.Combat:
                            if (date.Hour != 10) break;
                            working = CompleteCurrentArmyCombatCut(working,
                                CaptureWritableTransportRouteCurrent(working), date, ordinal,
                                out int armyPairCount, out sampler);
                            if (working.NormalCombatImmediateContinuation is not null)
                            {
                                var suspendedCombat = working.NativePeriodContinuation
                                    ?? throw PeriodMissing("stage13_preparation");
                                var cursor = new NativePeriodCursor(cycle, stage, date, -1, -1,
                                    stages.CaptureCheckpoint(), diagnostics.ToArray(), dueBeforeBlock);
                                return new(working with { NativePeriodContinuation = suspendedCombat with { Cursor = cursor } }, stages);
                            }
                            resume = null;
                            working = working with { NativePeriodContinuation = working.NativePeriodContinuation is { } completedCombat
                                ? completedCombat with { Cursor = null } : null };
                            //This saved field describes only the real Army
                            //selection count. Application itself ran above,
                            //including every reached mixed participant pair.
                            diagnostics.Add(new(ordinal, date,
                                NativeNormalArmyCombatPhaseOutcome.ArmyOnlyPlanResolved, armyPairCount,
                                "normal.actual_cycle_mixed_application_army_pair_count", Revision));
                            break;
                        case NativeArmyProgressStage.Defeat:
                            var defeated = new OwnedArmyDestructionStage(this).TryConsumeDeferredDefeats(
                                working, sampler?.Read(working), date);
                            working = defeated.State ?? throw PeriodMissing(defeated.UnresolvedOwner ?? "stage25_defeat");
                            var reports = defeated.Receipts.Select(receipt => new NativeSubdayReport("field_destroyed",
                                working.NativeClock!.UpdateOrdinal, receipt.UnitId, receipt.ForceId, receipt.PhysicalSlot));
                            working = working with { NativeSubdayReports = [.. working.NativeSubdayReports ?? [], .. reports] };
                            if (working.ArmyDefeatContinuation is not null || working.BaseCaptureContinuation is not null)
                            {
                                var suspendedPeriod = working.NativePeriodContinuation ?? throw PeriodMissing("stage25_preparation");
                                var cursor = new NativePeriodCursor(cycle, stage, date, -1, -1,
                                    stages.CaptureCheckpoint(), diagnostics.ToArray(), dueBeforeBlock);
                                return new(working with { NativePeriodContinuation = suspendedPeriod with { Cursor = cursor } }, stages);
                            }
                            resume = null;
                            working = working with { NativePeriodContinuation = working.NativePeriodContinuation is { } completedDefeat
                                ? completedDefeat with { Cursor = null } : null };
                            break;
                        case NativeArmyProgressStage.MaintainArmyCommands:
                            var maintained = TryAdvanceArmyCommandMaintenance(working, moment);
                            working = maintained.State ?? throw PeriodMissing(maintained.UnresolvedOwner ?? "stage28_commands");
                            foreach (var check in maintained.Checks)
                            {
                                if (check.UnitId is not int id || !working.Units.TryGetValue(id, out var image) ||
                                    image.MilitaryPlan is not { Completed: false } plan ||
                                    ArmyConfigurationSourceAdapter.ReadAction(working, image) != 13) continue;
                                working = working with { Units = new(working.Units) { [id] = image with
                                    { MilitaryPlan = plan with { Completed = true,
                                        NextWaypointIndex = plan.WaypointHexIds.Length,
                                        IncidentalEnemyId = null, RestingHexId = image.HexId } } } };
                            }
                            break;
                        default:
                            var completed = stages.AdvanceStage(working, moment);
                            working = completed?.State ?? throw PeriodMissing("stage_" + stage + "_" + (NativeArmyProgressStage)stage);
                            break;
                    }
                    if (working.NativePlayableTerminal is not null)
                        throw PeriodMissing("terminal_phase_publication_at_stage_" + stage);
                    if (stage != (byte)NativeArmyProgressStage.Waiting ||
                        !NativeArmyProgressAtomicRuntime.IsArmyWaitingDue(date)) continue;
                    var order = stages.ReadCompleteWaitingArmyOrder(working, moment)
                        ?? throw PeriodMissing("stage2_complete_current_manager");
                    working = NativeSubdayRuntime.AdvanceCurrentManagerOwned(working, waitingRules, order);
                    var after = stages.AdvanceStage(working, moment with
                        { Boundary = NativeArmyProgressStageBoundary.AfterArmyWaiting });
                    working = after?.State ?? throw PeriodMissing("stage2_after_waiting_counters_or_landmark_consumer");
                }
                date = MapSeasonRuntime.StepCalendarWriter(date).Date;
            }
            //Commit only this actual three-cycle clock boundary. No callback
            //then catches up a second calendar or repeats midnight appearance.
            var advanced = MapSeasonRuntime.AdvanceToOwnedClock(season, working.NativeClock);
            if (!advanced.Available || advanced.CalendarStepsApplied != NativeArmyProgressAtomicRuntime.CyclesPerSaveBlock ||
                advanced.State.ThirdDate != date)
                throw PeriodMissing("calendar_writer_boundary");
            calendar = calendar with { NextCalendarCycleOrdinal = advanced.State.AdvancedCalendarSteps,
                ReachedDuePhaseCount = calendar.ReachedDuePhaseCount + diagnostics.Count - dueBeforeBlock,
                LastAdvanceDiagnostics = [] };
            working = working with { NativeMapSeason = advanced.State, NativeNormalArmyCombatCalendar = calendar };
            _periodPresentation?.Invoke(working);
        }
        working = working with { NativeNormalArmyCombatCalendar = calendar with
            { LastAdvanceDiagnostics = diagnostics.ToArray() } };
        return new(working, stages);
    }

    /// <summary>Only stage6 remains outside the existing concrete stage
    /// composition. Already produced display requests stay in their actual
    /// queues. A pending numerical intervention uses the same shared command
    /// executor and immutable admission as the campaign progress path. This
    /// period never manufactures presentation receipts.</summary>
    private sealed class NativePeriodRemainder(ArmyCampaignRuleAdapter campaign) : IStatelessNativeProgressStageRules
    {
        public NativeArmyProgressStageResolution? AdvanceStage(GameState state, NativeArmyProgressStageMoment moment)
        {
            if (moment.SchedulerStageIndex != (byte)NativeArmyProgressStage.ManualTacticIntervention ||
                moment.Boundary != NativeArmyProgressStageBoundary.StageEntry)
                throw PeriodMissing("unowned_stage_" + moment.SchedulerStageIndex);
            var owner = state.ManualTacticIntervention ?? throw PeriodMissing("manual_manager_owner");
            if (owner.EffectBusy)
            {
                if (owner.Effects.Length != 0)
                    return new(state, "army.progress.manual-effects-queued-for-host");
                //The original busy branch clears busy and returns. It does
                //not execute the still-pending request on this same visit.
                return new(state with { ManualTacticIntervention = owner with { EffectBusy = false } },
                    "army.progress.manual-effect-list-completed");
            }
            if (owner.Pending is { } request)
            {
                var intervention = campaign.TryExecuteManualTacticIntervention(state, moment, request,
                    campaign._automaticPresentationAdmission);
                state = intervention.State ?? throw PeriodMissing(intervention.UnresolvedOwner ?? "manual_intervention_execution");
                owner = state.ManualTacticIntervention ?? throw PeriodMissing("manual_intervention_manager");
            }
            return new(state, owner.Directions.Length == 0
                ? "army.progress.manual-manager-no-pending-work"
                : "army.progress.manual-directions-queued-for-host");
        }
        public IReadOnlyList<int>? ReadCompleteWaitingArmyOrder(GameState state, NativeArmyProgressStageMoment moment)
            => null; //The concrete ordinary stage composition owns this query.
    }

    private static InvalidDataException PeriodMissing(string owner) => new("army.production_period." + owner);
}
