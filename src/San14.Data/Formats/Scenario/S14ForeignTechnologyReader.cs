using System.Buffers.Binary;

namespace San14.Data.Formats;

public sealed record S14ForeignTechnologyDefinition(int Slot, string Name, string Description, byte Value);
public sealed record LoadedForeignTechnologies(IReadOnlyList<S14ForeignTechnologyDefinition> Definitions,
    SourceProvenance Source);

/// <summary>Normal CE section37:23D7E0 serializes seven name units,
/// nineteen description units and the independent byte44.</summary>
public static class S14ForeignTechnologyReader
{
    public static IReadOnlyList<S14ForeignTechnologyDefinition> Read(S14Document document)
    {
        const int start = 355217, stride = 53, count = 9;
        ReadOnlySpan<byte> bytes = document.DecodedPayload;
        int end = start + count * stride;
        if (document.FormatMarker != 0x66 || bytes.Length < end + 8 ||
            BinaryPrimitives.ReadInt32LittleEndian(bytes[(start - 8)..]) != 37 ||
            BinaryPrimitives.ReadInt32LittleEndian(bytes[(start - 4)..]) != count ||
            BinaryPrimitives.ReadInt32LittleEndian(bytes[end..]) != 38 ||
            BinaryPrimitives.ReadInt32LittleEndian(bytes[(end + 4)..]) != 101)
            throw new InvalidDataException("Foreign technologies require the CE section37/9/53-byte resource profile.");
        var rows = new S14ForeignTechnologyDefinition[count];
        for (int slot = 0; slot < count; slot++)
        {
            var row = bytes.Slice(start + slot * stride, stride);
            rows[slot] = new(slot, S14ForceNameDefinitionReader.ReadLiteralUnits(row[..14]),
                S14ForceNameDefinitionReader.ReadLiteralUnits(row.Slice(14, 38)), row[52]);
        }
        return Array.AsReadOnly(rows);
    }
}
