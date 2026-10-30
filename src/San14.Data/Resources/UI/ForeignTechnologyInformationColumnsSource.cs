namespace San14.Data;

internal static class ForeignTechnologyInformationColumnsSource
{
    internal static DefinitionInformationProfile Profile { get; } = new(1825,
        new Dictionary<int, DefinitionInformationColumnSource>
        {
            [0] = new(0, 188, 0x00000001, 139, 0, true),
            [1] = new(1, 1000, 0x00000001, 1024, 2, true),
        },
        Array.AsReadOnly<DefinitionInformationGroupSource>([
            new(1103, [0, 1]),
        ]), false);
}
