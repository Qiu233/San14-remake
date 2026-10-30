namespace San14.Data;

internal sealed partial class CampaignEventExecutionContext
{
    //127..131 write F0/E8/E0/D8/D0: the five explicit Person selectors,
    //not the separate lazy dialogue roles. An absent winner preserves the slot.
    internal int SetStoryGovernmentRepresentative(int code, int? physical)
    {
        if (code is < 127 or > 131) throw Missing("government representative opcode");
        if (physical is int selected) _actors[code - 141] = new(selected);
        return 1;
    }
}
