namespace San14.Data;

internal static class TacticInformationColumnsSource
{
    internal static DefinitionInformationProfile Profile { get; } = new(1469,
        new Dictionary<int, DefinitionInformationColumnSource>
        {
            [0] = new(0, 132, 0x00000001, 564, 0, true),
            [1] = new(1, 104, 0x00000008, 1109, 0, true),
            [2] = new(2, 132, 0x00000008, 1110, 0, true),
            [3] = new(3, 104, 0x20000008, 1204, 0, true),
            [4] = new(4, 244, 0x00000001, 1112, 0, true),
            [5] = new(5, 104, 0x20000002, 1113, 1, true),
            [6] = new(6, 160, 0x20000002, 1732, 1, true),
            [7] = new(7, 160, 0x20000001, 1115, 1, true),
            [8] = new(8, 244, 0x00000001, 1112, 0, true),
            [9] = new(9, 104, 0x20000002, 1113, 1, true),
            [10] = new(10, 160, 0x20000002, 1732, 1, true),
            [11] = new(11, 160, 0x20000001, 1115, 1, true),
            [12] = new(12, 244, 0x20000008, 1028, 0, true),
            [13] = new(13, 244, 0x20000008, 1029, 0, true),
        },
        Array.AsReadOnly<DefinitionInformationGroupSource>([
            new(732, [0, 1, 2, 3, 12, 13]),
            new(1028, [0, 4, 5, 6, 7]),
            new(1029, [0, 8, 9, 10, 11]),
        ]), true);
}
