namespace San14.Data;

internal static class CityInformationColumnsSource
{
    internal sealed record Definition(int ColumnId,int Width,int Flags,int CaptionId,int SortMode,bool Visible);
    internal sealed record Group(int CaptionId,int[] ColumnIds);
    internal static IReadOnlyDictionary<int,Definition> Rows { get; } = new Dictionary<int,Definition>
    {
        [0] = new(0, 76, 0x00000001, 0x207, 0, true),
        [1] = new(1, 160, 0x00000001, 0x1FD, 0, true),
        [2] = new(2, 132, 0x00000008, 0x1FE, 0, true),
        [3] = new(3, 132, 0x00000008, 0x215, 4, true),
        [4] = new(4, 104, 0x20000008, 0x5EA, 0, true),
        [5] = new(5, 104, 0x20000002, 0x20C, 0, true),
        [6] = new(6, 118, 0x20000002, 0x20D, 0, true),
        [7] = new(7, 104, 0x20000002, 0x20E, 0, true),
        [8] = new(8, 76, 0x20000002, 0x218, 0, true),
        [9] = new(9, 76, 0x20000002, 0x217, 0, true),
        [10] = new(10, 76, 0x20000002, 0x3EC, 0, true),
        [11] = new(11, 76, 0x20000008, 0x3ED, 0, true),
        [12] = new(12, 104, 0x20000002, 0x226, 0, true),
        [13] = new(13, 76, 0x20000002, 0x225, 0, true),
        [14] = new(14, 76, 0x20000002, 0x20B, 0, true),
        [15] = new(15, 188, 0x00000008, 0x234, 0, true),
        [16] = new(16, 76, 0x20000002, 0x25E, 0, true),
        [17] = new(17, 76, 0x20000002, 0x25C, 0, true),
        [18] = new(18, 76, 0x20000008, 0x3D6, 0, true),
        [19] = new(19, 132, 0x00000008, 0x3EE, 0, true),
        [20] = new(20, 174, 0x20000002, 0x62D, 0, true),
        [21] = new(21, 132, 0x00000008, 0x3EF, 0, true),
        [24] = new(24, 146, 0x20000002, 0x629, 0, true),
        [25] = new(25, 146, 0x20000002, 0x62A, 0, true),
        [26] = new(26, 174, 0x20000002, 0x62B, 0, true),
        [27] = new(27, 174, 0x20000002, 0x62C, 0, true),
        [28] = new(28, 90, 0x20000002, 0x3F6, 0, true),
        [29] = new(29, 76, 0x20000008, 0x22, 0, true),
        [30] = new(30, 76, 0x00000001, 0x1FF, 0, true),
        [31] = new(31, 76, 0x00000001, 0x200, 0, true),
        [32] = new(32, 76, 0x20000002, 0x202, 0, true),
    };
    internal static IReadOnlyList<Group> Groups { get; } = Array.AsReadOnly<Group>
    ([
        new(0x44F, [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11]),
        new(0x3FB, [0, 7, 12, 13, 14, 8, 15, 16, 17, 18]),
        new(0x2A3, [0, 19, 21, 13]),
        new(0x3DA, [0, 5, 24, 25, 6, 26, 27, 20, 28, 29]),
        new(0x3FC, [0, 30, 31, 32]),
    ]);
}
