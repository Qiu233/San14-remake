using San14.Core;

namespace San14.Data;

/// <summary>A consumer must obtain Armies captured at this exact pre-calendar
/// stage. The period-end campaign state is deliberately not an argument.</summary>
public interface IOriginalNormalArmyCombatStage13Consumer
{
    OriginalNormalArmyCombatStage13Resolution Resolve(OriginalNormalArmyCombatPhaseMoment moment);
}

public sealed record OriginalNormalArmyCombatStage13Resolution(
    NativeNormalArmyCombatPhaseOutcome Outcome, OriginalNormalArmyCombatPlan? ArmyOnlyPlan,
    string Reason, string Revision);

/// <summary>An explicit phase owner provides this operation-local capture.
/// Native calendar catch-up cannot manufacture it from the final period state.</summary>
public sealed record OriginalNormalArmyCombatPhaseLocalCapture(
    OriginalNormalArmyCombatPhaseMoment Moment, GameState State, string Revision);

public interface IOriginalNormalArmyCombatPhaseLocalCaptureSource
{
    OriginalNormalArmyCombatPhaseLocalCapture? Capture(OriginalNormalArmyCombatPhaseMoment moment);
}

/// <summary>Connects exact phase-local captures to the existing Army-only
/// selection/sort planner. It never executes 169A40/application or reports
/// unresolved Base/Gate-origin pairs as absent.</summary>
public sealed class OriginalNormalArmyCombatCapturedStage13Consumer : IOriginalNormalArmyCombatStage13Consumer
{
    private readonly IOriginalNormalArmyCombatPhaseLocalCaptureSource captures;
    private readonly Func<GameState, OriginalNormalArmyCombatSourcePlanner?> createCurrentPlanner;
    private readonly string revision;
    public OriginalNormalArmyCombatCapturedStage13Consumer(IOriginalNormalArmyCombatPhaseLocalCaptureSource phaseCaptures,
        Func<GameState, OriginalNormalArmyCombatSourcePlanner?> currentPlannerFactory, string sourceRevision)
    {
        captures = phaseCaptures ?? throw new ArgumentNullException(nameof(phaseCaptures));
        createCurrentPlanner = currentPlannerFactory ?? throw new ArgumentNullException(nameof(currentPlannerFactory));
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRevision); revision = sourceRevision;
    }
    public OriginalNormalArmyCombatStage13Resolution Resolve(OriginalNormalArmyCombatPhaseMoment moment)
    {
        var capture = captures.Capture(moment);
        if (capture is null) return new(NativeNormalArmyCombatPhaseOutcome.PhaseLocalArmyCaptureUnavailable,
            null, "normal.phase_local_army_capture_unavailable", revision);
        if (capture.Moment != moment || string.IsNullOrWhiteSpace(capture.Revision))
            throw new InvalidDataException("Normal Army capture belongs to another native stage13 calendar moment.");
        var planner = createCurrentPlanner(capture.State);
        var plan = planner?.Resolve(capture.State, moment);
        return plan is null
            ? new(NativeNormalArmyCombatPhaseOutcome.ArmyPlannerInputsUnresolved, null,
                "normal.army_planner_inputs_unresolved", revision + ";" + capture.Revision)
            : new(NativeNormalArmyCombatPhaseOutcome.ArmyOnlyPlanResolved, plan,
                "normal.army_only_selection_resolved_base_gate_and_application_unresolved", revision + ";" + capture.Revision);
    }
}

/// <summary>Observes stage13 before each existing World+34 two-hour writer.
/// Its final calendar state is the exact existing AdvanceToOwnedClock result;
/// no alternate calendar or per-period snapshot backfill is introduced.</summary>
public static class OriginalNormalArmyCombatCalendarRuntime
{
    public const string Revision = "san14:ce-normal-army-stage13-calendar-observer-20261003";

    public static GameState AdvanceCampaign(GameState campaign, IOriginalNormalArmyCombatStage13Consumer? phaseConsumer = null,
        Func<GameState, NativeMapSeasonDate, GameState>? beforeCalendarWriter = null)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ValidateSourceCursor(campaign.NativeNormalArmyCombatCalendar);
        if (campaign.NativeMapSeason is not { } previous)
        { NativeNormalArmyCombatCalendarStateValidation.Validate(campaign); return campaign; }
        var advancement = OriginalMapSeasonRuntime.AdvanceToOwnedClock(previous, campaign.NativeClock);
        if (!advancement.Available || advancement.CalendarStepsApplied == 0)
        {
            NativeNormalArmyCombatCalendarStateValidation.Validate(campaign);
            return campaign;
        }
        var cursor = campaign.NativeNormalArmyCombatCalendar ?? new(
            OriginalNormalArmyCombatSourcePlanner.ProfileId, previous.SourceScenarioUri, previous.SourceScenarioSha256,
            previous.SourceWorldSha256, previous.NativeClockRevision,
            previous.AdvancedCalendarSteps, previous.AdvancedCalendarSteps, 0, [], Revision);
        ValidateSourceCursor(cursor);
        NativeNormalArmyCombatCalendarStateValidation.Validate(campaign with { NativeNormalArmyCombatCalendar = cursor });
        long start = cursor.NextCalendarCycleOrdinal;
        var diagnostics = new List<NativeNormalArmyCombatPhaseDiagnostic>();
        NativeMapSeasonDate date = previous.ThirdDate;
        GameState working = campaign;
        for (long offset = 0; offset < advancement.CalendarStepsApplied; offset++)
        {
            if (beforeCalendarWriter is not null) working = beforeCalendarWriter(working, date);
            var moment = new OriginalNormalArmyCombatPhaseMoment(checked(start + offset), date,
                OriginalNormalArmyCombatSourcePlanner.SchedulerStageIndex, previous.SourceScenarioUri,
                previous.SourceScenarioSha256, previous.SourceWorldSha256,
                Revision);
            if (OriginalNormalArmyCombatSourcePlanner.IsDueAtSourceCalendarPhase(moment))
            {
                var reached = phaseConsumer?.Resolve(moment) ?? new OriginalNormalArmyCombatStage13Resolution(
                    NativeNormalArmyCombatPhaseOutcome.PhaseLocalArmyCaptureUnavailable, null,
                    "normal.phase_local_army_capture_unavailable", Revision);
                if (!Enum.IsDefined(reached.Outcome) || string.IsNullOrWhiteSpace(reached.Reason) || string.IsNullOrWhiteSpace(reached.Revision) ||
                    (reached.Outcome == NativeNormalArmyCombatPhaseOutcome.ArmyOnlyPlanResolved ? reached.ArmyOnlyPlan is null : reached.ArmyOnlyPlan is not null) ||
                    reached.ArmyOnlyPlan is { } plan && (plan.Moment != moment || plan.OrderedPairs is null || plan.IncludesBaseOrGateOriginPairs))
                    throw new InvalidDataException("Stage13 consumer returned a different moment or fabricated phase completeness.");
                diagnostics.Add(new(moment.CalendarCycleOrdinal, date, reached.Outcome,
                    reached.ArmyOnlyPlan?.OrderedPairs.Length, reached.Reason, reached.Revision));
            }
            date = OriginalMapSeasonRuntime.StepCalendarWriter(date).Date;
        }
        if (date != advancement.State.ThirdDate)
            throw new InvalidDataException("Stage13 observer diverged from the existing original calendar writer result.");
        GameState result = working with { NativeMapSeason = advancement.State,
            NativeNormalArmyCombatCalendar = cursor with {
                NextCalendarCycleOrdinal = advancement.State.AdvancedCalendarSteps,
                ReachedDuePhaseCount = checked(cursor.ReachedDuePhaseCount + diagnostics.Count),
                LastAdvanceDiagnostics = diagnostics.ToArray() } };
        NativeNormalArmyCombatCalendarStateValidation.Validate(result);
        return result;
    }
    private static void ValidateSourceCursor(NativeNormalArmyCombatCalendarState? cursor)
    {
        if (cursor is not null && (cursor.ProfileId != OriginalNormalArmyCombatSourcePlanner.ProfileId || cursor.Revision != Revision))
            throw new InvalidDataException("Normal phase observer belongs to a different source scheduler/writer generation.");
    }
}
