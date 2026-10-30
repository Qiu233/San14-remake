using System.Collections.Immutable;
namespace San14.Data;
internal static class DiplomacyTargetColumns
{
    internal sealed record Column(int Id, int Width, int Flags, int Caption);
    internal sealed record Group(int Caption, int[] Columns);
    internal static readonly Column[] Force =
    [
        new(0, 160, 0x1, 0x1fd),
        new(3, 118, 0x20000002, 0x20c),
        new(4, 146, 0x20000002, 0x20d),
        new(5, 132, 0x20000002, 0x20e),
        new(6, 76, 0x20000002, 0x207),
        new(7, 76, 0x20000002, 0x211),
        new(9, 76, 0x20000002, 0x1fe),
        new(10, 132, 0x20000002, 0x226),
        new(11, 76, 0x20000008, 0x232),
        new(12, 76, 0x20000008, 0x413),
        new(13, 76, 0x20000008, 0x289),
        new(14, 132, 0x20000008, 0x414),
        new(15, 76, 0x1, 0x244),
        new(16, 132, 0x8, 0x243),
        new(17, 146, 0x20000002, 0x629),
        new(18, 146, 0x20000002, 0x62a),
        new(19, 174, 0x20000002, 0x62b),
        new(20, 174, 0x20000002, 0x62c),
        new(21, 132, 0x1, 0x415),
        new(22, 132, 0x1, 0x416),
        new(23, 132, 0x20000002, 0x417),
        new(24, 132, 0x20000002, 0x418),
        new(25, 76, 0x20000008, 0x293),
        new(26, 132, 0x20000002, 0x419),
        new(27, 132, 0x20000008, 0x41a),
        new(29, 76, 0x20000002, 0x250),
        new(30, 76, 0x20000002, 0x2a0),
        new(31, 76, 0x20000002, 0x2a1),
        new(32, 76, 0x20000002, 0x2a2),
        new(33, 76, 0x20000002, 0x2a3),
        new(34, 76, 0x20000002, 0x2a4),
        new(37, 118, 0x20000002, 0x835),
        new(38, 146, 0x20000002, 0x85d),
    ];
    internal static readonly Group[] ForceGroups =
    [
        new(0x3cd, [0,12,13,14,21,22,23,24,25,26,27]),
        new(0x41b, [0,9,6,7,5,10,11]),
        new(0x287, [0,12,13,14,21,22,23,24,25,26,27]),
        new(0x242, [0,15,29,16,30,31,32,33,34]),
        new(0x3da, [0,3,17,18,4,19,20,37,38]),
    ];
    internal static readonly Column[] ReturnForce =
    [
        new(0, 160, 0x1, 0x1fd),
        new(3, 118, 0x20000002, 0x20c),
        new(4, 146, 0x20000002, 0x20d),
        new(5, 132, 0x20000002, 0x20e),
        new(6, 76, 0x20000002, 0x207),
        new(7, 76, 0x20000002, 0x211),
        new(9, 76, 0x20000002, 0x1fe),
        new(10, 132, 0x20000002, 0x226),
        new(11, 76, 0x20000008, 0x232),
        new(12, 76, 0x20000008, 0x413),
        new(13, 76, 0x20000008, 0x289),
        new(14, 132, 0x20000008, 0x414),
        new(15, 76, 0x1, 0x244),
        new(16, 132, 0x8, 0x243),
        new(17, 146, 0x20000002, 0x629),
        new(18, 146, 0x20000002, 0x62a),
        new(19, 174, 0x20000002, 0x62b),
        new(20, 174, 0x20000002, 0x62c),
        new(21, 132, 0x1, 0x415),
        new(22, 132, 0x1, 0x416),
        new(23, 132, 0x20000002, 0x417),
        new(24, 132, 0x20000002, 0x418),
        new(25, 76, 0x20000008, 0x293),
        new(26, 132, 0x20000002, 0x419),
        new(27, 132, 0x20000008, 0x41a),
        new(28, 76, 0x20000002, 0x3ec),
        new(29, 76, 0x20000002, 0x250),
        new(30, 76, 0x20000002, 0x2a0),
        new(31, 76, 0x20000002, 0x2a1),
        new(32, 76, 0x20000002, 0x2a2),
        new(33, 76, 0x20000002, 0x2a3),
        new(34, 76, 0x20000002, 0x2a4),
        new(37, 118, 0x20000002, 0x835),
        new(38, 146, 0x20000002, 0x85d),
    ];
    internal static readonly Group[] ReturnForceGroups =
    [
        new(0x3cd, [0,28,12,13,14,21,22,23,24,25,26,27]),
        new(0x41b, [0,9,6,7,5,10,11]),
        new(0x287, [0,12,13,14,21,22,23,24,25,26,27]),
        new(0x242, [0,15,29,16,30,31,32,33,34]),
        new(0x3da, [0,3,17,18,4,19,20,37,38]),
    ];
    internal static readonly Column[] City =
    [
        new(0, 132, 0x1, 0x205),
        new(1, 216, 0x1, 0x1fe),
        new(2, 76, 0x20000002, 0x218),
        new(3, 104, 0x20000002, 0x20e),
        new(4, 104, 0x20000002, 0x20c),
        new(5, 118, 0x20000002, 0x20d),
        new(8, 188, 0x1, 0x1fd),
        new(9, 132, 0x1, 0x215),
        new(10, 104, 0x20000002, 0x5ea),
        new(11, 76, 0x20000002, 0x217),
        new(12, 76, 0x20000002, 0x3ec),
    ];
    internal static readonly Group[] CityGroups =
    [
        new(0x3cd, [0,8,1,9,10,4,5,3,2,11,12]),
    ];
    internal static readonly Column[] Item =
    [
        new(0, 188, 0x1, 0x246),
        new(1, 104, 0x8, 0x401),
        new(3, 132, 0x8, 0x411),
        new(4, 76, 0x20000002, 0x423),
        new(5, 160, 0x8, 0x424),
        new(6, 132, 0x8, 0x425),
        new(7, 76, 0x8, 0x426),
        new(8, 132, 0x20000001, 0x452),
    ];
    internal static readonly Group[] ItemGroups =
    [
        new(0x2dc, [0,1,8,3,4,5,6,7]),
    ];
    internal static readonly Column[] Area =
    [
        new(0, 132, 0x1, 0x202),
        new(1, 76, 0x1, 0x207),
        new(2, 160, 0x1, 0x1fd),
        new(3, 132, 0x20000008, 0x1fe),
        new(4, 118, 0x20000008, 0x3d3),
        new(5, 132, 0x8, 0x3d4),
        new(6, 104, 0x20000002, 0x47c),
        new(7, 132, 0x20000002, 0x47d),
        new(8, 104, 0x20000002, 0x47e),
        new(9, 132, 0x20000002, 0x47f),
        new(10, 104, 0x20000002, 0x480),
        new(11, 132, 0x20000002, 0x481),
        new(12, 132, 0x20000008, 0x2b6),
        new(13, 104, 0x20000002, 0x298),
        new(14, 76, 0x20000002, 0x253),
        new(15, 104, 0x20000002, 0x254),
        new(16, 132, 0x20000002, 0x255),
        new(17, 132, 0x20000002, 0x256),
    ];
    internal static readonly Group[] AreaGroups =
    [
        new(0x44f, [0,1,2,3,4,5,6,8,10]),
        new(0x2a3, [0,5,6,7,8,9,10,11,13,14,12]),
        new(0x3da, [0,15,16,17]),
    ];
}
