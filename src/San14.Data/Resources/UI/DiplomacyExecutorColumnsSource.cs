using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

internal static class DiplomacyExecutorColumnsSource
{
    internal static ImmutableArray<OfficerInformationGroup> Create(DiplomaticMissionKind kind, Func<int,int,string> text)
    {
        var columns = new Dictionary<int, OfficerInformationColumn>
        {
            [0] = new(0, 0x0211, text(1, 0x0211), 132, 3, true, true, true),
            [5] = new(5, 0x0212, text(1, 0x0212), 76, 4, true, true, true),
            [11] = new(11, 0x023B, text(1, 0x023B), 76, 5, false, true, true),
            [18] = new(18, 0x0229, text(1, 0x0229), 76, 4, true, true, true),
            [24] = new(24, 0x0244, text(1, 0x0244), 76, 4, true, true, true),
            [28] = new(28, 0x042A, text(1, 0x042A), 76, 5, false, true, true),
            [29] = new(29, 0x042B, text(1, 0x042B), 76, 4, true, true, true),
            [30] = new(30, 0x042C, text(1, 0x042C), 132, 4, true, true, true),
            [31] = new(31, 0x042D, text(1, 0x042D), 132, 4, true, true, true),
            [32] = new(32, 0x042E, text(1, 0x042E), 132, 4, true, true, true),
            [33] = new(33, 0x042F, text(1, 0x042F), 132, 4, true, true, true),
            [35] = new(35, 0x03D4, text(1, 0x03D4), 104, 3, false, true, true),
            [75] = new(75, 0x044D, text(1, 0x044D), 132, 4, false, true, true),
            [102] = new(102, 0x026C, text(1, 0x026C), 76, 5, true, true, true),
            [105] = new(105, 0x051C, text(1, 0x051C), 76, 4, false, true, true),
            [194] = new(194, 0x0822, text(1, 0x0822), 160, 4, false, true, true),
        };
        int[][] ids = kind == DiplomaticMissionKind.CancelAlliance
            ? [[0,5,35,11,102,18,194],[0,29,28,24,30,31,32,33]]
            : [[0,75,5,35,11,105,102,18,194],[0,29,28,24,30,31,32,33]];
        int[] captions = [0x03CD,0x0451];
        return ids.Select((group,index) => new OfficerInformationGroup(index,captions[index],text(1,captions[index]),
            group.Select(id => columns[id]).ToImmutableArray())).ToImmutableArray();
    }
}
