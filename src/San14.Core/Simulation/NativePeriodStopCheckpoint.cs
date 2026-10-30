namespace San14.Core;

/// <summary>The reached stage of an Army period which ended before its
/// three-cycle calendar block completed. The season retains the last complete
/// block; this checkpoint never advances its calendar or waiting clock.</summary>
public sealed record NativePeriodStopCheckpoint(long CalendarCycleOrdinal,
    NativeMapSeasonDate Date, byte StageIndex);

public sealed partial record GameState
{
    public NativePeriodStopCheckpoint? NativePeriodStop { get; init; }
}

internal static class NativePeriodStopCheckpointValidation
{
    internal static void Validate(GameState state)
    {
        if (state.NativePeriodStop is not { } stop) return;
        if (state.NativePlayableTerminal is null && state.CampaignTurnStop is null ||
            state.Phase is not (TurnPhase.Strategy or TurnPhase.Reporting) ||
            stop.StageIndex is not (5 or 6 or 13 or 25) ||
            state.NativeMapSeason is not { } season || state.NativeClock is not { } clock ||
            stop.CalendarCycleOrdinal < season.AdvancedCalendarSteps ||
            stop.CalendarCycleOrdinal - season.AdvancedCalendarSteps > 2 ||
            stop.Date is null ||
            stop.Date != season.ThirdDate with
            { Hour = (sbyte)(season.ThirdDate.Hour + 2 * (stop.CalendarCycleOrdinal - season.AdvancedCalendarSteps)) } ||
            clock.UpdateOrdinal - season.ProcessedNativeOrdinal !=
                (stop.CalendarCycleOrdinal - season.AdvancedCalendarSteps == 2 ? 1 : 0) ||
            state.NativePeriodContinuation?.Cursor is not null || state.NativeNormalTurnContinuation is not null)
            throw new InvalidDataException("The terminal Army period has no matching reached stage, date or waiting clock.");
    }
}
