using System.Collections.Immutable;

namespace San14.Data;

internal static class DiplomacyPrisonerColumnsSource
{
    internal static ImmutableArray<OfficerInformationGroup> Create(Func<int,int,string> text)
    {
        var columns = new Dictionary<int, OfficerInformationColumn>
        {
            [0] = new(0, 0x0211, text(1, 0x0211), 132, 3, true, true, true),
            [4] = new(4, 0x021A, text(1, 0x021A), 160, 4, true, true, true),
            [8] = new(8, 0x0238, text(1, 0x0238), 76, 5, false, true, true),
            [9] = new(9, 0x0239, text(1, 0x0239), 76, 5, false, true, true),
            [10] = new(10, 0x023A, text(1, 0x023A), 76, 5, false, true, true),
            [11] = new(11, 0x023B, text(1, 0x023B), 76, 5, false, true, true),
            [12] = new(12, 0x023C, text(1, 0x023C), 76, 5, false, true, true),
            [18] = new(18, 0x0229, text(1, 0x0229), 76, 4, true, true, true),
            [24] = new(24, 0x0244, text(1, 0x0244), 76, 4, true, true, true),
            [28] = new(28, 0x042A, text(1, 0x042A), 76, 5, false, true, true),
            [29] = new(29, 0x042B, text(1, 0x042B), 76, 4, true, true, true),
            [30] = new(30, 0x042C, text(1, 0x042C), 132, 4, true, true, true),
            [31] = new(31, 0x042D, text(1, 0x042D), 132, 4, true, true, true),
            [32] = new(32, 0x042E, text(1, 0x042E), 132, 4, true, true, true),
            [33] = new(33, 0x042F, text(1, 0x042F), 132, 4, true, true, true),
            [61] = new(61, 0x03E2, text(1, 0x03E2), 104, 4, true, true, true),
            [62] = new(62, 0x03E3, text(1, 0x03E3), 104, 4, true, true, true),
            [63] = new(63, 0x03E4, text(1, 0x03E4), 104, 4, true, true, true),
            [64] = new(64, 0x03E5, text(1, 0x03E5), 104, 4, true, true, true),
            [65] = new(65, 0x03E6, text(1, 0x03E6), 104, 4, true, true, true),
            [78] = new(78, 0x044E, text(1, 0x044E), 132, 4, true, true, true),
            [79] = new(79, 0x020C, text(1, 0x020C), 104, 5, false, true, true),
            [113] = new(113, 0x0510, text(1, 0x0510), 76, 4, false, true, true),
            [194] = new(194, 0x0822, text(1, 0x0822), 160, 4, false, true, true),
        };
        int[][] ids = [[0,4,78,194,8,9,10,11,12,79,113,18],
            [0,61,62,63,64,65],[0,29,28,24,30,31,32,33]];
        int[] captions = [0x03CD,0x023D,0x0451];
        return ids.Select((group,index) => new OfficerInformationGroup(index,captions[index],text(1,captions[index]),
            group.Select(id => columns[id]).ToImmutableArray())).ToImmutableArray();
    }
}
