using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //741530 displays these announcements after applying the full appointment
    //map. They are not history entries and have no character portrait.
    private HistoryTextResult FormatOfficeCommandReportCurrent(GameState state,
        SourceWorldContext current, int root, int forceId)
    {
        if (root is not (0x597C or 0x597D))
            throw new InvalidDataException("personnel.office.announcement_root");
        return FormatReportMessage(state, current, root, new(Force0: forceId),
            ReportMessageOrigin.Talent, null);
    }
}
