using System.Buffers.Binary;
using System.Security.Cryptography;

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
/// Reads both current CE 62-entry color tables. Selector 1 follows the
/// second-table territory CPU path; selectors 0/2 decode the first table.
/// Their UI consumers and final framebuffer colors remain separate questions.
/// </summary>
public sealed class OriginalCeForceMapPalette
{
    public const string FormulaId = "ce-sc-20260928-force-map-cpu-color";
    public const string TcFormulaId = "ce-tc-20260928-force-map-cpu-color";
    public const string SourceTableSha256 =
        "B0637A9E16964F7B312EB0BA8794A2FD26817F1CAA2D41EA649CFC70B3522848";
    public const string SourceFirstTableSha256 =
        "1228847B12FC1521FEBCC00A1099480C98596D0F6E6A54E4C3048DDC851793C2";
    private const int ScTableFileOffset = 0x1A26EA0;
    private const int TcTableFileOffset = 0x1A25E70;
    private const int ScFirstTableFileOffset = 0x1A26DA0;
    private const int TcFirstTableFileOffset = 0x1A25D70;
    private const int TableLength = 62 * 4;
    private readonly uint[] _diskEntries;
    private readonly uint[] _firstDiskEntries;

    public string ExecutableSha256 { get; }
    public string TableSha256 { get; }
    public string FirstTableSha256 { get; }
    public string Locale { get; }
    public string SourceFormulaId => Locale == "TC" ? TcFormulaId : FormulaId;
    private OriginalCeForceMapPalette(uint[] diskEntries, uint[] firstDiskEntries,
        string executableSha256, string tableSha256, string firstTableSha256,
        string locale)
        => (_diskEntries, _firstDiskEntries, ExecutableSha256, TableSha256,
            FirstTableSha256, Locale) =
            (diskEntries, firstDiskEntries, executableSha256, tableSha256,
                firstTableSha256, locale);

    public static OriginalCeForceMapPalette LoadCurrentSc(string sourceDirectory)
        => LoadCurrent(sourceDirectory, "SC");

    public static OriginalCeForceMapPalette LoadCurrentTc(string sourceDirectory)
        => LoadCurrent(sourceDirectory, "TC");

    public static OriginalCeForceMapPalette LoadCurrent(string sourceDirectory, string locale)
    {
        (int tableOffset, int firstTableOffset) = locale switch
        {
            "SC" => (ScTableFileOffset, ScFirstTableFileOffset),
            "TC" => (TcTableFileOffset, TcFirstTableFileOffset),
            _ => throw new ArgumentOutOfRangeException(nameof(locale), locale,
                "Only the verified current CE SC and TC force map tables are supported.")
        };
        string executableSha = OriginalCeHexWorldCoordinates.VerifyInstalledExecutable(
            sourceDirectory, locale);
        string path = Path.Combine(sourceDirectory, $"SAN14CE_{locale}.exe");
        byte[] bytes = File.ReadAllBytes(path);
        if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(executableSha,
            StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("CE executable changed while reading its force color table.");
        if (bytes.Length < tableOffset + TableLength ||
            bytes.Length < firstTableOffset + TableLength)
            throw new InvalidDataException("Verified CE force color tables exceed the executable.");
        ReadOnlySpan<byte> table = bytes.AsSpan(tableOffset, TableLength);
        ReadOnlySpan<byte> firstTable = bytes.AsSpan(firstTableOffset, TableLength);
        string tableSha = Convert.ToHexString(SHA256.HashData(table));
        string firstTableSha = Convert.ToHexString(SHA256.HashData(firstTable));
        if (!tableSha.Equals(SourceTableSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("CE force map color table differs from the verified current source.");
        if (!firstTableSha.Equals(SourceFirstTableSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("CE first force color table differs from the verified current source.");
        var entries = new uint[62];
        var firstEntries = new uint[62];
        for (int index = 0; index < entries.Length; index++)
        {
            entries[index] = BinaryPrimitives.ReadUInt32LittleEndian(table[(index * 4)..]);
            firstEntries[index] = BinaryPrimitives.ReadUInt32LittleEndian(firstTable[(index * 4)..]);
        }
        return new(entries, firstEntries, executableSha, tableSha,
            firstTableSha, locale);
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
