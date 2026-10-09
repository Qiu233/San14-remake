
namespace San14.Data;

public readonly record struct OriginalMapCornerColors(OriginalAreaMarkerColor LT, OriginalAreaMarkerColor RT,
    OriginalAreaMarkerColor LB, OriginalAreaMarkerColor RB);

/// <summary>Source sprite palettes and the force-letter contrast selector.
/// Sprite palettes get the startup conversion; contrast colors use their own
/// numeric rules. Prepare once, retaining both authored and CPU colors.</summary>
public sealed class OriginalMapSpritePalettes
{
    private readonly OriginalMapCornerColors[] _rows;
    private readonly OriginalAreaMarkerColor[] _contrast;
    private readonly byte[] _contrastMap;
    private readonly byte _contrastFallback;
    public int MoraleStyleThreshold { get; }
    public int DurabilityStylePercent { get; }

    private OriginalMapSpritePalettes(OriginalMapCornerColors[] rows, OriginalAreaMarkerColor[] contrast,
        byte[] map, byte fallback, int moraleThreshold, int durabilityPercent)
        => (_rows, _contrast, _contrastMap, _contrastFallback, MoraleStyleThreshold, DurabilityStylePercent) =
            (rows, contrast, map, fallback, moraleThreshold, durabilityPercent);

    public static OriginalMapSpritePalettes Create()
    {
        var rows = OriginalMapUiRules.SpriteStyles.Select(row =>
            new OriginalMapCornerColors(Read(row.LT), Read(row.RT), Read(row.LB), Read(row.RB))).ToArray();
        return new(rows, OriginalMapUiRules.ForceLetterColors.Select(Read).ToArray(),
            OriginalMapUiRules.ForceLetterStyleIndices, OriginalMapUiRules.ForceLetterFallback,
            OriginalMapUiRules.MoraleStyleThreshold, OriginalMapUiRules.DurabilityStylePercent);
    }

    public OriginalMapCornerColors this[int index] => _rows[(uint)index < 71 ? index : 0];

    // 2AD610 ->32C950: this is neither Force fill nor a text preset number.
    public OriginalAreaMarkerColor ForceLetter(int actualForceColor49)
        => _contrast[(uint)actualForceColor49 < 62 ? _contrastMap[actualForceColor49] : _contrastFallback];

    private static OriginalAreaMarkerColor Read(MapUiRuleColor color)
    {
        uint authored = color.Packed;
        uint cpu = (authored & 0xFF000000) | Gamma((byte)authored) | (uint)Gamma((byte)(authored >> 8)) << 8 |
            (uint)Gamma((byte)(authored >> 16)) << 16;
        return new(authored, cpu);
    }
    private static byte Gamma(byte value) => (byte)Math.Clamp((int)(MathF.Pow(value / 255f, 2.2f) * 255f), 0, 255);
}
