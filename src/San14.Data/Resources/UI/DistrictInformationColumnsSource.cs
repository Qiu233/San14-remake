using System.Collections.ObjectModel;

namespace San14.Data;

internal static class DistrictInformationColumnsSource
{
    internal sealed record Definition(int Id,int Width,int Flags,int CaptionId,int SortMode);
    internal sealed record Group(int CaptionId,int[] ColumnIds);
    internal static IReadOnlyDictionary<int,Definition> Rows { get; } = new ReadOnlyDictionary<int,Definition>(new Dictionary<int,Definition>
    {
        [0] = new(0,216,0x00000001,0x1FE,3),
        [1] = new(1,132,0x00000008,0x214,4),
        [2] = new(2,118,0x20000002,0x20C,0),
        [3] = new(3,146,0x20000002,0x20D,0),
        [4] = new(4,132,0x20000002,0x20E,0),
        [5] = new(5,76,0x20000002,0x207,0),
        [6] = new(6,76,0x20000002,0x211,0),
        [7] = new(7,132,0x00000008,0x21F,1),
        [8] = new(8,188,0x00000008,0x3FF,3),
        [9] = new(9,146,0x20000002,0x629,0),
        [10] = new(10,146,0x20000002,0x62A,0),
        [11] = new(11,174,0x20000002,0x62B,0),
        [12] = new(12,174,0x20000002,0x62C,0),
        [13] = new(13,132,0x20000002,0x8A4,0),
    });
    internal static IReadOnlyList<Group> Groups { get; } = Array.AsReadOnly<Group>([
        new(0x44F,[0,1,2,3,4,13,5,6,7,8]),
        new(0x3DA,[0,2,9,10,3,11,12]),
    ]);
}
