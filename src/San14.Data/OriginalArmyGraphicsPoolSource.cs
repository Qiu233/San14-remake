using System.Buffers.Binary;
using System.Security.Cryptography;

namespace San14.Data;

public readonly record struct OriginalArmyGraphicsPoolBank(uint Category, uint Capacity,
    uint Kind, uint Flags);

/// <summary>Requested graphics banks from the original 4C-byte manager
/// table. This once-loaded source owns no model slots or allocation policy.</summary>
public sealed class OriginalArmyGraphicsPoolSource
{
    public string Language { get; }
    public string ExecutableSha256 { get; }
    public uint TableRva { get; }
    public string TableSha256 { get; }
    public IReadOnlyList<OriginalArmyGraphicsPoolBank> Banks { get; }

    internal OriginalArmyGraphicsPoolSource(string language, string executableSha256,
        uint tableRva, ReadOnlySpan<byte> table)
    {
        Language = language;
        ExecutableSha256 = executableSha256;
        TableRva = tableRva;
        TableSha256 = Convert.ToHexString(SHA256.HashData(table));
        var banks = new OriginalArmyGraphicsPoolBank[table.Length / 0x4C];
        for (int category = 0; category < banks.Length; category++)
        {
            ReadOnlySpan<byte> row = table.Slice(category * 0x4C, 0x4C);
            banks[category] = new((uint)category,
                BinaryPrimitives.ReadUInt32LittleEndian(row[0x40..]),
                BinaryPrimitives.ReadUInt32LittleEndian(row[0x48..]),
                BinaryPrimitives.ReadUInt32LittleEndian(row[0x44..]));
        }
        Banks = Array.AsReadOnly(banks);
    }

    public static OriginalArmyGraphicsPoolSource Load(OriginalGameRepository repository,
        string language) => Load(repository,language,4);
    public static OriginalArmyGraphicsPoolSource Load(OriginalGameRepository repository,
        string language, int categoryCount) => LoadSourceDirectory(repository.SourceDirectory, language, categoryCount);

    public static OriginalArmyGraphicsPoolSource LoadSourceDirectory(string sourceDirectory,
        string language) => LoadSourceDirectory(sourceDirectory,language,4);
    public static OriginalArmyGraphicsPoolSource LoadSourceDirectory(string sourceDirectory,
        string language, int categoryCount) => OriginalDeferredSamplerSources.ReadArmyGraphicsPoolSource(sourceDirectory, language, categoryCount);

    /// <summary>ARMYPOL1, count, then category/capacity/kind/flags DWORDs.
    /// Source hash and RVA belong in the import metadata, outside this packet.</summary>
    public byte[] Encode()
    {
        byte[] result = new byte[12 + Banks.Count * 16];
        "ARMYPOL1"u8.CopyTo(result);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(8), (uint)Banks.Count);
        for (int index = 0; index < Banks.Count; index++)
        {
            OriginalArmyGraphicsPoolBank bank = Banks[index];
            Span<byte> row = result.AsSpan(12 + index * 16, 16);
            BinaryPrimitives.WriteUInt32LittleEndian(row, bank.Category);
            BinaryPrimitives.WriteUInt32LittleEndian(row[4..], bank.Capacity);
            BinaryPrimitives.WriteUInt32LittleEndian(row[8..], bank.Kind);
            BinaryPrimitives.WriteUInt32LittleEndian(row[12..], bank.Flags);
        }
        return result;
    }
}

public static partial class OriginalDeferredSamplerSources
{
    internal static OriginalArmyGraphicsPoolSource ReadArmyGraphicsPoolSource(
        string sourceDirectory, string language, int categoryCount)
    {
        if (categoryCount < 1 || categoryCount > OriginalCeArmyModels.KnownSourceCategories.Count)
            throw new ArgumentOutOfRangeException(nameof(categoryCount));
        var profile = Profile(language);
        byte[] exe = File.ReadAllBytes(Path.Combine(sourceDirectory, $"SAN14CE_{language}.exe"));
        VerifyHash(exe, profile.ExeHash, "Army graphics pool executable");
        var image = new NativeImage(exe);
        int rva = language switch
        {
            "TC" => 0x1A22000,
            "SC" => 0x1A23000,
            _ => throw new InvalidDataException("Army graphics pools require the pinned SC or TC source.")
        };
        return new(language, profile.ExeHash, (uint)rva, image.Raw(rva, categoryCount * 0x4C));
    }
}
