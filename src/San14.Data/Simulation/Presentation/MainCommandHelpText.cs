namespace San14.Data;

/// <summary>The fixed rank denial includes source colour controls. Shared
/// startup and live hint consumers expand the same resource without a World.</summary>
public static class MainCommandHelpText
{
    public static string ReadRankRequirement(LoadedMessages resource)
        => new NarrationResourceMessages(resource.Messages).ResolveMainCommandRankRequirement();
}

internal sealed partial class NarrationResourceMessages
{
    internal string ResolveMainCommandRankRequirement()
    {
        _prefix = "map.main_command.rank_help";
        const int root = 0x5991;
        return ExecuteMessage(InformationExplanationArguments.Instance,
            _ => throw Failure("unexpected_random"), root, Get(root, true), _ => false,
            true, null, _ => false, CampaignHelpEscape, true).Text;
    }
}
