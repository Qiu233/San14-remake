using San14.Core;
namespace San14.Data;

/// <summary>Finite resumable stage2 consumer. It cannot supply unimplemented remaining stages or finalize a period.</summary>
public sealed partial class CampaignNativeProgressRules(ArmyCampaignRuleAdapter campaign,
    IPlayableNativeSubdayRules waiting, IPlayableNativeArmyProgressStageSource stages,
    CampaignStoryBindings stories, ArmyMarchSourceRuntime? movement = null,
    Func<INormalArmyCombatPhaseLocalCaptureSource, INormalArmyCombatStage13Consumer>? createCaptureConsumer = null,
    IManualTacticPresentationAdmission? manualPresentation = null,
    ICampaignAppRandomBinding? appRandom = null)
    : ICampaignManualTacticProgressRules
{
    public const string Revision = "san14:campaign-stage2-story-progress-20261018";
    private ICampaignStageOperation? _completedRules;
    // CPU observation only; copies cannot mutate the candidate. No source gate depends on it.
    internal Action<string, GameState>? ObserveTurnCandidate { get; init; }
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
            return Drive(candidate, request, 0, calendar, [], CampaignServiceChanges.Empty, firstStorySequence-1, null);
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
            return Drive(accepted.Candidate, accepted.Request, accepted.BlockIndex, accepted.Calendar, accepted.Captures,
                accepted.ServiceChanges, accepted.LastStorySequence, accepted);
        }
        catch (InvalidDataException error)
        { return new(CampaignProgressOutcome.Unresolved, null, accepted with
            { ServiceChanges = stories.PreserveClockReads(accepted.ServiceChanges) }, error.Message); }
    }
    private CampaignProgressTransition Drive(GameState working, CampaignProgressRequest request, int firstBlock,
        NativeNormalArmyCombatCalendarState calendar, IReadOnlyList<SavedArmyCombatCapture> priorCaptures,
        CampaignServiceChanges changes, long lastSequence, CampaignProgressOperation? resume, ManualTacticProgressOperation? manualResume = null)
    {
        var rules = stages.BeginOperation() as ICampaignStageOperation ?? throw Missing("resumable_stage_operation");
        if (resume is not null) rules.RestoreCheckpoint(resume.Grouping);
        else if (manualResume is not null) rules.RestoreCheckpoint(manualResume.Grouping);
        var captures = priorCaptures.ToList();
        for (int block = firstBlock; block < request.BlockCount; block++)
        {
            bool continuing = resume is not null && block == firstBlock;
            bool continuingManual = manualResume is not null && block == firstBlock;
            var context = continuing ? resume!.Context : continuingManual ? manualResume!.Context : null;
            var season = context?.BlockStartSeason ?? working.NativeMapSeason!;
            var clock = context?.BlockStartClock ?? working.NativeClock!;
            var date = context?.Moment.PreWriterThirdDate ?? season.ThirdDate;
            int firstCycle = context?.Moment.CycleWithinAtomicBlock ?? 0;
            int waitingUpdates = continuing || continuingManual && firstCycle == 2 ? 1 : 0, captureStart = captures.Count;
            for (int cycle = firstCycle; cycle < 3; cycle++)
            {
                campaign.RequireInactiveDuelServiceForProgress(working);
                ArmyMovementQueue? movementQueue = null;
                for (byte stage = (byte)(continuingManual && cycle == firstCycle ? 7 : continuing && cycle == 2 ? 3 : 0); stage < 32; stage++)
                {
                    var moment = new NativeArmyProgressStageMoment(season.AdvancedCalendarSteps + cycle, date, stage, cycle, NativeArmyProgressStageBoundary.StageEntry);
                    if (stage == 6)
                    {
                        var manual = AdvanceManualStage(working, moment, ManualTacticStagePhase.EnterStage, lastSequence);
                        working = manual.State;
                        lastSequence = Math.Max(lastSequence, working.ManualTacticIntervention!.LastPresentationSequence);
                        if (working.ManualTacticExecutionContinuation is not null)
                            return AwaitingManual(new(Revision, request, block, working, new(season, clock, moment), calendar,
                                manual.Phase, null, lastSequence, captures.ToArray(), rules.CaptureCheckpoint(), changes)
                                { ContinueAfterPresentation = false });
                        if (manual.Presentation is not null)
                            return AwaitingManual(new(Revision, request, block, working, new(season, clock, moment), calendar,
                                manual.Phase, new(manual.Presentation.Sequence, manual.Presentation.Kind), lastSequence, captures.ToArray(), rules.CaptureCheckpoint(), changes));
                        continue;
                    }
                    if (stage == 13)
                    {
                        var captureMoment = new NormalArmyCombatPhaseMoment(moment.CalendarCycleOrdinal, date, 13,
                            season.SourceScenarioUri, season.SourceScenarioSha256, season.SourceWorldSha256, NormalArmyCombatCalendarRuntime.Revision);
                        if (NormalArmyCombatSourcePlanner.IsDueAtSourceCalendarPhase(captureMoment))
                            captures.Add(new(new(season, clock, moment), GameStateCopy.Copy(working)));
                    }
                    GameState? next;
                    if (movement is not null && stage == 7)
                    { movementQueue = movement.PrepareStage7(working, moment); next = movementQueue is null ? null : working; }
                    else if (movement is not null && stage == 8)
                    {
                        var result = ArmyMovementStage.TryAdvanceOwned(working, moment,
                            movementQueue ?? throw Missing("same_cycle_movement_queue"), movement);
                        movementQueue = null; next = result.Complete ? result.State : null;
                    }
                    else next = NativeArmyProgressAtomicRuntime.AdvanceStage(working, rules, moment);
                    working = next ?? throw Missing($"stage_{stage}_unresolved_or_busy");
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
                            landmarks.Checkpoint!, landmarks.Story, lastSequence, captures.ToArray(), rules.CaptureCheckpoint(), changes));
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
                ReachedDuePhaseCount = calendar.ReachedDuePhaseCount + captures.Count-captureStart, LastAdvanceDiagnostics = [] };
            working = working with { NativeMapSeason = advancement.State, NativeNormalArmyCombatCalendar = calendar };
        }
        var source = new SavedCaptures(captures); var consumer = createCaptureConsumer?.Invoke(source);
        var diagnostics = new List<NativeNormalArmyCombatPhaseDiagnostic>();
        foreach (var capture in captures)
        {
            var moment = source.Moment(capture);
            var result = consumer?.Resolve(moment) ?? new(NativeNormalArmyCombatPhaseOutcome.ArmyPlannerInputsUnresolved, null, "normal.army_planner_inputs_unresolved", NativeArmyProgressAtomicRuntime.Revision);
            if (!Enum.IsDefined(result.Outcome) || string.IsNullOrWhiteSpace(result.Reason) || string.IsNullOrWhiteSpace(result.Revision) ||
                (result.Outcome == NativeNormalArmyCombatPhaseOutcome.ArmyOnlyPlanResolved ? result.ArmyOnlyPlan is null : result.ArmyOnlyPlan is not null) ||
                result.ArmyOnlyPlan is { } plan && (plan.Moment != moment || plan.OrderedPairs is null || plan.IncludesBaseOrGateOriginPairs))
                throw Missing("exact_combat_capture_result");
            diagnostics.Add(new(moment.CalendarCycleOrdinal, moment.WorldThirdDate, result.Outcome, result.ArmyOnlyPlan?.OrderedPairs.Length, result.Reason, result.Revision));
        }
        working = working with { NativeNormalArmyCombatCalendar = calendar with { LastAdvanceDiagnostics = diagnostics.ToArray() } };
        if (request.NormalPeriod is { } normal)
        {
            var turn = campaign.TryAdvancePersonAppearanceTurnPrefix(working, normal, appRandom,
                stories.Current, stories.ExternalMode, ObserveTurnCandidate);
            if (turn.State is not { } complete)
                return new(CampaignProgressOutcome.Unresolved, null, null,
                    turn.UnresolvedOwner ?? "normal.turn.missing_prefix_result");
            working = complete;
        }
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
    private static CampaignProgressTransition Awaiting(CampaignProgressOperation operation) => new(CampaignProgressOutcome.AwaitingStory, null, operation, null);
    private static CampaignProgressTransition Unresolved(InvalidDataException error) => new(CampaignProgressOutcome.Unresolved, null, null, error.Message);
    private static InvalidDataException Missing(string field) => new("Campaign progress requires " + field + ".");
    private sealed class SavedCaptures(IReadOnlyList<SavedArmyCombatCapture> captures) : INormalArmyCombatPhaseLocalCaptureSource
    {
        public NormalArmyCombatPhaseMoment Moment(SavedArmyCombatCapture capture)
        {
            var c = capture.Context; var s = c.BlockStartSeason;
            return new(c.Moment.CalendarCycleOrdinal, c.Moment.PreWriterThirdDate, 13, s.SourceScenarioUri,
                s.SourceScenarioSha256, s.SourceWorldSha256, NormalArmyCombatCalendarRuntime.Revision);
        }
        public NormalArmyCombatPhaseLocalCapture? Capture(NormalArmyCombatPhaseMoment moment)
        {
            var capture = captures.SingleOrDefault(c => Moment(c) == moment);
            return capture is null ? null : new(moment, GameStateCopy.Copy(capture.State), NativeArmyProgressAtomicRuntime.Revision);
        }
    }
}
