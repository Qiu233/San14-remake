using San14.Core;

namespace San14.Data;

/// <summary>Sampler owner at the actually captured pre-writer hour10 stage13.
/// The waiting manager has already run in this cycle, while the saved season
/// still names the beginning of the three-cycle block. No saved cursor is
/// rewritten to make those distinct source clocks appear equal.</summary>
public sealed class NativeArmyCombatCycleSampler
{
    private readonly NativeSubdayClock clock;
    private readonly NativeMapSeasonState savedBoundary;
    private readonly TacticSourceSamplerWorld world;
    private NativeArmyCombatCycleSampler(NativeSubdayClock currentClock,
        NativeMapSeasonState boundary, TacticSourceSamplerWorld currentWorld)
    { clock = currentClock; savedBoundary = boundary; world = currentWorld; }

    public static NativeArmyCombatCycleSampler? Create(NormalArmyCombatPhaseLocalCapture capture)
    {
        ArgumentNullException.ThrowIfNull(capture);
        GameState state = capture.State;
        var moment = capture.Moment;
        if (capture.Revision != NativeArmyProgressAtomicRuntime.Revision ||
            state.NativeClock is not { } clock || state.NativeMapSeason is not { } season ||
            state.NativeArmySimulation?.World.SamplerEpoch is not { } epoch ||
            !NormalArmyCombatSourcePlanner.IsDueAtSourceCalendarPhase(moment) ||
            moment.CalendarCycleOrdinal != checked(season.AdvancedCalendarSteps + 2) ||
            clock.UpdateOrdinal != checked(season.ProcessedNativeOrdinal + 1) ||
            clock.Revision != season.NativeClockRevision || epoch.NativeClockRevision != clock.Revision ||
            moment.SourceScenarioUri != season.SourceScenarioUri ||
            moment.SourceScenarioSha256 != season.SourceScenarioSha256 ||
            moment.SourceWorldSha256 != season.SourceWorldSha256 ||
            epoch.SourceScenarioUri != season.SourceScenarioUri || epoch.SourceScenarioSha256 != season.SourceScenarioSha256 ||
            epoch.SourceWorldSha256 != season.SourceWorldSha256) return null;
        MapSeasonRuntime.ValidateOwned(season);
        var first = MapSeasonRuntime.StepCalendarWriter(season.ThirdDate);
        var second = MapSeasonRuntime.StepCalendarWriter(first.Date);
        if (second.Date != moment.WorldThirdDate) return null;
        long advancedDays = checked(season.AdvancedDays + (first.DayAdvanced ? 1 : 0) + (second.DayAdvanced ? 1 : 0));
        return new(clock, season, new(unchecked(epoch.Day450 + (int)advancedDays), epoch.Salt45C));
    }

    public TacticSourceSamplerWorld? Read(GameState current) =>
        current.NativeClock == clock && current.NativeMapSeason == savedBoundary ? world : null;
}
