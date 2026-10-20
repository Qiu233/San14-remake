/// <summary>Page columns from the original initialized person-list
/// definitions. Message and column identities belong to the UI resource boundary.</summary>
internal sealed record DeploymentPersonColumn(int Id, int Width, int CaptionMessage,
    byte Align, byte LineArrange, bool InitialAscending = false);

internal sealed record DeploymentPersonCell(string Text, bool Emphasized = false, uint? FirstGlyphColour = null);

internal static class DeploymentPersonColumns
{
    internal static IReadOnlyList<DeploymentPersonColumn> Basic { get; } = Array.AsReadOnly<DeploymentPersonColumn>(
    [
        new(0,132,0x0211,0x01,1,true), new(22,90,0x03D2,0x21,3),
        new(8,76,0x0238,0x21,3), new(9,76,0x0239,0x21,3),
        new(10,76,0x023A,0x21,3), new(11,76,0x023B,0x21,3), new(12,76,0x023C,0x21,3),
        new(39,104,0x03D4,0x11,2), new(18,76,0x0229,0x11,2,true),
        new(19,76,0x0295,0x11,2,true), new(5,76,0x0212,0x11,2,true),
        new(194,160,0x0822,0x11,2), new(210,132,0x089D,0x11,2)
    ]);

    internal static IReadOnlyList<DeploymentPersonColumn> Formations { get; } = Array.AsReadOnly<DeploymentPersonColumn>(
    [
        new(0,132,0x0211,0x01,1,true),
        new(40,76,0x0432,0x11,2), new(41,76,0x0433,0x11,2),
        new(42,76,0x0434,0x11,2), new(43,76,0x0435,0x11,2),
        new(44,76,0x0436,0x11,2), new(45,76,0x0437,0x11,2),
        new(46,76,0x0438,0x11,2), new(47,76,0x0439,0x11,2),
        new(48,76,0x043A,0x11,2), new(49,76,0x043B,0x11,2),
        new(50,76,0x06FE,0x11,2)
    ]);

    internal static int FormationId(int columnId) => columnId switch
    {
        >= 40 and <= 49 => columnId - 39,
        50 => 14,
        _ => throw new ArgumentOutOfRangeException(nameof(columnId))
    };

    internal static IReadOnlyList<DeploymentPersonColumn> Linkage { get; } = Array.AsReadOnly<DeploymentPersonColumn>(
    [
        new(0,132,0x0211,0x01,1,true),
        new(212,132,0,0x11,2), new(213,132,0,0x11,2), new(214,132,0,0x11,2),
        new(215,132,0,0x11,2), new(216,132,0,0x11,2), new(217,132,0,0x11,2),
        new(218,132,0,0x11,2), new(219,132,0,0x11,2), new(220,132,0,0x11,2)
    ]);

    internal static IReadOnlyList<DeploymentPersonColumn> TacticsFirst { get; } = Array.AsReadOnly<DeploymentPersonColumn>(
    [
        new(0,132,0x0211,0x01,1,true),
        new(51,132,0x03DD,0x11,2,true), new(52,132,0x03DE,0x11,2,true),
        new(53,132,0x03DF,0x11,2,true), new(54,132,0x043C,0x11,2,true),
        new(55,132,0x043D,0x11,2,true)
    ]);

    internal static IReadOnlyList<DeploymentPersonColumn> TacticsSecond { get; } = Array.AsReadOnly<DeploymentPersonColumn>(
    [
        new(0,132,0x0211,0x01,1,true),
        new(56,132,0x043E,0x11,2,true), new(57,132,0x043F,0x11,2,true),
        new(58,132,0x0440,0x11,2,true), new(59,132,0x0441,0x11,2,true),
        new(60,132,0x0442,0x11,2)
    ]);

    internal static IReadOnlyList<DeploymentPersonColumn> Traits { get; } = Array.AsReadOnly<DeploymentPersonColumn>(
    [
        new(0,132,0x0211,0x01,1,true),
        new(61,104,0x03E2,0x11,2,true), new(62,104,0x03E3,0x11,2,true),
        new(63,104,0x03E4,0x11,2,true), new(64,104,0x03E5,0x11,2,true),
        new(65,104,0x03E6,0x11,2,true)
    ]);
}
