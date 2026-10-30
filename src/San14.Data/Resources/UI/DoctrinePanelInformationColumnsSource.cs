namespace San14.Data;

internal static class DoctrinePanelInformationColumnsSource
{
    internal static DefinitionInformationProfile Profile { get; } = new(2089,
        new Dictionary<int, DefinitionInformationColumnSource>
        {
            [0] = new(0, 244, 0x00000001, 2089, 0, true),
            [1] = new(1, 1140, 0x20000001, 1024, 2, true),
            [2] = new(2, 90, 0x20000008, 2131, 0, true),
            [3] = new(3, 90, 0x20000008, 2133, 0, true),
            [4] = new(4, 90, 0x20000008, 2132, 0, true),
            [5] = new(5, 90, 0x20000008, 2134, 0, true),
        },
        Array.AsReadOnly<DefinitionInformationGroupSource>([
            new(732, [0, 1]),
            new(2090, [0, 2, 3, 4, 5]),
        ]), false);
}
