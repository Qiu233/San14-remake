namespace San14.Data;

/// <summary>Project CE reference rules shared with stock portrait aliases.
/// The TC file values were extracted offline; sharing this catalog across CE
/// locales is a project profile choice, not native runtime immutability.</summary>
internal static class PersonPortraitTransitionCatalog
{
    internal readonly record struct Rule(int PersonId, short ThresholdYear,
        int Direction, int PortraitWhenFalse, int PortraitWhenTrue);

    internal static IReadOnlyList<Rule> Rules { get; } = Array.AsReadOnly<Rule>(
    [
        new(124, 194, 1, 7, 2197), new(952, 210, 0, 36, 2196),
        new(440, 223, 0, 45, 2195), new(147, 210, 0, 2, 2194),
        new(656, 210, 0, 1, 2193), new(596, 223, 0, 9, 2192),
        new(203, 249, 0, 84, 2191), new(521, 210, 0, 60, 2190),
        new(393, 203, 1, 44, 2189), new(344, 223, 0, 49, 2188),
        new(663, 212, 0, 14, 2187), new(618, 220, 0, 25, 2186),
        new(442, 212, 0, 37, 2185), new(553, 219, 1, 64, 2184),
        new(557, 200, 0, 63, 2183), new(379, 209, 0, 91, 2182),
        new(988, 210, 1, 30, 2181), new(902, 229, 0, 117, 2180),
        new(179, 215, 0, 17, 2179)
    ]);
}
