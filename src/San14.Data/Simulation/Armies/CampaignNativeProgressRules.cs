using San14.Core;
namespace San14.Data;

/// <summary>Finite resumable stage2 consumer. It cannot supply unimplemented remaining stages or finalize a period.</summary>
public sealed partial class CampaignNativeProgressRules(ArmyCampaignRuleAdapter campaign,
    IPlayableNativeSubdayRules waiting, IPlayableNativeArmyProgressStageSource stages,
    CampaignStoryBindings stories, ArmyMarchSourceRuntime? movement = null,
    IManualTacticPresentationAdmission? manualPresentation = null,
    ICampaignAppRandomBinding? appRandom = null,
    Action? checkCancellation = null)
    : ICampaignManualTacticProgressRules, ICampaignArmyProgressRules
{
    public const string Revision = "san14:campaign-stage2-story-progress-20261018";
    private ICampaignStageOperation? _completedRules;
    // CPU observation only; copies cannot mutate the candidate. No source gate depends on it.
    internal Action<string, GameState>? ObserveTurnCandidate { get; init; }
    // Explicit offline instrumentation may borrow the reached state. The
    // production factory never installs this observer or copies the World.
    internal Action<NormalArmyCombatPhaseMoment, GameState>? ObserveCombatPhase { get; init; }
    public CampaignProgressTransition Begin(GameState candidate, CampaignProgressRequest request, long firstStorySequence)
    {
        try
        {
            request.ValidateNormalPeriod(candidate);
            stories.BeginOperation(CampaignServiceChanges.Empty);
            var season = candidate.NativeMapSeason ?? throw Missing("progress_calendar");
            var clock = candidate.NativeClock ?? throw Missing("progress_waiting_clock");
            MapSeasonRuntime.ValidateOwned(season);
            NativeArmyProgressAtomicRuntime.ValidateCursor(candidate.NativeNormalArmyCombatCalendar);
            if (request.BlockCount is < 1 or > 40 || request.StartCalendarCycleOrdinal != season.AdvancedCalendarSteps ||
                request.BlockCount > 40-clock.UpdatesIntoPeriod || season.ProcessedNativeOrdinal != clock.UpdateOrdinal ||
                season.ThirdDate.Hour % 6 != 0 || firstStorySequence < 1)
                throw Missing("progress_request_boundary");
            var calendar = candidate.NativeNormalArmyCombatCalendar ?? new(NormalArmyCombatSourcePlanner.ProfileId,
                season.SourceScenarioUri, season.SourceScenarioSha256, season.SourceWorldSha256, clock.Revision,
                season.AdvancedCalendarSteps, season.AdvancedCalendarSteps, 0, [], NormalArmyCombatCalendarRuntime.Revision);
            return Drive(candidate, request, 0, calendar, CampaignServiceChanges.Empty, firstStorySequence-1, null);
        }
        catch (InvalidDataException error) { return Unresolved(error); }
    }
    public CampaignProgressTransition Resume(CampaignProgressOperation pending, CampaignStoryStepReceipt receipt)
    {
        if (pending.ContinueAfterPresentation || pending.Revision != Revision || receipt.RequestSequence != pending.Story.Sequence || receipt.Step != pending.Story.NextStep)
            throw Missing("progress_story_receipt");
        return ContinueAccepted(pending with { Story = pending.Story.CompleteStep(receipt), ContinueAfterPresentation = true });
    }
    public CampaignProgressTransition Retry(CampaignProgressOperation pending)
    {
        if (!pending.ContinueAfterPresentation || pending.Revision != Revision) throw Missing("progress_retry_boundary");
        return ContinueAccepted(pending);
    }
    private CampaignProgressTransition ContinueAccepted(CampaignProgressOperation pending)
    {
        var accepted = pending;
        try
        {
            stories.BeginOperation(accepted.ServiceChanges);
            campaign.RequireStorySource(accepted.Story);
            if (accepted.Story.NextStep < accepted.Landmark.EventStepCount)
                return Awaiting(accepted with { ContinueAfterPresentation = false });
            if (!accepted.CurrentLandmarkCompleted && !accepted.CurrentLandmarkServicePrepared)
            {
                var manager = campaign.ResumeLandmarkProgress(accepted, stories);
                if (manager.Story is not null)
                    return Awaiting(accepted with { Candidate = manager.State, Landmark = manager.Checkpoint!, Story = manager.Story,
                        ServiceChanges = manager.ServiceChanges, LastStorySequence = manager.LastSequence, ContinueAfterPresentation = false });
                accepted = accepted with { Candidate = manager.State, ServiceChanges = manager.ServiceChanges,
                    Landmark = manager.Checkpoint!, LastStorySequence = manager.LastSequence };
                accepted = accepted with { ServiceChanges = campaign.PrepareCurrentLandmarkService(accepted, stories),
                    CurrentLandmarkServicePrepared = true };
            }
            if (!accepted.CurrentLandmarkCompleted)
            {
                accepted = accepted with { Candidate = campaign.ApplyCurrentLandmarkRewards(accepted), CurrentLandmarkCompleted = true };
            }
            if (!accepted.LandmarkManagerCompleted)
            {
                var next = campaign.ContinueLandmarkProgress(accepted.Candidate, accepted.Context.Moment, stories,
                    accepted.ServiceChanges, checked(accepted.LastStorySequence+1), accepted.Landmark);
                if (next.Story is not null)
                    return Awaiting(accepted with { Candidate = next.State, Landmark = next.Checkpoint!, Story = next.Story,
                        ServiceChanges = next.ServiceChanges, LastStorySequence = next.LastSequence,
                        CurrentLandmarkCompleted = false, CurrentLandmarkServicePrepared = false, ContinueAfterPresentation = false });
                accepted = accepted with { Candidate = next.State, ServiceChanges = next.ServiceChanges,
                    LastStorySequence = next.LastSequence, LandmarkManagerCompleted = true };
            }
            return Drive(accepted.Candidate, accepted.Request, accepted.BlockIndex, accepted.Calendar,
                accepted.ServiceChanges, accepted.LastStorySequence, accepted);
        }
        catch (InvalidDataException error)
        { return new(CampaignProgressOutcome.Unresolved, null, accepted with
            { ServiceChanges = stories.PreserveClockReads(accepted.ServiceChanges) }, error.Message); }
    }
    private CampaignProgressTransition Drive(GameState working, CampaignProgressRequest request, int firstBlock,
        NativeNormalArmyCombatCalendarState calendar,
        CampaignServiceChanges changes, long lastSequence, CampaignProgressOperation? resume,
        ManualTacticProgressOperation? manualResume = null, CampaignArmyProgressOperation? armyResume = null)
    {
        var rules = stages.BeginOperation() as ICampaignStageOperation ?? throw Missing("resumable_stage_operation");
        if (resume is not null) rules.RestoreCheckpoint(resume.Grouping);
        else if (manualResume is not null) rules.RestoreCheckpoint(manualResume.Grouping);
        else if (armyResume is not null) rules.RestoreCheckpoint(armyResume.Grouping);
        for (int block = firstBlock; block < request.BlockCount; block++)
        {
            checkCancellation?.Invoke();
            bool continuing = resume is not null && block == firstBlock;
            bool continuingManual = manualResume is not null && block == firstBlock;
            bool continuingArmy = armyResume is not null && block == firstBlock;
            byte? resumedArmyStage = continuingArmy ? armyResume!.Context.Moment.SchedulerStageIndex : null;
            var context = continuing ? resume!.Context : continuingManual ? manualResume!.Context :
                continuingArmy ? armyResume!.Context : null;
            var season = context?.BlockStartSeason ?? working.NativeMapSeason!;
            var clock = context?.BlockStartClock ?? working.NativeClock!;
            var date = context?.Moment.PreWriterThirdDate ?? season.ThirdDate;
            int firstCycle = context?.Moment.CycleWithinAtomicBlock ?? 0;
            int waitingUpdates = continuing || (continuingManual || continuingArmy) && firstCycle == 2 ? 1 : 0;
            for (int cycle = firstCycle; cycle < 3; cycle++)
            {
                if (resumedArmyStage == (byte)NativeArmyProgressStage.DuelOpportunity && cycle == firstCycle)
                {
                    if (working.DuelAftermathContinuation is null) throw Missing("stage5_resume_owner");
                }
                else campaign.RequireInactiveDuelServiceForProgress(working);
                ArmyMovementQueue? movementQueue = null;
                ArmyMarchSourceRuntime? cycleMovement = movement;
                LegacyArmyMovementOperation? cycleOperation = null;
                for (byte stage = (byte)(resumedArmyStage.HasValue && cycle == firstCycle ? resumedArmyStage.Value :
                    continuingManual && cycle == firstCycle ? 7 : continuing && cycle == 2 ? 3 : 0); stage < 32; stage++)
                {
                    checkCancellation?.Invoke();
                    var moment = new NativeArmyProgressStageMoment(season.AdvancedCalendarSteps + cycle, date, stage, cycle, NativeArmyProgressStageBoundary.StageEntry);
                    if (stage == 6)
                    {
                        var manual = AdvanceManualStage(working, moment, ManualTacticStagePhase.EnterStage, lastSequence);
                        working = manual.State;
                        lastSequence = Math.Max(lastSequence, working.ManualTacticIntervention!.LastPresentationSequence);
                        if (working.ManualTacticExecutionContinuation is not null)
                            return AwaitingManual(new(Revision, request, block, working, new(season, clock, moment), calendar,
                                manual.Phase, null, lastSequence, [], rules.CaptureCheckpoint(), changes)
                                { ContinueAfterPresentation = false });
                        if (manual.Presentation is not null)
                            return AwaitingManual(new(Revision, request, block, working, new(season, clock, moment), calendar,
                                manual.Phase, new(manual.Presentation.Sequence, manual.Presentation.Kind), lastSequence, [], rules.CaptureCheckpoint(), changes));
                        continue;
                    }
                    if (stage == 13)
                    {
                        var combatMoment = new NormalArmyCombatPhaseMoment(moment.CalendarCycleOrdinal, date, 13,
                            season.SourceScenarioUri, season.SourceScenarioSha256, season.SourceWorldSha256, NormalArmyCombatCalendarRuntime.Revision);
                        if (NormalArmyCombatSourcePlanner.IsDueAtSourceCalendarPhase(combatMoment) &&
                            !(cycle == firstCycle && resumedArmyStage == stage))
                        {
                            calendar = calendar with { ReachedDuePhaseCount = checked(calendar.ReachedDuePhaseCount + 1) };
                            ObserveCombatPhase?.Invoke(combatMoment, working);
                        }
                    }
                    GameState? next;
                    if ((movement is not null || CreateMovement is not null) && stage == 7)
                    {
                        cycleOperation = CreateMovement?.Invoke(working);
                        cycleMovement = cycleOperation?.Runtime ?? movement;
                        movementQueue = cycleOperation is not null
                            ? cycleMovement!.PrepareCurrentStage7(working, moment)
                            : cycleMovement!.PrepareStage7(working, moment);
                        next = movementQueue is null ? null : working;
                    }
                    else if ((cycleMovement is not null || continuingArmy) && stage == 8)
                    {
                        var held = continuingArmy && cycle == firstCycle ? armyResume?.Movement : null;
                        if (held is not null)
                        {
                            cycleOperation = CreateMovement?.Invoke(working) ?? throw Missing("movement_resume_current");
                            working = campaign.ResumeMovementReturnCaptivesCurrent(working,
                                cycleOperation.Current, cycleOperation.Pool);
                            if (working.ArmyReturnCaptiveContinuation is not null)
                                throw Missing("movement_return_suffix_still_pending");
                            movementQueue = held.RestoreQueue(working);
                            cycleMovement = cycleOperation.Runtime;
                        }
                        var result = cycleOperation is not null
                            ? ArmyMovementStage.AdvanceCurrentQueueOwned(working, moment,
                                movementQueue ?? throw Missing("same_cycle_movement_queue"), cycleMovement!, resumed: held)
                            : ArmyMovementStage.TryAdvanceOwned(working, moment,
                                movementQueue ?? throw Missing("same_cycle_movement_queue"), cycleMovement!);
                        var reached = cycleOperation?.Publish(result.State) ?? result.State;
                        if (result.SuspendedMovement is { } suspended)
                            return AwaitingArmyProgress(new(Revision, request, block, reached, new(season, clock, moment),
                                calendar, suspended, lastSequence, [], rules.CaptureCheckpoint(), changes));
                        if (!result.Complete)
                            throw Missing("stage8_unresolved_Army_" + result.UnresolvedArmyId);
                        movementQueue = null; armyResume = null; next = reached;
                    }
                    else next = NativeArmyProgressAtomicRuntime.AdvanceStage(working, rules, moment);
                    working = next ?? throw Missing($"stage_{stage}_unresolved_or_busy");
                    if (stage == (byte)NativeArmyProgressStage.DuelOpportunity && working.DuelAftermathContinuation is not null ||
                        stage == (byte)NativeArmyProgressStage.Combat && working.NormalCombatImmediateContinuation is not null ||
                        stage == (byte)NativeArmyProgressStage.Defeat &&
                        (working.ArmyDefeatContinuation is not null || working.BaseCaptureContinuation is not null ||
                         AutomaticEmperorCaptureLifecycle.HasCaptureTail(working) ||
                         working.ArmyReturnCaptiveContinuation is { Caller: ArmyReturnCaptiveCaller.BaseCapture }))
                        return AwaitingArmyProgress(new(Revision, request, block, working, new(season, clock, moment),
                            calendar, null, lastSequence, [], rules.CaptureCheckpoint(), changes));
                    if (stage == (byte)NativeArmyProgressStage.DuelOpportunity)
                    {
                        armyResume = null;
                        campaign.RequireInactiveDuelServiceForProgress(working);
                    }
                    if (stage != 2 || !NativeArmyProgressAtomicRuntime.IsArmyWaitingDue(date)) continue;
                    var order = rules.ReadCompleteWaitingArmyOrder(working, moment) ?? throw Missing("complete_waiting_order");
                    var currentClock = working.NativeClock!;
                    var waitingMoment = new NativeSubdayMoment(currentClock.AnchorDate, currentClock.UpdateOrdinal+1, currentClock.UpdatesIntoPeriod/4, date.Hour);
                    working = NativeArmyWaitingStageAtomicRuntime.TryAdvanceOwned(working, waiting, waitingMoment, order.ToArray()) ?? throw Missing("waiting_unresolved");
                    waitingUpdates++;
                    moment = moment with { Boundary = NativeArmyProgressStageBoundary.AfterArmyWaiting };
                    working = campaign.TryAdvanceBaseStatusCounters(working, moment).State ?? throw Missing("post_waiting_base_counters");
                    var landmarks = campaign.ContinueLandmarkProgress(working, moment, stories, changes, checked(lastSequence+1));
                    working = landmarks.State; changes = landmarks.ServiceChanges; lastSequence = landmarks.LastSequence;
                    if (landmarks.Story is not null)
                    {
                        if (movementQueue is not null) throw Missing("stage2_movement_queue_not_empty");
                        return Awaiting(new(Revision, request, block, working, new(season, clock, moment), calendar,
                            landmarks.Checkpoint!, landmarks.Story, lastSequence, [], rules.CaptureCheckpoint(), changes));
                    }
                }
                date = MapSeasonRuntime.StepCalendarWriter(date).Date;
            }
            if (waitingUpdates != 1 || working.NativeClock!.UpdateOrdinal != clock.UpdateOrdinal + 1)
                throw Missing("one_waiting_update_per_block");
            var advancement = MapSeasonRuntime.AdvanceToOwnedClock(season, working.NativeClock);
            if (!advancement.Available || advancement.CalendarStepsApplied != 3 || advancement.State.ThirdDate != date)
                throw Missing("calendar_writer_boundary");
            calendar = calendar with { NextCalendarCycleOrdinal = advancement.State.AdvancedCalendarSteps,
                LastAdvanceDiagnostics = [] };
            working = working with { NativeMapSeason = advancement.State, NativeNormalArmyCombatCalendar = calendar };
        }
        // Combat was executed by the shared stage13 consumer. Runtime does
        // not capture/replay Worlds or resolve diagnostic ArmyOnlyPlan proofs.
        working = working with { NativeNormalArmyCombatCalendar = calendar with { LastAdvanceDiagnostics = [] } };
        if (request.NormalPeriod is { } normal)
        {
            var turn = campaign.TryAdvancePersonAppearanceTurnPrefix(working, normal, appRandom,
                stories.Current, stories.ExternalMode, ObserveTurnCandidate);
            if (turn.State is not { } complete)
                return new(CampaignProgressOutcome.Unresolved, null, null,
                    turn.UnresolvedOwner ?? "normal.turn.missing_prefix_result");
            working = complete;
        }
        // The legacy binding owns only an attempt-local profile copy. Actual
        // service-store publication belongs to the adopted World's outbox.
        working = working with { PendingCampaignServices = MergeLegacyServiceChanges(working.PendingCampaignServices, changes) };
        _completedRules = rules;
        return new(CampaignProgressOutcome.Committed, working, null, null)
        { ServiceChanges = changes, LastStorySequence = lastSequence };
    }
    public void Publish(GameState committed, CampaignServiceChanges changes, CampaignGroupingCheckpoint? grouping)
    {
        var service = stories.Materialize(changes);
        var completed = _completedRules ?? throw Missing("completed_progress_rules");
        completed.PublishDerivedState(committed); stories.Publish(service); _completedRules = null;
    }
    private static CampaignServiceChanges MergeLegacyServiceChanges(CampaignServiceChanges old, CampaignServiceChanges added) =>
        new(old.Events.Concat(added.Events).Distinct().ToArray(),
            old.Achievements.Concat(added.Achievements).DistinctBy(value => value.Id).ToArray(), old.Dirty || added.Dirty)
        {
            ClockReads = old.ClockReads.Concat(added.ClockReads).DistinctBy(value => value.Id).ToArray(),
            CompletedScenarios = old.CompletedScenarios.Concat(added.CompletedScenarios).Distinct().ToArray(),
            CompletedEndingTracks = old.CompletedEndingTracks.Concat(added.CompletedEndingTracks).Distinct().ToArray(),
            UnlockedBonusOfficerIndices = old.UnlockedBonusOfficerIndices.Concat(added.UnlockedBonusOfficerIndices).Distinct().ToArray(),
            CompletedTutorialLessons = old.CompletedTutorialLessons.Concat(added.CompletedTutorialLessons).Distinct().ToArray()
        };
    private static CampaignProgressTransition Awaiting(CampaignProgressOperation operation) => new(CampaignProgressOutcome.AwaitingStory, null, operation, null);
    private static CampaignProgressTransition Unresolved(InvalidDataException error) => new(CampaignProgressOutcome.Unresolved, null, null, error.Message);
    private static InvalidDataException Missing(string field) => new("Campaign progress requires " + field + ".");

}
