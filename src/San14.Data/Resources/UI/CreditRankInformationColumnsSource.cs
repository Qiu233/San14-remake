namespace San14.Data;

internal static class CreditRankInformationColumnsSource
{
    internal static DefinitionInformationProfile Profile { get; } = new(2082,
        new Dictionary<int, DefinitionInformationColumnSource>
        {
            [0] = new(0, 160, 0x00000001, 2082, 0, true),
            [1] = new(1, 132, 0x20000002, 2210, 0, true),
            [2] = new(2, 160, 0x00000008, 2199, 0, true),
            [3] = new(3, 188, 0x00000008, 2211, 0, true),
        },
        Array.AsReadOnly<DefinitionInformationGroupSource>([
            new(732, [0, 1, 2, 3]),
        ]), false);
}
