using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //These are the existing source terminal writers, not a display receipt
    //or a synthetic return value for a partially executed combat callback.
    internal static bool HasNativeCampaignStop(GameState state)
        => state.NativePlayableTerminal is not null || state.CampaignTurnStop is not null;
}
