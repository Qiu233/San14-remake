using San14.Core;
using static San14.Data.FacilityConstructionDefinitions;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    // Native scheduling retains its exact moment gates. The coarse playable
    // driver borrows only the real committed calendar for one business pass.
    private sealed record FacilityOperation(Func<NativeMapSeasonDate> ReadDate,
        long Revision, Func<GameState, int> ReadCampaignDay)
    {
        public static implicit operator FacilityOperation(NativeArmyProgressStageMoment moment) =>
            new(() => moment.PreWriterThirdDate, moment.CalendarCycleOrdinal,
                state => StatusCampaignDay(state, moment));
        public static FacilityOperation Playable(GameState state) => new(
            () => (state.NativeMapSeason ?? throw Missing("playable_calendar")).ThirdDate,
            state.NativeMapSeason?.AdvancedCalendarSteps ?? throw Missing("playable_calendar"),
            reached => unchecked((reached.NativeArmySimulation?.World.SamplerEpoch
                ?? throw Missing("playable_epoch")).Day450 +
                (int)(reached.NativeMapSeason ?? throw Missing("playable_calendar")).AdvancedDays));
    }
}
