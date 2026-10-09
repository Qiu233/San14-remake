using System.Buffers.Binary;

namespace San14.Data.Formats;

/// <summary>Unmodified CE static source fields for the later person slot7 callback.
/// References include all slots, repeats, zeros, reserved keys and formation bits.</summary>
public sealed record S14PersonSkillReference(int RecordSlot, ushort CharacterId,
    IReadOnlyList<ushort> RawPersonalitySlots150, uint RawFormationMask164,
    IReadOnlyList<byte> RawTacticSlots168, byte[] RawFieldBytes);

public sealed record S14PersonSkillTable(int RecordOffset, int RecordStride,
    int PersonalityFieldOffset, int FormationFieldOffset, int TacticFieldOffset,
    IReadOnlyList<S14PersonSkillReference> Characters);

public static class S14PersonSkillReader
{
    /// <summary>CE only. Other editions return unknown without borrowing CE layout.
    /// The static source omits runtime1BC/1BE fields; they are not inferred here.</summary>
    public static S14PersonSkillTable? ReadCharacterReferences(S14Document scenario, string locale)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        if (scenario.FormatMarker != 0x66) return null;

        S14CharacterTable table = S14ScenarioTables.ReadCharacterNames(scenario, locale);
        IReadOnlyList<S14CharacterIdentity> identities = S14ScenarioTables.ReadCharacterIdentities(scenario, locale);
        int personalityOffset = locale == "SC" ? 232 : 176;
        int formationOffset = personalityOffset + 18;
        int tacticOffset = formationOffset + 4;
        ReadOnlySpan<byte> bytes = scenario.DecodedPayload;
        int end = checked(table.RecordOffset + table.Names.Count * table.RecordStride);
        RequireHeader(bytes, table.RecordOffset - 8, 10, table.Names.Count);
        RequireHeader(bytes, end, 11, 21);
        RequireHeader(bytes, checked(end + 8), 12, 201);

        var characters = new S14PersonSkillReference[table.Names.Count];
        for (int slot = 0; slot < characters.Length; slot++)
        {
            ReadOnlySpan<byte> fields = bytes.Slice(
                checked(table.RecordOffset + slot * table.RecordStride + personalityOffset), 32);
            var personalities = new ushort[9];
            for (int index = 0; index < personalities.Length; index++)
                personalities[index] = BinaryPrimitives.ReadUInt16LittleEndian(fields.Slice(index * 2, 2));
            uint formationMask = BinaryPrimitives.ReadUInt32LittleEndian(fields.Slice(18, 4));
            byte[] tactics = fields.Slice(22, 10).ToArray();
            characters[slot] = new(slot, identities[slot].CharacterId,
                Array.AsReadOnly(personalities), formationMask, Array.AsReadOnly(tactics), fields.ToArray());
        }
        return new(table.RecordOffset, table.RecordStride, personalityOffset, formationOffset,
            tacticOffset, Array.AsReadOnly(characters));
    }

    private static void RequireHeader(ReadOnlySpan<byte> bytes, int offset, int section, int count)
    {
        if (offset < 0 || offset > bytes.Length - 8 ||
            BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, 4)) != section ||
            BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset + 4, 4)) != count)
            throw new InvalidDataException($"S14 person skill section {section} boundary or count differs.");
    }
}
