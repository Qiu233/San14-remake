namespace San14.Data;

internal sealed partial class NarrationResourceMessages
{
    //The actual ordinary rank-promotion narration roots. All five installed
    //TC/SC ExCe records are leaves with no static child references, so the
    //root and referenced-message admission use this same finite set.
    private static readonly HashSet<int> StoryRankReportMessages =
    [
        0x2C66, 0x2C67, 0x2C6A, 0x2C7C, 0x2C7E,
    ];
}
