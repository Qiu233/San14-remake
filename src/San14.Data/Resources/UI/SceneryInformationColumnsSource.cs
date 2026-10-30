namespace San14.Data;

internal static class SceneryInformationColumnsSource
{
    internal static DefinitionInformationProfile Profile { get; } = new(0x840,
        new Dictionary<int, DefinitionInformationColumnSource>
        {
            [0] = new(0, 188, 0x00000001, 173, 0, true),
            [1] = new(1, 132, 0x00000001, 514, 0, true),
            [2] = new(2, 76, 0x00000001, 519, 0, true),
            [5] = new(5, 160, 0x00000001, 509, 0, true),
            [6] = new(6, 160, 0x00000002, 2124, 0, true),
            [7] = new(7, 188, 0x20000001, 1041, 0, true),
            [8] = new(8, 160, 0x00000001, 1103, 0, true),
            [9] = new(9, 216, 0x00000002, 2204, 0, true),
        }, Array.AsReadOnly<DefinitionInformationGroupSource>([new(0x44F, [0,2,1,5,6,7,8,9])]), false);
}
