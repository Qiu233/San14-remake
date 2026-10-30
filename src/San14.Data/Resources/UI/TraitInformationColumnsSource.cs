namespace San14.Data;

internal static class TraitInformationColumnsSource
{
    internal static DefinitionInformationProfile Profile { get; } = new(1428,
        new Dictionary<int, DefinitionInformationColumnSource>
        {
            [0] = new(0, 76, 0x00000001, 573, 3, true),
            [1] = new(1, 1140, 0x00000001, 1024, 2, true),
            [2] = new(2, 76, 0x00000008, 2073, 0, true),
            [3] = new(3, 692, 0x00000001, 2043, 1, true),
            [4] = new(4, 692, 0x00000001, 2044, 2, true),
            [5] = new(5, 496, 0x00000001, 1028, 1, true),
            [6] = new(6, 202, 0x00000001, 2048, 1, true),
            [7] = new(7, 496, 0x00000001, 1029, 1, true),
            [8] = new(8, 202, 0x00000001, 2049, 1, true),
        },
        Array.AsReadOnly<DefinitionInformationGroupSource>([
            new(732, [0, 2, 1]),
            new(974, [0, 3, 4]),
            new(1041, [0, 5, 6, 7, 8]),
        ]), true);
}
