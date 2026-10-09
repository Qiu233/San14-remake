using System.Buffers.Binary;

namespace San14.Data;

/// <summary>One source packed RGBA value, before render-target view conversion.</summary>
public readonly record struct OriginalArmyFlagPaneColor(uint DiskPacked, uint RuntimePacked,
    uint PanePacked, byte Red, byte Green, byte Blue, byte Alpha)
{
    // SC 95FF8B / TC 95EEDB: multiply by the exact original reciprocal.
    private static float NativeReciprocal => BitConverter.UInt32BitsToSingle(0x3B808081);
    public float RedFloat => Red * NativeReciprocal;
    public float GreenFloat => Green * NativeReciprocal;
    public float BlueFloat => Blue * NativeReciprocal;
    public float AlphaFloat => Alpha * NativeReciprocal;
}

public sealed record OriginalArmyFlagPaneColorSelection(byte? SourceForceColorIndex49,
    bool UsedNativeFallback, OriginalArmyFlagPaneColor BackgroundTint,
    OriginalArmyFlagPaneColor BannerGlyphColor, uint FreshFlagGlyphPacked,
    string CallerSource);

public sealed record OriginalArmyFlagPaneGlyph(string TwoUtf16Units, int NativeCopyUnitCount,
    string CallerSource);

/// <summary>
/// Exact SC/TC flag-pane palette route. The caller supplies the source Force+49
/// byte, rather than a Core force identity. These tables are the native flag
/// helpers' tables. The 256 lookup bytes are derived from the bounded source
/// SSE/FMA powf arithmetic with IEEE rounding to nearest; no live LUT was read.
/// The result is a CPU color, not a claim about an original active frame.
/// </summary>
public sealed class OriginalArmyFlagPaneColors
{
    public const string FormulaId = "ce-sc-tc-army-flag-pane-selector1";
    private const string DerivedLutHex =
        "000000000000000000000000000000000000000000010101010101010102020202" +
        "02030303030304040404050505050606060707070808080909090A0A0A0B0B0C0C" +
        "0D0D0D0E0E0F0F1010111112121313141515161617171819191A1B1B1C1D1D1E1F" +
        "1F202121222324242526272828292A2B2C2D2D2E2F30313233343536373738393A3B" +
        "3C3D3E3F4142434445464748494A4B4D4E4F50515254555657585A5B5C5D5F606163" +
        "64656768696B6C6D6F707273757677797A7C7D7F8082838587888A8B8D8E90929395" +
        "97989A9C9D9FA1A2A4A6A8A9ABADAFB0B2B4B6B8BABBBDBFC1C3C5C7C9CBCDCF" +
        "D1D3D5D7D9DBDDDFE1E3E5E7E9EBEDEFF1F4F6F8FAFCFF";
    private static readonly byte[] Lut = Convert.FromHexString(DerivedLutHex);
    private readonly uint[] _first;
    private readonly uint[] _second;
    private readonly uint _fallback;

    public string Locale { get; }
    public string ImplementationVersion => RenderRuleIdentity.For("army-flag-pane-colors", Locale);
    public bool OriginalCurrentStateObserved => false;
    public string LutDerivation => "SC331A20/TC3317C0; SC101FB10/TC101EC10; " +
        "all256 finite inputs, SSE/FMA, IEEE rounding to nearest";

    private OriginalArmyFlagPaneColors(string locale, uint[] first, uint[] second, uint fallback)
        => (Locale, _first, _second, _fallback) = (locale, first, second, fallback);

    public static OriginalArmyFlagPaneColors LoadCurrent(string locale)
    {
        _ = RenderRuleIdentity.For("army-flag-pane-colors", locale);
        static uint[] LegacyWords(MapUiRuleColor[] colors) => colors.Select((color, index) =>
            index < 61 ? BinaryPrimitives.ReverseEndianness(color.Packed) : color.Packed).ToArray();
        return new(locale, LegacyWords(OriginalMapUiRules.ArmyBackgroundColors),
            LegacyWords(OriginalMapUiRules.ArmyGlyphColors), OriginalMapUiRules.ArmyFallbackColor.Packed);
    }

    /// <summary>
    /// SC2ACAD0/2ACB00, each called with selector1. Null and bytes62..255 take
    /// the general fallback; valid61 takes the distinct unchanged zero entry.
    /// Kind0/1 banner glyphs use the second table. Fresh kind2 glyphs retain
    /// the constructor's four FFFFFFFF primary corner colors.
    /// </summary>
    public OriginalArmyFlagPaneColorSelection Resolve(byte? explicitSourceForceColorIndex49,
        string callerSource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callerSource);
        bool fallback = !explicitSourceForceColorIndex49.HasValue ||
            explicitSourceForceColorIndex49.Value > 61;
        int index = fallback ? 61 : explicitSourceForceColorIndex49!.Value;
        OriginalArmyFlagPaneColor Decode(uint disk)
        {
            // SC330302..330374 reverses entries0..60, leaving entry61 unchanged.
            uint runtime = !fallback && index < 61
                ? BinaryPrimitives.ReverseEndianness(disk) : disk;
            byte r = Lut[(byte)runtime], g = Lut[(byte)(runtime >> 8)],
                b = Lut[(byte)(runtime >> 16)], a = (byte)(runtime >> 24);
            uint packed = (uint)(r | (g << 8) | (b << 16) | (a << 24));
            return new(disk, runtime, packed, r, g, b, a);
        }
        return new(explicitSourceForceColorIndex49, fallback,
            Decode(fallback ? _fallback : _first[index]),
            Decode(fallback ? _fallback : _second[index]), 0xFFFFFFFF, callerSource);
    }

    /// <summary>
    /// SC2ACFB0/30E110 supplies one UTF16 code unit and a zero unit to the
    /// text setter's explicit count2. The caller supplies the source-selected
    /// force name or native fallback name and its provenance. No officer/name
    /// or localization lookup is inferred by this helper.
    /// </summary>
    public static OriginalArmyFlagPaneGlyph CopySourceNameGlyph(string explicitSourceName,
        string callerSource)
    {
        ArgumentNullException.ThrowIfNull(explicitSourceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(callerSource);
        char first = explicitSourceName.Length == 0 ? '\0' : explicitSourceName[0];
        return new(new string([first, '\0']), 2, callerSource);
    }

    public static byte[] CopyDerivedLut() => Lut.ToArray();

}
