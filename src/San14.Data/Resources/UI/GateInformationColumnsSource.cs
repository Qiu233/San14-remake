namespace San14.Data;

/// <summary>Normal Gate list authored columns and two ordered groups.</summary>
internal static class GateInformationColumnsSource
{
    internal sealed record Definition(int ColumnId,int Width,int Flags,int CaptionId,int SortMode,bool Visible);
    internal sealed record Group(int CaptionId,int[] ColumnIds);
    internal static IReadOnlyDictionary<int,Definition> Rows { get; } = new Dictionary<int,Definition>
    {
        [0] = new(0,104,0x00000001,0x41D,0,true),
        [1] = new(1,160,0x00000001,0x1FD,0,true),
        [2] = new(2,132,0x00000008,0x1FE,0,true),
        [3] = new(3,132,0x00000008,0x215,4,true),
        [4] = new(4,90,0x20000002,0x20C,0,true),
        [5] = new(5,104,0x20000002,0x20D,0,true),
        [6] = new(6,90,0x20000002,0x20E,0,true),
        [7] = new(7,76,0x20000002,0x218,0,true),
        [8] = new(8,76,0x20000002,0x217,0,true),
        [9] = new(9,76,0x20000002,0x3EC,0,true),
        [10] = new(10,104,0x20000002,0x226,0,true),
        [11] = new(11,76,0x20000002,0x225,0,true),
        [12] = new(12,76,0x20000002,0x20B,0,true),
        [13] = new(13,160,0x00000008,0x234,0,true),
        [14] = new(14,76,0x20000002,0x25E,0,true),
        [15] = new(15,76,0x20000002,0x25C,0,true),
        [16] = new(16,76,0x20000008,0x3D6,0,true),
    };
    internal static IReadOnlyList<Group> Groups { get; } = Array.AsReadOnly<Group>
    ([
        new(0x44F,[0,1,2,3,4,5,6,7,8,9]),
        new(0x3FB,[0,6,10,11,12,7,13,14,15,16]),
    ]);
}
