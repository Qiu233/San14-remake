using System.Collections.Immutable;
using System.Globalization;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //42ACD0 is reached only under the Turn's prepared annual predicate.
    //The chronology entry and the year-limit announcements are distinct paths.
    private GameState ApplyNormalTurnAnnualCalendarCurrent(GameState state,
        SourceWorldContext current, out bool stop)
    {
        int year = state.NativeMapSeason?.ThirdDate.Year
            ?? throw AppearanceTurnMissing("annual_calendar_year");
        stop = false;
        if (year < 351)
        {
            int day = (ReadArmyDestructionSampler(state, current)
                ?? throw AppearanceTurnMissing("annual_chronicle_World450")).Day450;
            //The native format is %d, without a year suffix. Header field7=0
            //selects ImportantEntries, despite8C8D50's separate third argument1.
            return new CampaignChronicleWriter(HistoryWriter).Write(state, current,
                new(0x7D2, 2, day, string.Empty, 0, 0x51,
                    ImmutableArray.Create(year.ToString(CultureInfo.InvariantCulture), "", ""),
                    ImmutableArray.Create(48400, 48400, 48400, 48400), Primary: false),
                ReadApplicationGameplay()?.UserSelectionServicePresent);
        }

        var first = FormatHistoryReport(state, current, 0x5D25, new(Integers: [year]));
        var second = FormatHistoryReport(first.State, current, 0x5D26, new());
        var pending = second.State.CampaignEffectRequests
            ?? throw AppearanceTurnMissing("annual_limit_pending_owner");
        long revision = second.State.NativeClock?.UpdateOrdinal
            ?? throw AppearanceTurnMissing("annual_limit_revision");
        stop = true;
        //42ACD0 stops the Turn and host; it never calls29EBB0 to request an
        //ending. Both source-time announcements remain unacknowledged.
        return second.State with
        {
            CampaignEffectRequests = pending.Append(CampaignEffectSource.MonthlyMaintenance, revision,
                [Announcement(first.Text), Announcement(second.Text)]),
            CampaignTurnStop = new(CampaignTurnStopReason.CalendarLimit, null, false)
        };

        static CampaignEffectRequest Announcement(string text) => new(null, null)
        { MonthlyMaintenance = new(MonthlyMaintenanceOperation.NarrationText, null, null, text) };
    }
}
