namespace San14.Data;

// OwnForce belongings exposes two retained outer tabs. The shared Gem bank
// has further groups, but this caller has no group-switch owner: only basic
// columns0..5 are reachable. Gem column0 remains clickable with false comparator.
internal static class BelongingsInformationColumnsSource
{
    internal const int TitleCaptionId = 0x69B;
    internal const int ItemTabCaptionId = 0x246;
    internal const int GemTabCaptionId = 0x699;
    internal static DefinitionInformationProfile Items => ItemInformationColumnsSource.Profile;

    internal static DefinitionInformationProfile Gems { get; } = new(TitleCaptionId,
        new Dictionary<int, DefinitionInformationColumnSource>
        {
            [0] = new(0, 188, 0x00000001, 0x41E, 2, true),
            [1] = new(1, 62, 0x20000002, 0x41F, 0, true),
            [2] = new(2, 160, 0x00000001, 0x234, 0, true),
            [3] = new(3, 160, 0x00000001, 0x455, 0, true),
            [4] = new(4, 132, 0x00000008, 0x456, 0, true),
            [5] = new(5, 104, 0x20000008, 0x4B4, 0, true),
        }, Array.AsReadOnly<DefinitionInformationGroupSource>([
            new(0x3CD, [0, 1, 2, 3, 4, 5]),
        ]), false);
}
