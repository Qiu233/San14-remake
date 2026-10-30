namespace San14.Data;

internal static class FacilityInformationColumnsSource
{
    internal static DefinitionInformationProfile Profile { get; } = new(1464,
        new Dictionary<int, DefinitionInformationColumnSource>
        {
            [0] = new(0, 132, 0x00000001, 522, 0, true),
            [1] = new(1, 132, 0x20000008, 1025, 0, true),
            [3] = new(3, 76, 0x00000002, 1027, 0, true),
            [4] = new(4, 76, 0x20000002, 523, 0, true),
            [5] = new(5, 132, 0x20000008, 1158, 1, true),
            [6] = new(6, 160, 0x00000008, 1028, 1, true),
            [7] = new(7, 160, 0x00000008, 1029, 1, true),
            [8] = new(8, 132, 0x00000008, 1115, 1, true),
            [11] = new(11, 580, 0x00000001, 1042, 2, true),
        },
        Array.AsReadOnly<DefinitionInformationGroupSource>([
            new(732, [0, 1, 3, 4, 5, 6, 7, 8]),
            new(1042, [0, 11]),
        ]), false);
}
