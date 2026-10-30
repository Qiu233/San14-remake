using System.Globalization;

namespace San14.Data;

public static class RegisteredOfficerPortraitCatalog
{
    private static readonly ArmyMapUiSources Names = ArmyMapUiSources.Create();
    internal static bool IsStockAppearance(int id) => id is >= 0 and <= 1310 or >= 2000 and <= 2199
        or >= 2500 and <= 2509 or >= 3000 and <= 3005 or >= 5000 and <= 5399;
    public static ArmyMapPortraitNames FacePortrait(int id)
    {
        if (!IsStockAppearance(id)) throw new ArgumentOutOfRangeException(nameof(id));
        return Names.PortraitNames((ushort)id);
    }
    // The ordinary registered-portrait category concatenates the two source
    // sex groups, retaining their actual declared gaps and single entries.
    private static readonly (int First, int Count)[] Male =
        [(2000, 50), (2080, 10), (2100, 11), (2118, 11), (2136, 8), (2152, 4), (2157, 7)];
    private static readonly (int First, int Count)[] Female =
        [(2050, 30), (2090, 10), (2111, 7), (2129, 7), (2144, 8), (2156, 1), (2164, 3)];
    public static IReadOnlyList<RegisteredOfficerEditorChoice> OrdinaryPortraits { get; } =
        Male.Concat(Female).SelectMany(range => Enumerable.Range(range.First, range.Count))
            .Select(id => new RegisteredOfficerEditorChoice(id, id.ToString(CultureInfo.InvariantCulture))).ToArray();
    public static DeploymentNarrationPortraitRoute StandingPortrait(int id)
    {
        if (!IsStockAppearance(id)) throw new ArgumentOutOfRangeException(nameof(id));
        ushort appearance = checked((ushort)id);
        if (DeploymentNarrationPortraitRoute.IsAnimatedAppearance(appearance))
            return DeploymentNarrationPortraitRoute.Animated(appearance);
        var face = FacePortrait(id);
        string number = face.ResolvedAppearanceIndex.ToString("D4", CultureInfo.InvariantCulture)
            + (face.ResolvedAppearanceIndex != id ? "b" : "");
        string stem = "face_" + number;
        return new(appearance, face.ResolvedAppearanceIndex, "texture_chara_anime_" + number,
            [stem + "_b", stem + "_a", stem + "_c"]);
    }
}
