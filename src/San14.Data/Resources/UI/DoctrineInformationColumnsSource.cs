namespace San14.Data;

internal static class DoctrineInformationColumnsSource
{
    internal static DefinitionInformationProfile Profile { get; } = new(1411,
        new Dictionary<int, DefinitionInformationColumnSource>
        {
            [0] = new(0, 76, 0x00000001, 580, 0, true),
            [1] = new(1, 860, 0x20000001, 1024, 2, true),
        },
        Array.AsReadOnly<DefinitionInformationGroupSource>([
            new(732, [0, 1]),
        ]), true);
}
