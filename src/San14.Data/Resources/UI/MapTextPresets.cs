
namespace San14.Data;

public sealed record MapTextPreset(AreaMarkerColor LT, AreaMarkerColor RT,
    AreaMarkerColor LB, AreaMarkerColor RB, AreaMarkerColor StrokeColor,
    int StrokeWidth, AreaMarkerColor OffsetLT, AreaMarkerColor OffsetRT,
    AreaMarkerColor OffsetLB, AreaMarkerColor OffsetRB, int OffsetX, int OffsetY)
{
    public bool HasStroke => StrokeColor.AuthoredPacked != 0;
    public bool HasOffset => OffsetLT.AuthoredPacked != 0;
}

/// <summary>The78 map text styles with authored and CPU colors.
/// Numeric definitions are project rules; glyphs remain source resources.</summary>
public sealed class MapTextPresets
{
    private readonly MapTextPreset[] _rows;
    private MapTextPresets(MapTextPreset[] rows) => _rows = rows;

    public static MapTextPresets Create()
    {
        return new(MapUiRules.TextStyles.Select(Read).ToArray());
    }

    // The source falls back to row0 for any index outside0..77.
    public MapTextPreset this[int index] => _rows[(uint)index < 78 ? index : 0];

    private static MapTextPreset Read(MapUiTextStyle row)
        => new(Color(row.Body.LT), Color(row.Body.RT), Color(row.Body.LB), Color(row.Body.RB), Color(row.Stroke),
            row.StrokeWidth, Color(row.Offset.LT), Color(row.Offset.RT), Color(row.Offset.LB), Color(row.Offset.RB), row.OffsetX, row.OffsetY);
    private static AreaMarkerColor Color(MapUiRuleColor color)
    {
        uint packed = color.Packed;
        uint cpu = (packed & 0xFF000000) | Gamma((byte)packed) | (uint)Gamma((byte)(packed >> 8)) << 8 |
            (uint)Gamma((byte)(packed >> 16)) << 16;
        return new(packed, cpu);
    }
    private static byte Gamma(byte value) => (byte)Math.Clamp((int)(MathF.Pow(value / 255f, 2.2f) * 255f), 0, 255);
}
