namespace San14.Data;

internal static class AdvantageInformationColumnsSource
{
    internal static DefinitionInformationProfile Profile { get; } = new(1801,
        new Dictionary<int, DefinitionInformationColumnSource>
        {
            [0] = new(0, 104, 0x00000001, 737, 0, true),
            [1] = new(1, 132, 0x00000001, 509, 0, true),
            [2] = new(2, 1000, 0x20000001, 1024, 2, true),
            [3] = new(3, 440, 0x20000001, 1042, 2, true),
        },
        Array.AsReadOnly<DefinitionInformationGroupSource>([
            new(732, [0, 1, 2]),
            new(1042, [0, 3]),
        ]), false);
}
