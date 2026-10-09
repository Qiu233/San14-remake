using System.Buffers.Binary;

namespace San14.Data;

public readonly record struct ArmyGraphicsPoolBank(uint Category, uint Capacity,
    uint Kind, uint Flags);

/// <summary>Graphics bank allocation rules. This descriptor owns no model
/// slots or current allocation state.</summary>
public sealed class ArmyGraphicsPoolSource
{
    public string Language { get; }
    public string RuleVersion => RenderRuleIdentity.Version;
    public IReadOnlyList<ArmyGraphicsPoolBank> Banks { get; }

    private ArmyGraphicsPoolSource(string language, int categoryCount)
    {
        Language = language;
        Banks = Array.AsReadOnly(ArmyGraphicsRules.PoolBanks.Take(categoryCount).ToArray());
    }

    public static ArmyGraphicsPoolSource Load(GameResourceRepository repository,
        string language) => Load(repository,language,4);
    public static ArmyGraphicsPoolSource Load(GameResourceRepository repository,
        string language, int categoryCount) => Create(language, categoryCount);

    public static ArmyGraphicsPoolSource LoadSourceDirectory(string sourceDirectory,
        string language) => LoadSourceDirectory(sourceDirectory,language,4);
    public static ArmyGraphicsPoolSource LoadSourceDirectory(string sourceDirectory,
        string language, int categoryCount) => Create(language, categoryCount);

    public static ArmyGraphicsPoolSource Create(string language, int categoryCount = 4)
    {
        RenderRuleIdentity.For("army-pools", language);
        if (categoryCount < 1 || categoryCount > ArmyGraphicsRules.PoolBanks.Count)
            throw new ArgumentOutOfRangeException(nameof(categoryCount));
        return new(language, categoryCount);
    }

    /// <summary>ARMYPOL1, count, then category/capacity/kind/flags DWORDs.
    /// The packet contains allocation rules, not current model slots.</summary>
    public byte[] Encode()
    {
        byte[] result = new byte[12 + Banks.Count * 16];
        "ARMYPOL1"u8.CopyTo(result);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(8), (uint)Banks.Count);
        for (int index = 0; index < Banks.Count; index++)
        {
            ArmyGraphicsPoolBank bank = Banks[index];
            Span<byte> row = result.AsSpan(12 + index * 16, 16);
            BinaryPrimitives.WriteUInt32LittleEndian(row, bank.Category);
            BinaryPrimitives.WriteUInt32LittleEndian(row[4..], bank.Capacity);
            BinaryPrimitives.WriteUInt32LittleEndian(row[8..], bank.Kind);
            BinaryPrimitives.WriteUInt32LittleEndian(row[12..], bank.Flags);
        }
        return result;
    }
}
