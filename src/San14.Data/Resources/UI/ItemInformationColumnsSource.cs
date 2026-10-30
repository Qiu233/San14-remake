namespace San14.Data;

// Offline-extracted ordinary current-world Item information profile.
// The title Collection and OwnForce Belongings callers retain separate admission.
internal static class ItemInformationColumnsSource
{
    internal static DefinitionInformationProfile Profile { get; } = new(0x5A9,
        new Dictionary<int, DefinitionInformationColumnSource>
        {
            [0] = new(0, 188, 0x00000001, 0x246, 0, true),
            [1] = new(1, 104, 0x00000008, 0x401, 0, true),
            [3] = new(3, 132, 0x00000008, 0x411, 1, true),
            [4] = new(4, 76, 0x20000002, 0x423, 0, true),
            [5] = new(5, 160, 0x00000008, 0x424, 0, true),
            [6] = new(6, 132, 0x00000008, 0x425, 0, true),
            [7] = new(7, 76, 0x00000008, 0x426, 0, true),
            [8] = new(8, 132, 0x20000001, 0x452, 1, true),
        },
        Array.AsReadOnly<DefinitionInformationGroupSource>([
            new(0x2DC, [0, 1, 8, 3, 4, 5, 6, 7]),
        ]), false);
}
