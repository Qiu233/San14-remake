using San14.Data;

// Original page and column metadata stays at the UI resource boundary.
internal static class CityDomesticPersonColumns
{
    internal static IReadOnlyList<int> Captions { get; } = Array.AsReadOnly<int>(
        [0x03CD, 0x0219, 0x0452, 0x0821, 0x0221, 0x0451]);
    internal static IReadOnlyList<DeploymentPersonColumn> Recruiter { get; } = Array.AsReadOnly<DeploymentPersonColumn>(
    [
        new(0,132,0x0211,0x01,1,true),
        new(9,76,0x0239,0x21,3,false),
        new(121,76,0x0518,0x11,2,false),
        new(35,104,0x03D4,0x01,1,false),
        new(208,104,0x0895,0x01,1,false),
        new(209,104,0x0896,0x01,1,false),
        new(34,104,0x0430,0x11,2,false),
        new(36,76,0x0223,0x11,2,false),
        new(37,132,0x0221,0x11,2,true),
    ]);
    internal static IReadOnlyList<DeploymentPersonColumn> Trainer { get; } = Array.AsReadOnly<DeploymentPersonColumn>(
    [
        new(0,132,0x0211,0x01,1,true),
        new(8,76,0x0238,0x21,3,false),
        new(9,76,0x0239,0x21,3,false),
        new(122,76,0x0519,0x11,2,false),
        new(35,104,0x03D4,0x01,1,false),
        new(208,104,0x0895,0x01,1,false),
        new(209,104,0x0896,0x01,1,false),
        new(34,104,0x0430,0x11,2,false),
        new(36,76,0x0223,0x11,2,false),
        new(37,132,0x0221,0x11,2,true),
    ]);
    internal static IReadOnlyList<DeploymentPersonColumn> Affiliation { get; } = Array.AsReadOnly<DeploymentPersonColumn>(
    [
        new(0,132,0x0211,0x01,1,true),
        new(1,160,0x01FD,0x01,1,true),
        new(2,160,0x01FE,0x11,2,true),
        new(3,104,0x0219,0x11,2,true),
        new(4,160,0x021A,0x11,2,true),
        new(5,76,0x0212,0x11,2,true),
        new(35,104,0x03D4,0x01,1,false),
        new(6,76,0x021D,0x21,3,false),
        new(21,132,0x021C,0x11,2,true),
        new(23,76,0x0429,0x21,3,false),
        new(7,76,0x021E,0x21,3,false),
    ]);
    internal static IReadOnlyList<DeploymentPersonColumn> Abilities { get; } = Array.AsReadOnly<DeploymentPersonColumn>(
    [
        new(0,132,0x0211,0x01,1,true),
        new(5,76,0x0212,0x11,2,true),
        new(8,76,0x0238,0x21,3,false),
        new(9,76,0x0239,0x21,3,false),
        new(10,76,0x023A,0x21,3,false),
        new(11,76,0x023B,0x21,3,false),
        new(12,76,0x023C,0x21,3,false),
        new(18,76,0x0229,0x11,2,true),
        new(20,76,0x0246,0x21,3,false),
    ]);
    internal static IReadOnlyList<DeploymentPersonColumn> Credit { get; } = Array.AsReadOnly<DeploymentPersonColumn>(
    [
        new(0,132,0x0211,0x01,1,true),
        new(194,160,0x0822,0x11,2,false),
        new(193,90,0x0821,0x21,3,false),
        new(196,160,0x0823,0x21,3,false),
        new(197,132,0x0824,0x21,3,false),
        new(198,132,0x0825,0x21,3,false),
    ]);
    internal static IReadOnlyList<DeploymentPersonColumn> Task { get; } = Array.AsReadOnly<DeploymentPersonColumn>(
    [
        new(0,132,0x0211,0x01,1,true),
        new(37,132,0x0221,0x11,2,true),
        new(86,160,0x0299,0x11,2,true),
        new(88,76,0x0450,0x21,3,true),
        new(34,104,0x0430,0x11,2,false),
        new(204,216,0x08BB,0x21,3,true),
    ]);
    internal static IReadOnlyList<DeploymentPersonColumn> Other { get; } = Array.AsReadOnly<DeploymentPersonColumn>(
    [
        new(0,132,0x0211,0x01,1,true),
        new(29,76,0x042B,0x11,2,true),
        new(28,76,0x042A,0x21,3,false),
        new(24,76,0x0244,0x11,2,true),
        new(30,132,0x042C,0x11,2,true),
        new(31,132,0x042D,0x11,2,true),
        new(32,132,0x042E,0x11,2,true),
        new(33,132,0x042F,0x11,2,true),
    ]);
    internal static IReadOnlyList<DeploymentPersonColumn> For(CityDomesticDuty duty, int page = 0)
    {
        if (duty is not (CityDomesticDuty.Recruiter or CityDomesticDuty.Trainer))
            throw new ArgumentOutOfRangeException(nameof(duty));
        return page switch
        {
            0 => duty == CityDomesticDuty.Recruiter ? Recruiter : Trainer,
            1 => Affiliation,
            2 => Abilities,
            3 => Credit,
            4 => Task,
            5 => Other,
            _ => throw new ArgumentOutOfRangeException(nameof(page))
        };
    }
}
