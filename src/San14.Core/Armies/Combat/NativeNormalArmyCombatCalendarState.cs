namespace San14.Core;

public enum NativeNormalArmyCombatPhaseOutcome
{
    ArmyOnlyPlanResolved,
    PhaseLocalArmyCaptureUnavailable,
    ArmyPlannerInputsUnresolved
}

/// <summary>Diagnostic stage13 selection only. No losses, aftermath, Base or
/// Gate completeness, or successful full-world combat execution is implied.</summary>
public sealed record NativeNormalArmyCombatPhaseDiagnostic(long CalendarCycleOrdinal,
    NativeMapSeasonDate PreWriterThirdDate, NativeNormalArmyCombatPhaseOutcome Outcome,
    int? OrderedArmyPairCount, string Reason, string SourceRevision);

/// <summary>The exact calendar boundary observed by this stage13 consumer.
/// StartCycle records first attachment; older phases are not replayed or
/// inferred from a period-end Army snapshot.</summary>
public sealed record NativeNormalArmyCombatCalendarState(string ProfileId,
    string SourceScenarioUri, string SourceScenarioSha256, string SourceWorldSha256,
    string NativeClockRevision, long StartCalendarCycleOrdinal,
    long NextCalendarCycleOrdinal, long ReachedDuePhaseCount,
    NativeNormalArmyCombatPhaseDiagnostic[] LastAdvanceDiagnostics, string Revision);

public sealed partial record GameState
{
    public NativeNormalArmyCombatCalendarState? NativeNormalArmyCombatCalendar { get; init; }
}

public static class NativeNormalArmyCombatCalendarStateValidation
{
    public static void Validate(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.NativeNormalArmyCombatCalendar is not { } cursor) return;
        if (state.NativeMapSeason is not { } season || state.NativeClock is not { } clock ||
            string.IsNullOrWhiteSpace(cursor.ProfileId) || string.IsNullOrWhiteSpace(cursor.Revision) ||
            cursor.SourceScenarioUri != season.SourceScenarioUri || cursor.SourceScenarioSha256 != season.SourceScenarioSha256 ||
            cursor.SourceWorldSha256 != season.SourceWorldSha256 || cursor.NativeClockRevision != clock.Revision ||
            cursor.StartCalendarCycleOrdinal < 0 || cursor.NextCalendarCycleOrdinal < cursor.StartCalendarCycleOrdinal ||
            cursor.NextCalendarCycleOrdinal != season.AdvancedCalendarSteps || cursor.ReachedDuePhaseCount < 0 ||
            cursor.ReachedDuePhaseCount > cursor.NextCalendarCycleOrdinal - cursor.StartCalendarCycleOrdinal ||
            cursor.LastAdvanceDiagnostics is null ||
            cursor.ReachedDuePhaseCount < cursor.LastAdvanceDiagnostics.Length ||
            cursor.LastAdvanceDiagnostics.Any(d => d is null || d.CalendarCycleOrdinal < cursor.StartCalendarCycleOrdinal ||
                d.CalendarCycleOrdinal >= cursor.NextCalendarCycleOrdinal ||
                d.PreWriterThirdDate is not { Month: >= 1 and <= 12, Day: >= 1 and <= 30, Hour: 10 } ||
                !Enum.IsDefined(d.Outcome) || string.IsNullOrWhiteSpace(d.Reason) || string.IsNullOrWhiteSpace(d.SourceRevision) ||
                (d.Outcome == NativeNormalArmyCombatPhaseOutcome.ArmyOnlyPlanResolved ? d.OrderedArmyPairCount is null or < 0 : d.OrderedArmyPairCount is not null)) ||
            cursor.LastAdvanceDiagnostics.Zip(cursor.LastAdvanceDiagnostics.Skip(1),
                (left, right) => left.CalendarCycleOrdinal >= right.CalendarCycleOrdinal).Any(invalid => invalid))
            throw new InvalidDataException("Normal Army phase cursor does not belong to its exact calendar generation or contains invalid stage diagnostics.");
    }
}
