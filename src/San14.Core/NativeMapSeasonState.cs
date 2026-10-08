namespace San14.Core;

public sealed record NativeMapSeasonDate(short Year, sbyte Month, sbyte Day, sbyte Hour);
public enum NativeMapSeasonPhase { PostCalendarWriter }

/// <summary>Independent native second/third dates and their explicit owned
/// writer cursor. Neither PeriodDate nor a missing clock supplies date values.</summary>
public sealed record NativeMapSeasonState(string SourceScenarioUri, string SourceScenarioSha256,
    string SourceWorldSha256, NativeMapSeasonDate SecondDate, NativeMapSeasonDate ThirdDate,
    long ProcessedNativeOrdinal, string NativeClockRevision, string WriterRevision, NativeMapSeasonPhase Phase,
    long AdvancedCalendarSteps, long AdvancedDays, long AdvancedPeriods, long AdvancedMonths);

public sealed partial record GameState
{
    public NativeMapSeasonState? NativeMapSeason { get; init; }
}

internal static class NativeMapSeasonStateValidation
{
    public static void Validate(GameState state)
    {
        if (state.NativeMapSeason is not { } season) return;
        if (string.IsNullOrWhiteSpace(season.SourceScenarioUri) || !Hash(season.SourceScenarioSha256) ||
            !Hash(season.SourceWorldSha256) || season.SecondDate is null || season.ThirdDate is null ||
            season.ProcessedNativeOrdinal < 0 || string.IsNullOrWhiteSpace(season.NativeClockRevision) ||
            string.IsNullOrWhiteSpace(season.WriterRevision) || season.Phase != NativeMapSeasonPhase.PostCalendarWriter ||
            season.AdvancedCalendarSteps < 0 || season.AdvancedDays < 0 || season.AdvancedPeriods < 0 ||
            season.AdvancedMonths < 0 || state.NativeClock is { } clock &&
                (season.ProcessedNativeOrdinal > clock.UpdateOrdinal || season.NativeClockRevision != clock.Revision) ||
            state.ContentHashes.TryGetValue(season.SourceScenarioUri, out string? scenarioHash) &&
                !season.SourceScenarioSha256.Equals(scenarioHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Owned native map-season source or writer cursor is inconsistent.");
        // Native signed raw dates are preserved exactly. Their reached writer
        // validates calendar operations; loading does not repair unusual source bytes.
    }

    private static bool Hash(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
}
