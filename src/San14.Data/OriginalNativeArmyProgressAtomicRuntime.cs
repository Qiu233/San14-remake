using System.Text.Json;
using San14.Core;

namespace San14.Data;

public enum OriginalNativeArmyProgressBlockOutcome { Committed, StageUnresolvedOrBusy, WaitingUnresolved, SavedCursorAlreadyPassed }
public sealed record OriginalNativeArmyProgressBlockResult(OriginalNativeArmyProgressBlockOutcome Outcome,
    GameState State, NativeArmyProgressStageMoment? BlockedAt,
    OriginalNormalArmyCombatPhaseLocalCapture[] PhaseCaptures,
    OriginalNormalArmyCombatStage13Resolution[] ArmyOnlyResolutions);

/// <summary>Three reached source two-hour cycles form one current six-hour
/// save boundary. Grouping is a save-format adapter, not proof that all32
/// stages or full-world battle are implemented. Every other reached stage
/// requires an explicit owned provider; unknown/busy discards the entire block.</summary>
public static class OriginalNativeArmyProgressAtomicRuntime
{
    public const string Revision = "san14:ce-army-progress-source-stage-atomic-20261003";
    public const int CyclesPerSaveBlock = 3;

    public static bool IsArmyWaitingDue(NativeMapSeasonDate preWriterDate)
    {
        // TC44D684..44D6C9: source signed half, positive, (half+1)%3==0.
        ArgumentNullException.ThrowIfNull(preWriterDate);
        if (preWriterDate.Hour is < 0 or > 22 || (preWriterDate.Hour & 1) != 0)
            throw new InvalidDataException("Ordinary source scheduler requires an even native hour.");
        int half = preWriterDate.Hour / 2;
        return half > 0 && (half + 1) % 3 == 0;
    }

    public static OriginalNativeArmyProgressBlockResult TryAdvanceBlock(GameState input,
        long expectedStartCalendarCycleOrdinal, IPlayableNativeSubdayRules waitingRules,
        IPlayableNativeArmyProgressStageRules stageRules,
        Func<IOriginalNormalArmyCombatPhaseLocalCaptureSource, IOriginalNormalArmyCombatStage13Consumer>? createPhaseConsumer = null,
        OriginalArmyMarchSourceRuntime? movementSource = null)
    {
        ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(waitingRules);
        ArgumentNullException.ThrowIfNull(stageRules);
        NativeSubdayStateValidation.Validate(input);
        NativeArmyMarchStateValidation.Validate(input);
        NativeNormalArmyCombatCalendarStateValidation.Validate(input);
        var season = input.NativeMapSeason ?? throw new InvalidDataException("Source stage orchestration requires its actual World third-date owner.");
        var clock = input.NativeClock ?? throw new InvalidDataException("Source stage orchestration requires its actual Army waiting owner.");
        OriginalMapSeasonRuntime.ValidateOwned(season);
        if (season.ProcessedNativeOrdinal != clock.UpdateOrdinal)
            throw new InvalidDataException("Source stages cannot reconstruct historical state from a calendar catch-up boundary.");
        if (expectedStartCalendarCycleOrdinal < 0 || expectedStartCalendarCycleOrdinal > season.AdvancedCalendarSteps)
            throw new InvalidDataException("Source stage request names a future or invalid saved cursor.");
        ValidateCursor(input.NativeNormalArmyCombatCalendar);
        if (expectedStartCalendarCycleOrdinal < season.AdvancedCalendarSteps)
            return new(OriginalNativeArmyProgressBlockOutcome.SavedCursorAlreadyPassed, input, null, [], []);
        if (clock.UpdatesIntoPeriod == NativeSubdayStateValidation.UpdatesPerPeriod)
            throw new InvalidDataException("Completed native period must pass through its existing period finalizer before another stage block.");
        var cursor = input.NativeNormalArmyCombatCalendar ?? new(OriginalNormalArmyCombatSourcePlanner.ProfileId,
            season.SourceScenarioUri, season.SourceScenarioSha256, season.SourceWorldSha256, clock.Revision,
            season.AdvancedCalendarSteps,
            season.AdvancedCalendarSteps, 0, [], OriginalNormalArmyCombatCalendarRuntime.Revision);
        GameState working = Detach(input);
        var captures = new List<OriginalNormalArmyCombatPhaseLocalCapture>();
        NativeMapSeasonDate date = season.ThirdDate;
        int waitingUpdates = 0;
        for (int cycle = 0; cycle < CyclesPerSaveBlock; cycle++)
        {
            long ordinal = checked(season.AdvancedCalendarSteps + cycle);
            // The source list belongs to this reached stage7/8 pair only.
            // Keep it inside the transaction: no reusable provider-held queue
            // may survive a failed block or supply a later saved request.
            NativeArmyMovementQueue? movementQueue = null;
            for (byte stage = 0; stage < 32; stage++)
            {
                var moment = new NativeArmyProgressStageMoment(ordinal, date, stage, cycle,
                    NativeArmyProgressStageBoundary.StageEntry);
                if (stage == 13)
                {
                    var normalMoment = new OriginalNormalArmyCombatPhaseMoment(ordinal, date, 13,
                        season.SourceScenarioUri, season.SourceScenarioSha256, season.SourceWorldSha256,
                        OriginalNormalArmyCombatCalendarRuntime.Revision);
                    if (OriginalNormalArmyCombatSourcePlanner.IsDueAtSourceCalendarPhase(normalMoment))
                        captures.Add(new(normalMoment, Detach(working), Revision));
                }
                GameState? next;
                if (movementSource is not null && stage == 7)
                {
                    movementQueue = movementSource.PrepareStage7(Detach(working), moment);
                    if (movementQueue is null)
                        return Blocked(OriginalNativeArmyProgressBlockOutcome.StageUnresolvedOrBusy, moment);
                    next = working;
                }
                else if (movementSource is not null && stage == 8)
                {
                    var movement = NativeArmyMovementStage8AtomicRuntime.TryAdvance(working, moment,
                        movementQueue ?? throw new InvalidDataException("Reached movement stage8 lacks its same-cycle stage7 queue."),
                        movementSource);
                    movementQueue = null;
                    next = movement.Complete ? movement.State : null;
                }
                else next = AdvanceProtectedStage(working, stageRules, moment);
                if (next is null) return Blocked(OriginalNativeArmyProgressBlockOutcome.StageUnresolvedOrBusy, moment);
                working = next;
                if (stage == 2 && IsArmyWaitingDue(date))
                {
                    var order = stageRules.ReadCompleteWaitingArmyOrder(Detach(working), moment);
                    if (order is null) return Blocked(OriginalNativeArmyProgressBlockOutcome.WaitingUnresolved, moment);
                    var currentClock = working.NativeClock!;
                    var waitingMoment = new NativeSubdayMoment(currentClock.AnchorDate, checked(currentClock.UpdateOrdinal + 1),
                        currentClock.UpdatesIntoPeriod / 4, date.Hour);
                    next = NativeArmyWaitingStageAtomicRuntime.TryAdvance(working, waitingRules, waitingMoment, order.ToArray());
                    if (next is null) return Blocked(OriginalNativeArmyProgressBlockOutcome.WaitingUnresolved, moment);
                    working = next; waitingUpdates++;
                    moment = moment with { Boundary = NativeArmyProgressStageBoundary.AfterArmyWaiting };
                    next = AdvanceProtectedStage(working, stageRules, moment);
                    if (next is null) return Blocked(OriginalNativeArmyProgressBlockOutcome.StageUnresolvedOrBusy, moment);
                    working = next;
                }
            }
            // TC44DD67 requires stage==32, then33DCC0→33DC50 writes+2.
            date = OriginalMapSeasonRuntime.StepCalendarWriter(date).Date;
        }
        if (waitingUpdates != 1 || working.NativeClock!.UpdateOrdinal != checked(clock.UpdateOrdinal + 1))
            throw new InvalidDataException("Source gate did not reach exactly one Army waiting update in this save boundary.");
        var advancement = OriginalMapSeasonRuntime.AdvanceToOwnedClock(season, working.NativeClock);
        if (!advancement.Available || advancement.CalendarStepsApplied != CyclesPerSaveBlock || advancement.State.ThirdDate != date)
            throw new InvalidDataException("Reached source writers differ from the existing owned calendar save boundary.");

        // Resolve only exact captured snapshots after the whole private block
        // is ready. A later busy stage never publishes a prefix capture.
        var source = new ExactCaptures(captures);
        var consumer = createPhaseConsumer?.Invoke(source);
        var resolutions = new List<OriginalNormalArmyCombatStage13Resolution>();
        var diagnostics = new List<NativeNormalArmyCombatPhaseDiagnostic>();
        foreach (var capture in captures)
        {
            var reached = consumer?.Resolve(capture.Moment) ?? new(NativeNormalArmyCombatPhaseOutcome.ArmyPlannerInputsUnresolved,
                null, "normal.army_planner_inputs_unresolved", Revision);
            if (!Enum.IsDefined(reached.Outcome) || string.IsNullOrWhiteSpace(reached.Reason) || string.IsNullOrWhiteSpace(reached.Revision) ||
                (reached.Outcome == NativeNormalArmyCombatPhaseOutcome.ArmyOnlyPlanResolved ? reached.ArmyOnlyPlan is null : reached.ArmyOnlyPlan is not null) ||
                reached.ArmyOnlyPlan is { } plan && (plan.Moment != capture.Moment || plan.OrderedPairs is null || plan.IncludesBaseOrGateOriginPairs))
                throw new InvalidDataException("Reached source Army phase fabricated battle completeness or belongs to another moment.");
            resolutions.Add(reached);
            diagnostics.Add(new(capture.Moment.CalendarCycleOrdinal, capture.Moment.WorldThirdDate, reached.Outcome,
                reached.ArmyOnlyPlan?.OrderedPairs.Length, reached.Reason, reached.Revision));
        }
        GameState result = working with { NativeMapSeason = advancement.State,
            NativeNormalArmyCombatCalendar = cursor with {
                NextCalendarCycleOrdinal = advancement.State.AdvancedCalendarSteps,
                ReachedDuePhaseCount = checked(cursor.ReachedDuePhaseCount + diagnostics.Count),
                LastAdvanceDiagnostics = diagnostics.ToArray() } };
        NativeSubdayStateValidation.Validate(result);
        NativeArmyMarchStateValidation.Validate(result);
        NativeNormalArmyCombatCalendarStateValidation.Validate(result);
        return new(OriginalNativeArmyProgressBlockOutcome.Committed, Detach(result), null, captures.ToArray(), resolutions.ToArray());

        OriginalNativeArmyProgressBlockResult Blocked(OriginalNativeArmyProgressBlockOutcome outcome, NativeArmyProgressStageMoment at)
            => new(outcome, input, at, [], []);
    }

    private static GameState? AdvanceProtectedStage(GameState state, IPlayableNativeArmyProgressStageRules rules, NativeArmyProgressStageMoment moment)
    {
        var previousDate = state.Date;
        var previousClock = state.NativeClock;
        var previousSeason = state.NativeMapSeason;
        var previousCalendar = state.NativeNormalArmyCombatCalendar;
        var resolution = rules.AdvanceStage(state, moment);
        if (resolution is null) return null;
        if (resolution.State is null || string.IsNullOrWhiteSpace(resolution.SourceProvenance) ||
            resolution.State.Date != previousDate || resolution.State.NativeClock != previousClock ||
            resolution.State.NativeMapSeason != previousSeason || resolution.State.NativeNormalArmyCombatCalendar != previousCalendar)
            throw new InvalidDataException("Owned stage changed the source calendar/cursor or omitted its current provenance.");
        NativeSubdayStateValidation.Validate(resolution.State);
        NativeArmyMarchStateValidation.Validate(resolution.State);
        // A provider may retain its returned mutable dictionaries/arrays. Its
        // earlier stage must not mutate a later owned prefix through that alias.
        return ReferenceEquals(resolution.State, state) ? state : Detach(resolution.State);
    }
    private static void ValidateCursor(NativeNormalArmyCombatCalendarState? cursor)
    {
        if (cursor is not null && (cursor.ProfileId != OriginalNormalArmyCombatSourcePlanner.ProfileId ||
            cursor.Revision != OriginalNormalArmyCombatCalendarRuntime.Revision))
            throw new InvalidDataException("Source stage cursor names another scheduler generation.");
    }
    private static GameState Detach(GameState state) => JsonSerializer.Deserialize<GameState>(JsonSerializer.SerializeToUtf8Bytes(state))
        ?? throw new InvalidDataException("Owned source stage snapshot failed to detach.");
    private sealed class ExactCaptures(IEnumerable<OriginalNormalArmyCombatPhaseLocalCapture> captures) : IOriginalNormalArmyCombatPhaseLocalCaptureSource
    {
        private readonly Dictionary<long, OriginalNormalArmyCombatPhaseLocalCapture> values = captures.ToDictionary(c => c.Moment.CalendarCycleOrdinal);
        public OriginalNormalArmyCombatPhaseLocalCapture? Capture(OriginalNormalArmyCombatPhaseMoment moment)
            => values.TryGetValue(moment.CalendarCycleOrdinal, out var captured) && captured.Moment == moment
                ? captured with { State = Detach(captured.State) } : null;
    }
}
