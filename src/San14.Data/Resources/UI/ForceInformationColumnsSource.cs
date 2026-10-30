namespace San14.Data;

/// <summary>Normal Force list authored columns and seven ordered groups.</summary>
internal static class ForceInformationColumnsSource
{
    internal sealed record Definition(int ColumnId,int Width,int Flags,int CaptionId,int SortMode,bool Visible);
    internal sealed record Group(int CaptionId,int[] ColumnIds);
    internal static IReadOnlyDictionary<int,Definition> Rows { get; } = new Dictionary<int,Definition>
    {
        [0] = new(0,160,0x00000001,0x1FD,4,true),
        [1] = new(1,76,0x00000008,0x3C8,1,true),
        [2] = new(2,160,0x00000008,0x21B,0,true),
        [3] = new(3,118,0x20000002,0x20C,0,true),
        [4] = new(4,146,0x20000002,0x20D,0,true),
        [5] = new(5,132,0x20000002,0x20E,0,true),
        [6] = new(6,76,0x20000002,0x207,0,true),
        [7] = new(7,76,0x20000002,0x211,0,true),
        [8] = new(8,76,0x20000008,0x3ED,1,true),
        [9] = new(9,76,0x20000002,0x1FE,0,true),
        [10] = new(10,132,0x20000002,0x226,0,true),
        [11] = new(11,76,0x20000008,0x232,1,true),
        [12] = new(12,76,0x20000008,0x413,1,true),
        [13] = new(13,76,0x20000008,0x289,1,true),
        [14] = new(14,132,0x20000008,0x414,1,true),
        [15] = new(15,76,0x00000001,0x244,0,true),
        [16] = new(16,132,0x00000008,0x243,4,true),
        [17] = new(17,146,0x20000002,0x629,0,true),
        [18] = new(18,146,0x20000002,0x62A,0,true),
        [19] = new(19,174,0x20000002,0x62B,0,true),
        [20] = new(20,174,0x20000002,0x62C,0,true),
        [21] = new(21,132,0x00000001,0x415,0,true),
        [22] = new(22,132,0x00000001,0x416,0,true),
        [23] = new(23,132,0x20000002,0x417,1,true),
        [24] = new(24,132,0x20000002,0x418,0,true),
        [25] = new(25,76,0x20000008,0x293,1,true),
        [26] = new(26,132,0x20000002,0x419,1,true),
        [27] = new(27,132,0x20000008,0x41A,0,true),
        [29] = new(29,76,0x20000002,0x250,1,true),
        [30] = new(30,76,0x20000002,0x2A0,1,true),
        [31] = new(31,76,0x20000002,0x2A1,1,true),
        [32] = new(32,76,0x20000002,0x2A2,1,true),
        [33] = new(33,76,0x20000002,0x2A3,1,true),
        [34] = new(34,76,0x20000002,0x2A4,1,true),
        [37] = new(37,118,0x20000002,0x835,0,true),
        [38] = new(38,146,0x20000002,0x85D,0,true),
        [41] = new(41,104,0x20000008,0x83E,1,true),
        [43] = new(43,104,0x20000002,0x83F,1,true),
        [44] = new(44,76,0x20000002,0x82F,1,true),
        [45] = new(45,76,0x20000002,0x831,1,true),
        [46] = new(46,76,0x20000002,0x834,1,true),
        [47] = new(47,76,0x20000002,0x833,1,true),
        [48] = new(48,76,0x20000002,0x832,1,true),
        [49] = new(49,76,0x20000002,0x830,1,true),
    };
    internal static IReadOnlyList<Group> Groups { get; } = Array.AsReadOnly<Group>
    ([
        new(0x44F,[0,1,2,3,4,5,37,6,7,8]),
        new(0x41B,[0,9,6,7,5,10,11]),
        new(0x287,[0,12,13,14,21,22,23,24,25,26,27]),
        new(0x242,[0,15,29,16,30,31,32,33,34]),
        new(0x829,[0,15,44,45,46,47,48,49]),
        new(0x83E,[0,15,41,43,8]),
        new(0x3DA,[0,3,17,18,4,19,20,37,38]),
    ]);
}
