using System.Buffers.Binary;

namespace San14.Data.Formats;

public sealed record S14PersonDuelConditionReference(int RecordSlot, ushort PersonId,
    short InitialQualificationAdjustment);

/// <summary>The CE static stream transfers twelve words after the independent
/// doctrine/next bytes. Only the first consumed word is projected here. F1 is
/// omitted by this stream and has no static-resource field.</summary>
public static class S14PersonDuelConditionReader
{
    public static IReadOnlyList<S14PersonDuelConditionReference>? Read(S14Document scenario, string locale)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        if (scenario.FormatMarker != 0x66) return null;
        if (locale is not ("TC" or "SC")) throw new InvalidDataException("Person duel source locale is unsupported.");
        var table = S14ScenarioTables.ReadCharacterNames(scenario, locale);
        var identities = S14ScenarioTables.ReadCharacterIdentities(scenario, locale);
        if (table.Names.Count != 1851 || table.RecordStride != (locale == "TC" ? 316 : 372))
            throw new InvalidDataException("Person duel static source has another layout.");
        ReadOnlySpan<byte> data = scenario.DecodedPayload;
        int end = checked(table.RecordOffset + 1851 * table.RecordStride);
        if (table.RecordOffset < 8 || end > data.Length - 8 ||
            BinaryPrimitives.ReadInt32LittleEndian(data.Slice(table.RecordOffset - 8, 4)) != 10 ||
            BinaryPrimitives.ReadInt32LittleEndian(data.Slice(table.RecordOffset - 4, 4)) != 1851 ||
            BinaryPrimitives.ReadInt32LittleEndian(data.Slice(end, 4)) != 11 ||
            BinaryPrimitives.ReadInt32LittleEndian(data.Slice(end + 4, 4)) != 21)
            throw new InvalidDataException("Person duel static source section boundary is invalid.");
        int field = locale == "TC" ? 276 : 332;
        var result = new S14PersonDuelConditionReference[1851];
        for (int slot = 0; slot < result.Length; slot++)
            result[slot] = new(slot, identities[slot].CharacterId,
                BinaryPrimitives.ReadInt16LittleEndian(data.Slice(table.RecordOffset + slot * table.RecordStride + field, 2)));
        return Array.AsReadOnly(result);
    }
}
