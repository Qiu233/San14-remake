using San14.Data.Formats;

namespace San14.Data;

public readonly record struct AreaMarkerColor(uint AuthoredPacked, uint CpuPacked)
{
    public byte AuthoredRed => (byte)AuthoredPacked;
    public byte AuthoredGreen => (byte)(AuthoredPacked >> 8);
    public byte AuthoredBlue => (byte)(AuthoredPacked >> 16);
    public byte AuthoredAlpha => (byte)(AuthoredPacked >> 24);
    public byte Red => (byte)CpuPacked;
    public byte Green => (byte)(CpuPacked >> 8);
    public byte Blue => (byte)(CpuPacked >> 16);
    public byte Alpha => (byte)(CpuPacked >> 24);
}

public readonly record struct AreaMarkerRectangle(float Left, float Top, float Width, float Height);
public sealed record AreaMarkerRestGeometry(AreaMarkerRectangle FlagRectangle,
    AreaMarkerRectangle NameCellRectangle, float SourceLayoutRootX, float SourceLayoutRootY);

/// <summary>Normal CE county marker children and its player-district palette.
/// CPU packed colors are source linear UI values, not an instruction to change
/// the Canvas output transfer function. Special alliance/tutorial overrides
/// in 1B7110 remain distinct from the ordinary player-district branch.</summary>
public sealed class AreaMapMarkerSources
{
    private readonly uint[] districtColors;
    private readonly uint[] forceColors;
    public MapMarkerLayout Flag { get; }
    public MapMarkerLayout Text { get; }
    public const float WorldHeightOffset = 300;
    public const float FinalScreenOffsetY = -40;

    private AreaMapMarkerSources(uint[] districtColors,
        uint[] forceColors, MapMarkerLayout flag,
        MapMarkerLayout text)
        => (this.districtColors, this.forceColors, Flag, Text) =
            (districtColors, forceColors, flag, text);

    public static AreaMapMarkerSources LoadCurrentTc(GameResourceRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var colors = MapUiRules.DistrictColors.Select(color => color.Packed).ToArray();
        var forces = MapUiRules.ForceFillColors.Select(color => color.Packed).ToArray();
        return new(colors, forces,
            MapMarkerLayout.Load(repository, "00_81_07_btn_county_offset_00", 0x1933464B, "LCSK6500"),
            MapMarkerLayout.Load(repository, "00_81_07_btn_county_offset_01", 0xE2343D8C, "LCSK8500"));
    }

    /// <summary>2AAD40 -> 32C270(Number11,1); Number1..8, otherwise first entry.</summary>
    public AreaMarkerColor DistrictColor(int number)
    {
        uint source = districtColors[(uint)(number - 1) < 8 ? number - 1 : 0];
        return new(source, ApplyGamma(source));
    }

    /// <summary>1A6170 -> 2AD450(force,0), uses actual Force.Color49.</summary>
    public AreaMarkerColor ForceFillColor(int actualForceColorIndex)
    {
        // 2AD340 accepts indices0..59. Its fallback32C8C0(61) deliberately
        // selects DAT1A27660, the white table entry60, rather than disk slot61.
        uint source = forceColors[(uint)actualForceColorIndex < 60 ? actualForceColorIndex : 60];
        return new(source, ApplyGamma(source));
    }

    /// <summary>Ordinary normal-map player branch of 1B7110.
    /// Caller supplies District.force and the current player's actual force.</summary>
    public static bool ShowPlayerDistrictOutline(int? districtForceId, int? playerForceId)
        => playerForceId is >= 1 and <= 51 && districtForceId == playerForceId;

    /// <summary>19E940 table column for ordinary MapInfoMode0, after the
    /// source camera transition has selected its discrete UI level. This is
    /// not App render Mode288 and does not replace the transition controller.</summary>
    public static int NormalMapSettledDisplayLevel(int sourceCameraLevel)
        => sourceCameraLevel is >= 0 and <= 2 ? 0 : sourceCameraLevel == 3 ? 1 : -1;

    /// <summary>19C450 copies selected child's node1 hit_area. 1A8540 widens
    /// Text by actual UTF16 code-unit length, retaining the old left edge.
    /// This normal-state hit box is independent of glyph opacity and quads.</summary>
    public static AreaMarkerRectangle NormalHitRectangle(int utf16Length, bool showName)
        => new(-13, -31, showName ? 35 + 24 * utf16Length : 24, 26);

    /// <summary>Normal rest layout, relative to projection of the source placed
    /// CUnitCapital position plus (0,300,0). Source 1AAE20 root X uses text
    /// node6's local left (92D970); root screenY is -40. Parent anchors here
    /// are center11, source Y is upward, and own pivots use 0/1/2 halves.</summary>
    public AreaMarkerRestGeometry NormalRestGeometry()
    {
        var panel = Text.Nodes[6];
        float x = panel.HeaderValues[0] - ((panel.Value48 & 0xFF) >> 4) * panel.Value44 * panel.HeaderValues[7] * 0.5f;
        return new(Rect(Flag, 4, x), Rect(Text, 22, x), x, FinalScreenOffsetY);

        static AreaMarkerRectangle Rect(MapMarkerLayout layout, int index, float rootX)
        {
            var node = layout.Nodes[index];
            float px = 0, py = 0;
            for (LcskNodeRecord? n = node; n is not null; n = n.ParentIndex is int p ? layout.Nodes[p] : null)
            {
                px += n.HeaderValues[0]; py += n.HeaderValues[1];
            }
            float width = node.Value44 * node.HeaderValues[7], height = node.Value46 * node.HeaderValues[8];
            byte pivot = (byte)node.Value48;
            return new(rootX + px - (pivot >> 4) * width * 0.5f,
                FinalScreenOffsetY - py - (pivot & 15) * height * 0.5f, width, height);
        }
    }

    public static uint DarkenRgb(uint packed)
        => (packed & 0xFF000000) | (uint)(byte)((byte)packed * 0.4f)
            | (uint)(byte)((byte)(packed >> 8) * 0.4f) << 8
            | (uint)(byte)((byte)(packed >> 16) * 0.4f) << 16;

    private static uint ApplyGamma(uint packed)
        => (packed & 0xFF000000) | Gamma((byte)packed)
            | (uint)Gamma((byte)(packed >> 8)) << 8 | (uint)Gamma((byte)(packed >> 16)) << 16;

    private static byte Gamma(byte value) =>
        (byte)Math.Clamp((int)(MathF.Pow(value / 255f, 2.2f) * 255f), 0, 255);
}
