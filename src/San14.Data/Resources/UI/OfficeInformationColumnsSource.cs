namespace San14.Data;

internal static class OfficeInformationColumnsSource
{
    internal static DefinitionInformationProfile Profile { get; } = new(1457,
        new Dictionary<int, DefinitionInformationColumnSource>
        {
            [0] = new(0, 132, 0x00000001, 540, 0, true),
            [1] = new(1, 90, 0x20000002, 978, 0, true),
            [2] = new(2, 132, 0x20000002, 1065, 0, true),
            [3] = new(3, 76, 0x20000002, 568, 1, true),
            [4] = new(4, 76, 0x20000002, 569, 1, true),
            [5] = new(5, 76, 0x20000002, 570, 1, true),
            [6] = new(6, 76, 0x20000002, 571, 1, true),
            [9] = new(9, 160, 0x00000001, 1108, 0, true),
        },
        Array.AsReadOnly<DefinitionInformationGroupSource>([
            new(732, [0, 1, 2, 3, 4, 5, 6, 9]),
        ]), false);
}
