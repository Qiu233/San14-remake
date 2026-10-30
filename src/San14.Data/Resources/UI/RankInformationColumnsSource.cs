namespace San14.Data;

internal static class RankInformationColumnsSource
{
    internal static DefinitionInformationProfile Profile { get; } = new(1498,
        new Dictionary<int, DefinitionInformationColumnSource>
        {
            [0] = new(0, 160, 0x00000001, 539, 0, true),
            [1] = new(1, 90, 0x20000002, 978, 0, true),
            [2] = new(2, 76, 0x20000002, 568, 1, true),
            [3] = new(3, 76, 0x20000002, 569, 1, true),
            [4] = new(4, 76, 0x20000002, 570, 1, true),
            [5] = new(5, 76, 0x20000002, 571, 1, true),
            [7] = new(7, 1140, 0x20000001, 969, 2, true),
            [8] = new(8, 160, 0x20000002, 1560, 0, true),
            [9] = new(9, 132, 0x20000008, 971, 1, true),
            [10] = new(10, 132, 0x20000008, 976, 1, true),
            [11] = new(11, 132, 0x20000008, 977, 1, true),
            [12] = new(12, 76, 0x20000008, 972, 1, true),
            [13] = new(13, 188, 0x20000002, 2202, 0, true),
            [14] = new(14, 132, 0x20000002, 2201, 0, true),
        },
        Array.AsReadOnly<DefinitionInformationGroupSource>([
            new(732, [0, 1, 2, 3, 4, 5]),
            new(970, [0, 8, 13, 14, 9, 10, 11, 12]),
            new(969, [0, 7]),
        ]), false);
}
