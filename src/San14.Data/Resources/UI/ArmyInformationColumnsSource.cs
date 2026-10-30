namespace San14.Data;

/// <summary>Normal Army information columns and the six ordered source groups.</summary>
internal static class ArmyInformationColumnsSource
{
    internal sealed record Definition(int ColumnId, int Width, int Flags, int CaptionId, int SortMode, bool Visible);
    internal sealed record Group(int CaptionId, int[] ColumnIds);

    // Authored normal-list metadata extracted offline. Text uses normal Data section1.
    internal static IReadOnlyList<Definition> Rows { get; } = Array.AsReadOnly<Definition>
    ([
        new(0, 160, 0x00000001, 0x224, 4, true),
        new(1, 160, 0x00000001, 0x1FD, 0, true),
        new(2, 132, 0x20000008, 0x1FE, 0, true),
        new(3, 104, 0x00000008, 0x219, 3, true),
        new(4, 90, 0x20000002, 0x20E, 0, true),
        new(5, 104, 0x20000002, 0x226, 0, true),
        new(6, 76, 0x20000002, 0x3EC, 0, true),
        new(7, 104, 0x20000002, 0x3DB, 0, true),
        new(8, 132, 0x20000002, 0x3DC, 0, true),
        new(9, 76, 0x20000002, 0x225, 0, true),
        new(10, 76, 0x20000008, 0x258, 0, true),
        new(11, 76, 0x20000002, 0x25E, 0, true),
        new(12, 76, 0x20000002, 0x25C, 0, true),
        new(13, 76, 0x20000002, 0x260, 0, true),
        new(14, 76, 0x20000008, 0x3D6, 0, true),
        new(15, 76, 0x00000001, 0x22B, 0, true),
        new(16, 76, 0x00000002, 0x333, 0, true),
        new(17, 76, 0x20000002, 0x22F, 0, true),
        new(18, 104, 0x20000002, 0x22C, 0, true),
        new(19, 104, 0x20000002, 0x22D, 0, true),
        new(20, 76, 0x20000002, 0x230, 0, true),
        new(21, 76, 0x20000002, 0x22E, 0, true),
        new(22, 104, 0x20000008, 0x36A, 0, true),
        new(23, 76, 0x00000001, 0x22B, 0, true),
        new(24, 76, 0x00000002, 0x333, 0, true),
        new(25, 76, 0x20000002, 0x22F, 0, true),
        new(26, 104, 0x20000002, 0x22C, 0, true),
        new(27, 104, 0x20000002, 0x22D, 0, true),
        new(28, 76, 0x20000002, 0x230, 0, true),
        new(29, 76, 0x20000002, 0x22E, 0, true),
        new(30, 104, 0x20000008, 0x36A, 0, true),
        new(31, 188, 0x00000008, 0x3DD, 0, true),
        new(32, 188, 0x00000008, 0x3DE, 0, true),
        new(33, 188, 0x00000008, 0x3DF, 0, true),
        new(34, 132, 0x00000008, 0x3E0, 0, true),
        new(35, 160, 0x00000008, 0x299, 3, true),
        new(36, 104, 0x00000008, 0x20A, 3, true),
        new(37, 160, 0x00000008, 0x3E1, 3, true),
        new(38, 132, 0x00000008, 0x279, 0, true),
        new(39, 132, 0x20000008, 0x27A, 0, true),
        new(40, 76, 0x20000008, 0x27B, 0, true),
        new(41, 132, 0x00000008, 0x27C, 0, true),
        new(42, 104, 0x00000008, 0x34, 0, true),
    ]);

    internal static IReadOnlyList<Group> Groups { get; } = Array.AsReadOnly<Group>
    ([
        new(0x219, [0, 1, 2, 3, 42, 10, 9, 4, 5, 6, 7, 8]),
        new(0x44F, [0, 11, 12, 13, 14]),
        new(0x368, [0, 15, 16, 17, 18, 19, 20, 21, 22]),
        new(0x369, [0, 23, 24, 25, 26, 27, 28, 29, 30]),
        new(0x234, [0, 31, 32, 33]),
        new(0x3E0, [0, 34, 35, 36, 37, 38, 39, 40, 41]),
    ]);
}
