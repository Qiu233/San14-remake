namespace San14.Data;

internal static class PolicyInformationColumnsSource
{
    internal static DefinitionInformationProfile Profile { get; } = new(1478,
        new Dictionary<int, DefinitionInformationColumnSource>
        {
            [0] = new(0, 132, 0x00000001, 577, 0, true),
            [1] = new(1, 860, 0x00000001, 1024, 2, true),
            [2] = new(2, 76, 0x00000008, 1100, 0, true),
            [3] = new(3, 188, 0x00000001, 1164, 1, true),
            [4] = new(4, 188, 0x00000001, 1165, 1, true),
            [5] = new(5, 188, 0x00000001, 1166, 1, true),
            [6] = new(6, 188, 0x00000001, 1167, 1, true),
            [7] = new(7, 188, 0x00000001, 1168, 1, true),
            [8] = new(8, 188, 0x00000001, 1169, 1, true),
            [9] = new(9, 188, 0x00000001, 1170, 1, true),
            [10] = new(10, 188, 0x00000001, 1171, 1, true),
        },
        Array.AsReadOnly<DefinitionInformationGroupSource>([
            new(732, [0, 2, 1]),
            new(1164, [0, 3, 4, 5, 6]),
            new(1165, [0, 7, 8, 9, 10]),
        ]), true);
}
