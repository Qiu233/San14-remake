namespace San14.Data;

internal static class AreaInformationColumnsSource
{
    internal sealed record Definition(int ColumnId,int Width,int Flags,int CaptionId,int SortMode,bool Visible);
    internal sealed record Group(int CaptionId,int[] ColumnIds);
    internal static IReadOnlyDictionary<int,Definition> Rows { get; } = new Dictionary<int,Definition>
    {
        [0] = new(0, 132, 0x00000001, 0x202, 0, true),
        [1] = new(1, 76, 0x00000001, 0x207, 3, true),
        [2] = new(2, 160, 0x00000001, 0x1FD, 0, true),
        [3] = new(3, 132, 0x20000008, 0x1FE, 0, true),
        [4] = new(4, 118, 0x20000008, 0x3D3, 3, true),
        [5] = new(5, 132, 0x00000008, 0x3D4, 4, true),
        [6] = new(6, 104, 0x20000002, 0x47C, 0, true),
        [7] = new(7, 132, 0x20000002, 0x47D, 0, true),
        [8] = new(8, 104, 0x20000002, 0x47E, 0, true),
        [9] = new(9, 132, 0x20000002, 0x47F, 0, true),
        [10] = new(10, 104, 0x20000002, 0x480, 0, true),
        [11] = new(11, 132, 0x20000002, 0x481, 0, true),
        [12] = new(12, 132, 0x20000008, 0x2B6, 0, true),
        [13] = new(13, 104, 0x20000002, 0x298, 0, true),
        [14] = new(14, 76, 0x20000002, 0x253, 0, true),
        [15] = new(15, 104, 0x20000002, 0x254, 0, true),
        [16] = new(16, 132, 0x20000002, 0x255, 0, true),
        [17] = new(17, 132, 0x20000002, 0x256, 0, true),
    };
    internal static IReadOnlyList<Group> Groups { get; } = Array.AsReadOnly<Group>
    ([
        new(0x44F, [0, 1, 2, 3, 4, 5, 6, 8, 10]),
        new(0x2A3, [0, 5, 6, 7, 8, 9, 10, 11, 13, 14, 12]),
        new(0x3DA, [0, 15, 16, 17]),
    ]);
}
