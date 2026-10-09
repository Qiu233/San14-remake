using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace San14.Data.Formats;

public sealed record S14FormationDefinition(int Slot, string Name, string Pinyin,
    string ShortText, string Description, byte[] RawParameterBytes, byte[] RawBytes);

public sealed record S14TacticDefinition(int Slot, string Name, string Pinyin,
    uint FormationMask, IReadOnlyList<int> FormationSlots,
    byte[] RawParameterBytes, byte[] RawBytes);

public sealed record S14TacticEffectDefinition(int Slot, string Name,
    byte[] RawParameterBytes, byte[] RawBytes);

public sealed record S14FormationCatalog(uint FormatMarker, int FormationRecordOffset,
    int FormationRecordStride, int TacticRecordOffset, int TacticRecordStride,
    int EffectRecordOffset, int EffectRecordStride, string DecodedSha256,
    IReadOnlyList<S14FormationDefinition> Formations,
    IReadOnlyList<S14TacticDefinition> Tactics,
    IReadOnlyList<S14TacticEffectDefinition> Effects);

public sealed record S14CharacterCombatReference(int RecordSlot, ushort CharacterId,
    uint FormationMask, IReadOnlyList<int> FormationSlots,
    IReadOnlyList<byte> TacticSlots, byte[] RawFieldBytes);

public sealed record S14CharacterCombatTable(int RecordOffset, int RecordStride,
    int FormationFieldOffset, int TacticFieldOffset,
    IReadOnlyList<S14CharacterCombatReference> Characters);

/// <summary>
/// Reads verified fixed-data formation/tactic records and scenario character references.
/// Stored references are not full runtime permissions; unverified numeric parameters,
/// reserved definitions, zero references and original bytes are preserved.
/// </summary>
public static class S14FormationReader
{
    private const int FormationCount = 21;
    private const int TacticCount = 201;
    private const int EffectStride = 22;
    private static readonly UnicodeEncoding StrictUtf16 = new(false, false, true);

    public static S14FormationCatalog Read(S14Document fixedData)
    {
        (int start, int formationStride, int tacticStride, int characterCount, int effectCount, int maskOffset) =
            fixedData.FormatMarker switch
            {
                0x33 => (0x2D3E3, 146, 88, 1551, 201, 84),
                0x5C => (0x2D419, 151, 93, 1651, 201, 89),
                0x66 => (0x2D419, 152, 94, 1851, 202, 89),
                _ => throw new InvalidDataException("Unsupported S14 edition for formation definitions.")
            };
        ReadOnlySpan<byte> bytes = fixedData.DecodedPayload;
        RequireHeader(bytes, start - 16, 10, characterCount);
        RequireHeader(bytes, start - 8, 11, FormationCount);
        int tacticStart = start + FormationCount * formationStride + 8;
        RequireHeader(bytes, tacticStart - 8, 12, TacticCount);
        int effectStart = tacticStart + TacticCount * tacticStride + 8;
        RequireHeader(bytes, effectStart - 8, 13, effectCount);
        RequireHeader(bytes, effectStart + effectCount * EffectStride, 14, 101);

        var formations = new List<S14FormationDefinition>(FormationCount);
        for (int slot = 0; slot < FormationCount; slot++)
        {
            ReadOnlySpan<byte> row = bytes.Slice(start + slot * formationStride, formationStride);
            formations.Add(new S14FormationDefinition(slot, ReadText(row[..6], 11, slot, "name", true),
                ReadText(row.Slice(6, 14), 11, slot, "pinyin"),
                ReadText(row.Slice(20, 8), 11, slot, "short text"),
                ReadText(row.Slice(28, 82), 11, slot, "description"), row[110..].ToArray(), row.ToArray()));
        }
        var tactics = new List<S14TacticDefinition>(TacticCount);
        for (int slot = 0; slot < TacticCount; slot++)
        {
            ReadOnlySpan<byte> row = bytes.Slice(tacticStart + slot * tacticStride, tacticStride);
            uint mask = BinaryPrimitives.ReadUInt32LittleEndian(row.Slice(maskOffset, 4));
            IReadOnlyList<int> references = FormationReferences(mask, $"Fixed tactic {slot}");
            tactics.Add(new S14TacticDefinition(slot, ReadText(row[..10], 12, slot, "name", true),
                ReadText(row.Slice(10, 38), 12, slot, "pinyin"),
                mask, references, row[48..].ToArray(), row.ToArray()));
        }
        var effects = new List<S14TacticEffectDefinition>(effectCount);
        for (int slot = 0; slot < effectCount; slot++)
        {
            ReadOnlySpan<byte> row = bytes.Slice(effectStart + slot * EffectStride, EffectStride);
            effects.Add(new S14TacticEffectDefinition(slot, ReadText(row[..14], 13, slot, "name", true),
                row[14..].ToArray(), row.ToArray()));
        }
        return new S14FormationCatalog(fixedData.FormatMarker, start, formationStride,
            tacticStart, tacticStride, effectStart, EffectStride,
            Convert.ToHexString(SHA256.HashData(bytes)), formations, tactics, effects);
    }

    public static S14CharacterCombatTable ReadCharacterReferences(S14Document scenario, string locale)
    {
        S14CharacterTable table = S14ScenarioTables.ReadCharacterNames(scenario, locale);
        IReadOnlyList<S14CharacterIdentity> identities = S14ScenarioTables.ReadCharacterIdentities(scenario, locale);
        int formationOffset = scenario.FormatMarker == 0x33 ? 241 : 250;
        if (locale == "TC") formationOffset -= 56;
        ReadOnlySpan<byte> bytes = scenario.DecodedPayload;
        int afterCharacters = checked(table.RecordOffset + table.Names.Count * table.RecordStride);
        RequireHeader(bytes, afterCharacters, 11, FormationCount);
        RequireHeader(bytes, afterCharacters + 8, 12, TacticCount);
        var characters = new List<S14CharacterCombatReference>(table.Names.Count);
        for (int slot = 0; slot < table.Names.Count; slot++)
        {
            ReadOnlySpan<byte> fields = bytes.Slice(table.RecordOffset + slot * table.RecordStride + formationOffset, 14);
            uint mask = BinaryPrimitives.ReadUInt32LittleEndian(fields[..4]);
            IReadOnlyList<int> formations = FormationReferences(mask, $"Character record {slot}");
            byte[] tactics = fields[4..].ToArray();
            int firstZero = Array.IndexOf(tactics, (byte)0);
            if (tactics.Any(tactic => tactic >= TacticCount) ||
                (firstZero >= 0 && tactics.AsSpan(firstZero).IndexOfAnyExcept((byte)0) >= 0) ||
                tactics.Where(tactic => tactic != 0).Distinct().Count() != tactics.Count(tactic => tactic != 0))
                throw new InvalidDataException($"Character record {slot} tactic references are out of range, repeated or have nonzero entries after padding.");
            characters.Add(new S14CharacterCombatReference(slot, identities[slot].CharacterId,
                mask, formations, tactics, fields.ToArray()));
        }
        return new S14CharacterCombatTable(table.RecordOffset, table.RecordStride,
            formationOffset, formationOffset + 4, characters);
    }

    private static IReadOnlyList<int> FormationReferences(uint mask, string owner)
    {
        if (mask >> FormationCount != 0)
            throw new InvalidDataException($"{owner} formation references include an unsupported slot.");
        return Enumerable.Range(0, FormationCount).Where(slot => (mask & (1u << slot)) != 0).ToArray();
    }

    private static void RequireHeader(ReadOnlySpan<byte> bytes, int offset, int section, int count)
    {
        if (offset < 0 || offset > bytes.Length - 8 ||
            BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, 4)) != section ||
            BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset + 4, 4)) != count)
            throw new InvalidDataException($"S14 combat section {section} boundary or count differs.");
    }

    private static string ReadText(ReadOnlySpan<byte> bytes, int section, int slot, string field, bool requireName = false)
    {
        string value;
        try { value = StrictUtf16.GetString(bytes); }
        catch (DecoderFallbackException error)
        {
            throw new InvalidDataException($"S14 combat section {section} record {slot} {field} has invalid UTF-16LE.", error);
        }
        int end = value.IndexOf('\0');
        if (end >= 0)
        {
            if (value.AsSpan(end).IndexOfAnyExcept('\0') >= 0)
                throw new InvalidDataException($"S14 combat section {section} record {slot} {field} has nonzero bytes after its terminator.");
            value = value[..end];
        }
        if (requireName && value.Length == 0)
            throw new InvalidDataException($"S14 combat section {section} record {slot} name is empty.");
        return value;
    }
}
