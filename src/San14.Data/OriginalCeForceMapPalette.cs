using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Current CE source map-color CPU vector, before any shader blend.</summary>
public readonly record struct OriginalCeForceMapColor(int Index, uint DiskPacked,
    uint RuntimePacked, uint MapPacked, byte Red, byte Green, byte Blue, byte Alpha)
{
    public float RedFloat => Red / 255f;
    public float GreenFloat => Green / 255f;
    public float BlueFloat => Blue / 255f;
    public float AlphaFloat => Alpha / 255f;
}

/// <summary>Current CE first-table CPU result for selector 0 (LUT) or 2
/// (direct channels). This does not identify any particular UI marker.</summary>
public readonly record struct OriginalCeForceFirstTableColor(int Index, int Selector,
    uint DiskPacked, uint RuntimePacked, uint CpuPacked,
    byte Red, byte Green, byte Blue, byte Alpha);

/// <summary>
/// Defines both CE 62-entry color tables. Selector 1 follows the
/// second-table territory CPU path; selectors 0/2 decode the first table.
/// Their UI consumers and final framebuffer colors remain separate questions.
/// </summary>
public sealed class OriginalCeForceMapPalette
{
    public const string FormulaId = "ce-sc-20260928-force-map-cpu-color";
    public const string TcFormulaId = "ce-tc-20260928-force-map-cpu-color";
    private readonly uint[] _diskEntries;
    private readonly uint[] _firstDiskEntries;

    public string ImplementationVersion => RenderRuleIdentity.For("force-map-palette", Locale);
    public string Locale { get; }
    public string SourceFormulaId => Locale == "TC" ? TcFormulaId : FormulaId;
    private OriginalCeForceMapPalette(uint[] diskEntries, uint[] firstDiskEntries, string locale)
        => (_diskEntries, _firstDiskEntries, Locale) = (diskEntries, firstDiskEntries, locale);

    public static OriginalCeForceMapPalette LoadCurrentSc() => LoadCurrent("SC");

    public static OriginalCeForceMapPalette LoadCurrentTc() => LoadCurrent("TC");

    public static OriginalCeForceMapPalette LoadCurrent(string locale)
    {
        _ = RenderRuleIdentity.For("force-map-palette", locale);
        static uint[] LegacyWords(MapUiRuleColor[] colors) => colors.Select(color =>
            BinaryPrimitives.ReverseEndianness(color.Packed)).ToArray();
        return new(LegacyWords(OriginalMapUiRules.TerritoryFillColors),
            LegacyWords(OriginalMapUiRules.ForceFillColors), locale);
    }

    public OriginalCeForceMapColor MapColor(int colorIndex)
    {
        if ((uint)colorIndex >= 60)
            throw new ArgumentOutOfRangeException(nameof(colorIndex), colorIndex,
                "CE map palette indices 60 and above use a separate source branch.");
        return DecodeMapColor(colorIndex, _diskEntries[colorIndex]);
    }

    public OriginalCeForceFirstTableColor FirstTableColor(int colorIndex, int selector)
    {
        if ((uint)colorIndex >= 60)
            throw new ArgumentOutOfRangeException(nameof(colorIndex), colorIndex,
                "CE force color indices 60 and above use a separate source branch.");
        return DecodeFirstTableColor(colorIndex, _firstDiskEntries[colorIndex], selector);
    }

    public static OriginalCeForceMapColor DecodeMapColor(int index, uint diskPacked)
    {
        if ((uint)index >= 60)
            throw new ArgumentOutOfRangeException(nameof(index));
        uint runtime = BinaryPrimitives.ReverseEndianness(diskPacked);
        byte red = GammaByte((byte)runtime);
        byte green = GammaByte((byte)(runtime >> 8));
        byte blue = GammaByte((byte)(runtime >> 16));
        byte alpha = (byte)((byte)(runtime >> 24) * 0.30000001192092896f);
        uint mapped = (uint)(red | (green << 8) | (blue << 16) | (alpha << 24));
        return new(index, diskPacked, runtime, mapped, red, green, blue, alpha);
    }

    public static OriginalCeForceFirstTableColor DecodeFirstTableColor(int index,
        uint diskPacked, int selector)
    {
        if ((uint)index >= 60)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (selector is not (0 or 2))
            throw new ArgumentOutOfRangeException(nameof(selector));
        uint runtime = BinaryPrimitives.ReverseEndianness(diskPacked);
        byte originalRed = (byte)runtime;
        byte originalGreen = (byte)(runtime >> 8);
        byte originalBlue = (byte)(runtime >> 16);
        byte red = selector == 0 ? GammaByte(originalRed) : originalRed;
        byte green = selector == 0 ? GammaByte(originalGreen) : originalGreen;
        byte blue = selector == 0 ? GammaByte(originalBlue) : originalBlue;
        byte alpha = (byte)(runtime >> 24);
        uint packed = (uint)(red | (green << 8) | (blue << 16) | (alpha << 24));
        return new(index, selector, diskPacked, runtime, packed,
            red, green, blue, alpha);
    }

    private static byte GammaByte(byte source)
    {
        float value = MathF.Pow(source / 255f, 2.2f) * 255f;
        return (byte)Math.Clamp((int)value, 0, 255);
    }
}
