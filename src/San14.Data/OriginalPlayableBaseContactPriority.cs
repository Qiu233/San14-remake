using San14.Core;

namespace San14.Data;

/// <summary>16EB20's current Base-first Hex1A selection. Non-Base field
/// contacts retain their separate finite encounter provider.</summary>
public sealed class OriginalPlayableBaseContactPriority(OriginalOwnedArmyCampaignRules campaign)
    : IPlayableBaseContactPriorityRules
{
    public Func<int, PlayableBaseContactPriority?> CaptureCurrentBasePriority(GameState state)
    {
        var current = campaign.CaptureCurrentDefinitions(state);
        return hex => current is null ? null : OriginalFieldBasePrioritySource.ReadCurrentBasePriority(current, hex);
    }
}
