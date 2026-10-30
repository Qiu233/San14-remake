using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal bool ReadOriginalAiTroopsPurposeEqualsCurrent(GameState state,
        SourceWorldContext current, OriginalAiTroopsReference group, uint literal)
    {
        // The source first invokes the selected CTroopsData receiver's native
        // validity callback. An invalid receiver has no readable purpose.
        if (!ReadOriginalAiTroopsValidityCurrent(current, group)) return false;
        // Reuse the existing actual objective owner, including its explicit
        // unknown boundary. This is not the actor's own group or District.
        // Native compares its ushort purpose word against the full uint
        // literal; an out-of-range literal must not be truncated into a match.
        uint purpose = unchecked((ushort)TroopsInfoPurpose(state, group).Kind);
        return purpose == literal;
    }
}
