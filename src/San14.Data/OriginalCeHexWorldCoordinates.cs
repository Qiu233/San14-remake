using San14.Core;

namespace San14.Data;

/// <summary>
/// CE Hex index to terrain X/Z calculation.
/// Constants and float32 operation order are part of this project?s rules;
/// scene selection and height sampling are separate unresolved questions.
/// </summary>
public static class OriginalCeHexWorldCoordinates
{
    public const string FormulaId = "ce-20260928-hex-world";
    public const int Width = 220;
    public const int Height = 220;
    public const double OriginX = 37594.0;
    public const double ColumnStep = 702.0;
    public const double InitializedOriginZ = 13335.2998046875;
    public const double RowStep = 810.5997924804688;
    public const double EvenColumnRowOffset = 0.5;

    public static string GetRuleVersion(string locale) => SimulationRuleIdentity.ForLocale(locale);

    public static ProjectedMapPoint Project(int hexId)
    {
        if ((uint)hexId >= Width * Height)
            throw new ArgumentOutOfRangeException(nameof(hexId));
        int column = hexId % Width, row = hexId / Width;
        float x = column * 702f;
        x += 37594f;

        float root3 = MathF.Sqrt(3f);
        // RVA 0x11D0 first-startup initializer: sqrtss, mulss, mulss, addss.
        float initialHalf = root3 * 0.5f;
        float initialOffset = initialHalf * 468f;
        float zOrigin = initialOffset + 12930f;
        // RVA 0x1888C0: even columns add a separate half-row product.
        float evenOffset = column % 2 == 0 ? (root3 * 468f) * 0.5f : 0f;
        float zStride = root3 * 468f;
        float z = zStride * row;
        z += evenOffset;
        z += zOrigin;
        return new ProjectedMapPoint(hexId, x, z);
    }
}
