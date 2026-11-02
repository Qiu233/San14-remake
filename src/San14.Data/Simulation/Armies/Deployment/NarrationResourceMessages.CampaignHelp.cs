namespace San14.Data;

internal sealed partial class NarrationResourceMessages
{
    private bool _campaignHelpContent;

    internal string ResolveCampaignHelpContent(int root)
    {
        if (root is < 0x9FCA or > 0xA832) throw new InvalidDataException("campaign.help.message_range");
        _prefix = "campaign.help.content";
        _campaignHelpContent = true;
        try
        {
            return ExecuteMessage(InformationExplanationArguments.Instance,
                _ => throw Failure("unexpected_random"), root, Get(root, true), _ => false,
                true, null, _ => false, CampaignHelpEscape, true).Text;
        }
        finally { _campaignHelpContent = false; }
    }

    internal NarrationResourceText ResolveCampaignHelpAdviser(INarrationCurrentContext people,
        Func<int, int> drawApp32)
    {
        _prefix = "campaign.help.adviser";
        bool previous = _report; _report = true;
        try
        {
            return ExecuteMessage(people, drawApp32, 0x9FB2, Get(0x9FB2, true),
                id => id is >= 0x9FB3 and <= 0x9FCC, true, null,
                CampaignHelpAdviserReferenceAllowed, CampaignHelpEscape, true);
        }
        finally { _report = previous; }
    }

    //The finite TC/SC9FB2 resource closure includes shared forms of address.
    //Only9FB3..9FCC are greeting leaves; nested references retain that leaf.
    private static bool CampaignHelpAdviserReferenceAllowed(int id) => id is
        >= 0x9FB3 and <= 0x9FCC or
        >= 0x0097 and <= 0x00D5 or >= 0x00D7 and <= 0x0101 or 0x0119 or
        >= 0x04FC and <= 0x0502 or >= 0x05E3 and <= 0x05E8 or >= 0x0600 and <= 0x060A;

    private static string CampaignHelpEscape(int group, int code) => (group, code) switch
    {
        ('C', 'J') => "^02", ('C', 'L') => "^05",
        ('E', 'K') or ('E', 'M') => string.Empty,
        _ => throw new InvalidDataException($"campaign.help.escape.{group:X2}.{code:X2}")
    };
}
