using San14.Core;

namespace San14.Data;

/// <summary>Sampler owner at the actually captured pre-writer hour10 stage13.
/// The waiting manager has already run in this cycle, while the saved season
/// still names the beginning of the three-cycle block. No saved cursor is
/// rewritten to make those distinct source clocks appear equal.</summary>
public sealed class OriginalNativeArmyCombatCycleSampler
{
    private readonly NativeSubdayClock clock;
    private readonly NativeMapSeasonState savedBoundary;
    private readonly OriginalTacticSourceSamplerWorld world;
    private OriginalNativeArmyCombatCycleSampler(NativeSubdayClock currentClock,
        NativeMapSeasonState boundary, OriginalTacticSourceSamplerWorld currentWorld)
    { clock = currentClock; savedBoundary = boundary; world = currentWorld; }

    public static OriginalNativeArmyCombatCycleSampler? Create(OriginalNormalArmyCombatPhaseLocalCapture capture)
    {
        ArgumentNullException.ThrowIfNull(capture);
        GameState state = capture.State;
        var moment = capture.Moment;
        if (capture.Revision != OriginalNativeArmyProgressAtomicRuntime.Revision ||
            state.NativeClock is not { } clock || state.NativeMapSeason is not { } season ||
            state.NativeArmySimulation?.World.SamplerEpoch is not { } epoch ||
            !OriginalNormalArmyCombatSourcePlanner.IsDueAtSourceCalendarPhase(moment) ||
            moment.CalendarCycleOrdinal != checked(season.AdvancedCalendarSteps + 2) ||
            clock.UpdateOrdinal != checked(season.ProcessedNativeOrdinal + 1) ||
            clock.Revision != season.NativeClockRevision || epoch.NativeClockRevision != clock.Revision ||
            moment.SourceScenarioUri != season.SourceScenarioUri ||
            moment.SourceScenarioSha256 != season.SourceScenarioSha256 ||
            moment.SourceWorldSha256 != season.SourceWorldSha256 ||
            epoch.SourceScenarioUri != season.SourceScenarioUri || epoch.SourceScenarioSha256 != season.SourceScenarioSha256 ||
            epoch.SourceWorldSha256 != season.SourceWorldSha256) return null;
        OriginalMapSeasonRuntime.ValidateOwned(season);
        var first = OriginalMapSeasonRuntime.StepCalendarWriter(season.ThirdDate);
        var second = OriginalMapSeasonRuntime.StepCalendarWriter(first.Date);
        if (second.Date != moment.WorldThirdDate) return null;
        long advancedDays = checked(season.AdvancedDays + (first.DayAdvanced ? 1 : 0) + (second.DayAdvanced ? 1 : 0));
        return new(clock, season, new(unchecked(epoch.Day450 + (int)advancedDays), epoch.Salt45C));
    }

    public OriginalTacticSourceSamplerWorld? Read(GameState current) =>
        current.NativeClock == clock && current.NativeMapSeason == savedBoundary ? world : null;
}
