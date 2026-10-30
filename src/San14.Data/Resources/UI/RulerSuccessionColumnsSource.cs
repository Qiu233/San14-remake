using System.Collections.Immutable;

namespace San14.Data;

/// <summary>The successor dialog's eight authored Person groups.</summary>
internal static class RulerSuccessionColumnsSource
{
    internal static ImmutableArray<OfficerInformationGroup> Create(Func<int,int,string> text) =>
    [
        new(0, 0x452, text(1, 0x452), [
            new(0, 0x211, text(1, 0x211), 132, 3, true, true, true),
            new(138, 0x550, text(1, 0x550), 76, 4, true, true, true),
            new(8, 0x238, text(1, 0x238), 76, 5, false, true, true),
            new(9, 0x239, text(1, 0x239), 76, 5, false, true, true),
            new(10, 0x23A, text(1, 0x23A), 76, 5, false, true, true),
            new(11, 0x23B, text(1, 0x23B), 76, 5, false, true, true),
            new(12, 0x23C, text(1, 0x23C), 76, 5, false, true, true),
        ]),
        new(1, 0x22B, text(1, 0x22B), [
            new(0, 0x211, text(1, 0x211), 132, 3, true, true, true),
            new(40, 0x432, text(1, 0x432), 76, 4, false, true, true),
            new(41, 0x433, text(1, 0x433), 76, 4, false, true, true),
            new(42, 0x434, text(1, 0x434), 76, 4, false, true, true),
            new(43, 0x435, text(1, 0x435), 76, 4, false, true, true),
            new(44, 0x436, text(1, 0x436), 76, 4, false, true, true),
            new(45, 0x437, text(1, 0x437), 76, 4, false, true, true),
            new(46, 0x438, text(1, 0x438), 76, 4, false, true, true),
            new(47, 0x439, text(1, 0x439), 76, 4, false, true, true),
            new(48, 0x43A, text(1, 0x43A), 76, 4, false, true, true),
            new(49, 0x43B, text(1, 0x43B), 76, 4, false, true, true),
            new(50, 0x6FE, text(1, 0x6FE), 76, 4, false, true, true),
        ]),
        new(2, 0x3DD, text(1, 0x3DD), [
            new(0, 0x211, text(1, 0x211), 132, 3, true, true, true),
            new(51, 0x3DD, text(1, 0x3DD), 132, 4, true, true, true),
            new(52, 0x3DE, text(1, 0x3DE), 132, 4, true, true, true),
            new(53, 0x3DF, text(1, 0x3DF), 132, 4, true, true, true),
            new(54, 0x43C, text(1, 0x43C), 132, 4, true, true, true),
            new(55, 0x43D, text(1, 0x43D), 132, 4, true, true, true),
        ]),
        new(3, 0x3DE, text(1, 0x3DE), [
            new(0, 0x211, text(1, 0x211), 132, 3, true, true, true),
            new(56, 0x43E, text(1, 0x43E), 132, 4, true, true, true),
            new(57, 0x43F, text(1, 0x43F), 132, 4, true, true, true),
            new(58, 0x440, text(1, 0x440), 132, 4, true, true, true),
            new(59, 0x441, text(1, 0x441), 132, 4, true, true, true),
            new(60, 0x442, text(1, 0x442), 132, 4, false, true, true),
        ]),
        new(4, 0x241, text(1, 0x241), [
            new(0, 0x211, text(1, 0x211), 132, 3, true, true, true),
            new(98, 0x241, text(1, 0x241), 132, 4, true, true, true),
            new(99, 0x250, text(1, 0x250), 76, 5, false, true, true),
            new(27, 0x386, text(1, 0x386), 104, 5, false, true, true),
            new(101, 0x400, text(1, 0x400), 860, 3, true, false, true),
        ]),
        new(5, 0x23D, text(1, 0x23D), [
            new(0, 0x211, text(1, 0x211), 132, 3, true, true, true),
            new(61, 0x3E2, text(1, 0x3E2), 104, 4, true, true, true),
            new(62, 0x3E3, text(1, 0x3E3), 104, 4, true, true, true),
            new(63, 0x3E4, text(1, 0x3E4), 104, 4, true, true, true),
            new(64, 0x3E5, text(1, 0x3E5), 104, 4, true, true, true),
            new(65, 0x3E6, text(1, 0x3E6), 104, 4, true, true, true),
        ]),
        new(6, 0x821, text(1, 0x821), [
            new(0, 0x211, text(1, 0x211), 132, 3, true, true, true),
            new(194, 0x822, text(1, 0x822), 160, 4, false, true, true),
            new(193, 0x821, text(1, 0x821), 90, 5, false, true, true),
            new(196, 0x823, text(1, 0x823), 160, 5, false, true, true),
            new(197, 0x824, text(1, 0x824), 132, 5, false, true, true),
            new(198, 0x825, text(1, 0x825), 132, 5, false, true, true),
        ]),
        new(7, 0x451, text(1, 0x451), [
            new(0, 0x211, text(1, 0x211), 132, 3, true, true, true),
            new(29, 0x42B, text(1, 0x42B), 76, 4, true, true, true),
            new(28, 0x42A, text(1, 0x42A), 76, 5, false, true, true),
            new(24, 0x244, text(1, 0x244), 76, 4, true, true, true),
            new(30, 0x42C, text(1, 0x42C), 132, 4, true, true, true),
            new(31, 0x42D, text(1, 0x42D), 132, 4, true, true, true),
            new(32, 0x42E, text(1, 0x42E), 132, 4, true, true, true),
            new(33, 0x42F, text(1, 0x42F), 132, 4, true, true, true),
        ]),
    ];
}
