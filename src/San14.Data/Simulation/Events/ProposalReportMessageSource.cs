using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal HistoryTextResult FormatProposalReportCurrent(GameState state, SourceWorldContext current,
        int root, HistoryMessageOperands operands, Action<CampaignAppRandomState>? publishAfterDraw = null) =>
        FormatReportMessage(state, current, root, operands, ReportMessageOrigin.Proposal,
            next => publishAfterDraw?.Invoke(next.CampaignAppRandom32 ??
                throw new InvalidDataException("proposal.text.App32_draw_state")), _personnelNarrationVoice);
}
