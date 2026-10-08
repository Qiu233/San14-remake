using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace San14.Data.Formats;

public sealed record S14PolicyDefinition(int Slot, string Name, string SecondaryText,
    string Description, IReadOnlyList<byte> RawEffectSlots, byte[] RawTailBytes, byte[] RawBytes)
{
    // Kept nullable so older serialized catalogs do not infer a value from diagnostic raw bytes.
    public ushort? RawCostAddition { get; init; }
}

public sealed record S14PolicyEffectDefinition(int Slot, string Name, string Description,
    IReadOnlyList<ushort> RawValues, byte[] RawBytes);

public sealed record S14PolicyCatalog(uint FormatMarker, int PolicyRecordOffset,
    int PolicyRecordStride, int EffectRecordOffset, int EffectRecordStride, string DecodedSha256,
    IReadOnlyList<S14PolicyDefinition> Policies, IReadOnlyList<S14PolicyEffectDefinition> Effects);

public sealed record S14CharacterPolicyReference(int RecordSlot, ushort CharacterId,
    byte RawPolicySlot, byte[] RawFieldBytes)
{
    public byte? RawPolicyBaseCost { get; init; }
}

public sealed record S14CharacterPolicyTable(int RecordOffset, int RecordStride,
    int PolicyFieldOffset, IReadOnlyList<S14CharacterPolicyReference> Characters)
{
    public int PolicyBaseCostFieldOffset { get; init; }
}

/// <summary>
/// Reads the verified fixed-data policy tables. All eight candidate effect references,
/// including zero and repeats, are retained. The ten raw values do not establish level
/// indexing, stacking, active policies or any command-book initialization formula.
/// </summary>
public static class S14PolicyReader
{
    private const int PolicyCount = 121, PolicyStride = 148;
    private const int EffectCount = 101, EffectStride = 134;
    private static readonly UnicodeEncoding StrictUtf16 = new(false, false, true);

    /// <summary>
    /// Retains the scenario's static policy-reference candidate and its seven neighboring
    /// bytes, plus the independently serialized personal policy base-cost byte. The seven
    /// neighboring bytes do not establish an active appointment or policy level.
    /// </summary>
    public static S14CharacterPolicyTable ReadCharacterReferences(S14Document scenario, string locale)
    {
        S14CharacterTable table = S14ScenarioTables.ReadCharacterNames(scenario, locale);
        IReadOnlyList<S14CharacterIdentity> identities = S14ScenarioTables.ReadCharacterIdentities(scenario, locale);
        int field = (scenario.FormatMarker == 0x33 ? 255 : 264) - (locale == "TC" ? 56 : 0);
        // The CE SC serializer's version branches account for the full 354/363/372-byte
        // SC records: the base profile serializes nine u8 values where PK/CE serialize
        // nine u16 values. The corresponding original TC record bytes were independently
        // checked at these offsets; this is not a decoded TC executable consumer claim.
        int costField = (scenario.FormatMarker == 0x33 ? 347 : 356) - (locale == "TC" ? 56 : 0);
        var references = new S14CharacterPolicyReference[table.Names.Count];
        for (int slot = 0; slot < references.Length; slot++)
        {
            ReadOnlySpan<byte> bytes = scenario.DecodedPayload.AsSpan(table.RecordOffset + slot * table.RecordStride + field, 8);
            if (bytes[0] >= PolicyCount)
                throw new InvalidDataException($"S14 character record {slot} policy field references missing slot {bytes[0]}.");
            references[slot] = new S14CharacterPolicyReference(slot, identities[slot].CharacterId, bytes[0], bytes.ToArray())
            {
                RawPolicyBaseCost = scenario.DecodedPayload[table.RecordOffset + slot * table.RecordStride + costField]
            };
        }
        return new S14CharacterPolicyTable(table.RecordOffset, table.RecordStride, field, Array.AsReadOnly(references))
        {
            PolicyBaseCostFieldOffset = costField
        };
    }

    public static S14PolicyCatalog Read(S14Document fixedData)
    {
        (int policyStart, int effectStart) = fixedData.FormatMarker switch
        {
            0x33 => (238404, 305491),
            0x5C => (239568, 323555),
            0x66 => (239838, 324181),
            _ => throw new InvalidDataException("Unsupported S14 edition for policy definitions.")
        };
        ReadOnlySpan<byte> bytes = fixedData.DecodedPayload;
        RequireHeader(bytes, policyStart - 8, 18, PolicyCount);
        RequireHeader(bytes, policyStart + PolicyCount * PolicyStride, 19, 31);
        RequireHeader(bytes, effectStart - 8, 26, EffectCount);
        RequireHeader(bytes, effectStart + EffectCount * EffectStride, 27, 501);

        var policies = new S14PolicyDefinition[PolicyCount];
        for (int slot = 0; slot < policies.Length; slot++)
        {
            ReadOnlySpan<byte> row = bytes.Slice(policyStart + slot * PolicyStride, PolicyStride);
            byte[] references = row.Slice(136, 8).ToArray();
            for (int index = 0; index < references.Length; index++)
                if (references[index] >= EffectCount)
                    throw new InvalidDataException($"S14 policy {slot} effect field {index} references missing slot {references[index]}.");
            policies[slot] = new S14PolicyDefinition(slot, ReadText(row[..12], 18, slot, "name"),
                ReadText(row.Slice(12, 32), 18, slot, "secondary text"),
                ReadText(row.Slice(44, 92), 18, slot, "description"), Array.AsReadOnly(references),
                row[144..].ToArray(), row.ToArray())
            {
                RawCostAddition = BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(146, 2))
            };
        }
        var effects = new S14PolicyEffectDefinition[EffectCount];
        for (int slot = 0; slot < effects.Length; slot++)
        {
            ReadOnlySpan<byte> row = bytes.Slice(effectStart + slot * EffectStride, EffectStride);
            var values = new ushort[10];
            for (int index = 0; index < values.Length; index++)
                values[index] = BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(114 + index * 2, 2));
            effects[slot] = new S14PolicyEffectDefinition(slot, ReadText(row[..22], 26, slot, "name"),
                ReadText(row.Slice(22, 92), 26, slot, "description"), Array.AsReadOnly(values), row.ToArray());
        }
        return new S14PolicyCatalog(fixedData.FormatMarker, policyStart, PolicyStride,
            effectStart, EffectStride, Convert.ToHexString(SHA256.HashData(bytes)),
            Array.AsReadOnly(policies), Array.AsReadOnly(effects));
    }

    private static void RequireHeader(ReadOnlySpan<byte> bytes, int offset, int section, int count)
    {
        if (offset < 0 || offset > bytes.Length - 8 ||
            BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, 4)) != section ||
            BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset + 4, 4)) != count)
            throw new InvalidDataException($"S14 policy section {section} boundary or count differs.");
    }

    private static string ReadText(ReadOnlySpan<byte> bytes, int section, int slot, string field)
    {
        string value;
        try { value = StrictUtf16.GetString(bytes); }
        catch (DecoderFallbackException error)
        {
            throw new InvalidDataException($"S14 policy section {section} record {slot} {field} has invalid UTF-16LE.", error);
        }
        int end = value.IndexOf('\0');
        if (end < 0) return value;
        if (value.AsSpan(end).IndexOfAnyExcept('\0') >= 0)
            throw new InvalidDataException($"S14 policy section {section} record {slot} {field} has nonzero text after its terminator.");
        return value[..end];
    }
}
