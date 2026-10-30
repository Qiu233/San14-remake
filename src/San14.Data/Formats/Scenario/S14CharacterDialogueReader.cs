using System.Buffers.Binary;
using System.Text;

namespace San14.Data.Formats;

public sealed record S14CharacterDialogue(byte Style, ushort BirthYear, byte FamilyGeneration,
    string GivenName, string CourtesyName)
{
    public byte VoiceType { get; init; }
    public byte SpecialVoiceType { get; init; }
}

/// <summary>Verified CE character serializer fields. Format offsets stay at
/// this import boundary; current narration never rereads the initial scenario.</summary>
public static class S14CharacterDialogueReader
{
    public static IReadOnlyList<S14CharacterDialogue> Read(S14Document document, string locale)
    {
        if (document.FormatMarker != 0x66 || locale is not ("SC" or "TC"))
            throw new InvalidDataException("Character dialogue fields require CE SC/TC.");
        var table = S14ScenarioTables.ReadCharacterNames(document, locale);
        int delta = locale == "SC" ? 56 : 0;
        int courtesyOffset = locale == "SC" ? 36 : 28;
        int courtesyBytes = locale == "SC" ? 18 : 6;
        var encoding = new UnicodeEncoding(false,false,true);
        var result = new S14CharacterDialogue[table.Names.Count];
        for (int slot = 0; slot < result.Length; slot++)
        {
            ReadOnlySpan<byte> row = document.DecodedPayload.AsSpan(table.RecordOffset + slot * table.RecordStride,table.RecordStride);
            string courtesy = encoding.GetString(row.Slice(courtesyOffset,courtesyBytes));
            int end = courtesy.IndexOf('\0');
            if (end >= 0) courtesy = courtesy[..end];
            result[slot] = new(row[211+delta],BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(219+delta,2)),
                row[230+delta],table.Names[slot].GivenName,courtesy)
                { VoiceType = row[209+delta], SpecialVoiceType = row[210+delta] };
        }
        return Array.AsReadOnly(result);
    }
}
