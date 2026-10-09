using System.Buffers.Binary;
using System.Security.Cryptography;

namespace San14.Data.Formats;

public sealed record S14CreditEffectDefinition(int Slot, byte RawTypeCode,
    ushort RawAddition, byte RawTailCode, byte[] RawBytes);

public sealed record S14CreditEffectCatalog(uint FormatMarker, int RecordOffset,
    int RecordStride, string DecodedSha256, IReadOnlyList<S14CreditEffectDefinition> Effects);

public sealed record S14CharacterCreditReference(int RecordSlot, ushort CharacterId,
    byte RawStageCode, ushort RawValue0, ushort RawValue1,
    IReadOnlyList<byte> RawSelectionCodes, byte RawTailCode, byte[] RawBytes)
{
    // Static CPersonData flags preceding the CE appendix. Null preserves an
    // older snapshot's missing field; the individual bits are not Core state.
    public ushort? RawFlagsWord { get; init; }
}

public sealed record S14CharacterCreditTable(int RecordOffset, int RecordStride,
    int CreditFieldOffset, IReadOnlyList<S14CharacterCreditReference> Characters);

/// <summary>
/// Reads the CE-only CCreditEffectData catalog and CPersonData's nine-byte appendix.
/// Source selection bytes are retained in their original order. The game derives two
/// counters from them; they are not three independently active effect references.
/// This catalog alone does not establish merit eligibility, rank, or policy level.
/// </summary>
public static class S14CreditEffectReader
{
    private const int Count = 7, Stride = 4, Start = 361366;

    public static S14CreditEffectCatalog Read(S14Document fixedData)
    {
        RequireCe(fixedData);
        ReadOnlySpan<byte> bytes = fixedData.DecodedPayload;
        RequireHeader(bytes, Start - 8, 39, Count);
        RequireHeader(bytes, Start + Count * Stride, 40, 31);
        var effects = new S14CreditEffectDefinition[Count];
        for (int slot = 0; slot < effects.Length; slot++)
        {
            ReadOnlySpan<byte> row = bytes.Slice(Start + slot * Stride, Stride);
            effects[slot] = new S14CreditEffectDefinition(slot, row[0],
                BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(1, 2)), row[3], row.ToArray());
        }
        return new S14CreditEffectCatalog(fixedData.FormatMarker, Start, Stride,
            Convert.ToHexString(SHA256.HashData(bytes)), Array.AsReadOnly(effects));
    }

    public static S14CharacterCreditTable ReadCharacterReferences(S14Document scenario, string locale)
    {
        RequireCe(scenario);
        S14CharacterTable table = S14ScenarioTables.ReadCharacterNames(scenario, locale);
        IReadOnlyList<S14CharacterIdentity> identities = S14ScenarioTables.ReadCharacterIdentities(scenario, locale);
        int field = locale == "SC" ? 363 : 307;
        var references = new S14CharacterCreditReference[table.Names.Count];
        for (int slot = 0; slot < references.Length; slot++)
        {
            ReadOnlySpan<byte> row = scenario.DecodedPayload.AsSpan(
                table.RecordOffset + slot * table.RecordStride + field, 9);
            references[slot] = new S14CharacterCreditReference(slot, identities[slot].CharacterId,
                row[0], BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(1, 2)),
                BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(3, 2)),
                Array.AsReadOnly(row.Slice(5, 3).ToArray()), row[8], row.ToArray())
            {
                RawFlagsWord = BinaryPrimitives.ReadUInt16LittleEndian(scenario.DecodedPayload.AsSpan(
                    table.RecordOffset + slot * table.RecordStride + field - 2, 2))
            };
        }
        return new S14CharacterCreditTable(table.RecordOffset, table.RecordStride,
            field, Array.AsReadOnly(references));
    }

    private static void RequireCe(S14Document document)
    {
        if (document.FormatMarker != 0x66)
            throw new InvalidDataException("S14 credit effects and character appendix require the CE profile (0x66).");
    }

    private static void RequireHeader(ReadOnlySpan<byte> bytes, int offset, int section, int count)
    {
        if (offset < 0 || offset > bytes.Length - 8 ||
            BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, 4)) != section ||
            BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset + 4, 4)) != count)
            throw new InvalidDataException($"S14 credit effect section {section} boundary or count differs.");
    }
}
