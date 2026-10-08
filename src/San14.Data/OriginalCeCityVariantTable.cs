using System.Buffers.Binary;
using System.Security.Cryptography;

namespace San14.Data;

/// <summary>Reads the source CE city's 52-slot model variant table from the installed EXE.</summary>
public sealed class OriginalCeCityVariantTable
{
    public const string TableSha256 =
        "7A13DDA7B7FBB220BF47C4E4546B67DB2D721747DEE575FBC357E7B4E22009E9";
    private const int EntryCount = 52;
    private const int ScFileOffset = 0x1A20DA0;
    private const int TcFileOffset = 0x1A1FDA0;
    private readonly uint[] _variants;

    public string ExecutableSha256 { get; }
    public string Locale { get; }

    private OriginalCeCityVariantTable(uint[] variants, string executableSha256, string locale)
        => (_variants, ExecutableSha256, Locale) = (variants, executableSha256, locale);

    public static OriginalCeCityVariantTable LoadCurrent(string sourceDirectory, string locale)
    {
        int offset = locale switch
        {
            "SC" => ScFileOffset,
            "TC" => TcFileOffset,
            _ => throw new ArgumentOutOfRangeException(nameof(locale))
        };
        string executableSha = OriginalCeHexWorldCoordinates.VerifyInstalledExecutable(
            sourceDirectory, locale);
        byte[] exe = File.ReadAllBytes(Path.Combine(sourceDirectory, $"SAN14CE_{locale}.exe"));
        if (!Convert.ToHexString(SHA256.HashData(exe)).Equals(executableSha,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("CE executable changed while reading the city model table.");
        if (exe.Length < offset + EntryCount * sizeof(uint))
            throw new InvalidDataException("CE city model table exceeds its executable.");
        ReadOnlySpan<byte> raw = exe.AsSpan(offset, EntryCount * sizeof(uint));
        if (!Convert.ToHexString(SHA256.HashData(raw)).Equals(TableSha256,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("CE city model variant table differs from the verified source.");
        var variants = new uint[EntryCount];
        for (int index = 0; index < variants.Length; index++)
            variants[index] = BinaryPrimitives.ReadUInt32LittleEndian(raw[(index * 4)..]);
        return new(variants, executableSha, locale);
    }

    public int VariantForCitySlot(int slot)
    {
        if (slot is < 1 or >= EntryCount)
            throw new ArgumentOutOfRangeException(nameof(slot));
        uint variant = _variants[slot];
        if (variant > 4)
            throw new InvalidDataException($"CE city slot {slot} has no supported model variant.");
        return (int)variant;
    }
}
