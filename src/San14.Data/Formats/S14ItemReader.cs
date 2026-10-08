using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace San14.Data.Formats;

public sealed record S14ItemDefinition(int Slot, string Name, string? SecondaryText,
    byte RawKind, byte RawValue, int RecordOffset, int RecordStride, byte[] RawBytes,
    string RawSha256);

public sealed record S14ScenarioItem(int Slot, ushort OwnerId,
    S14ItemDefinition? EmbeddedDefinition, int RecordOffset, int RecordStride,
    byte[] RawBytes, string RawSha256)
{
    // Missing typed metadata in an older snapshot remains unknown.
    public byte? RawStatusCode { get; init; }
}

public sealed record S14ItemDefinitionCatalog(uint FormatMarker, int RecordOffset,
    int RecordStride, string DecodedSha256, string TableSha256,
    IReadOnlyList<S14ItemDefinition> Definitions);

public sealed record S14ScenarioItemCatalog(uint FormatMarker, int RecordOffset,
    int RecordStride, string DecodedSha256, string TableSha256,
    IReadOnlyList<S14ScenarioItem> Items);

/// <summary>
/// Reads the inspected item tables without inventing ownership, item-kind meanings,
/// or a merged catalog. PK/CE scenario definitions are retained independently from
/// fixed data; base scenarios contain ownership/state bytes only.
/// </summary>
public static class S14ItemReader
{
    private static readonly UnicodeEncoding StrictUtf16 = new(false, false, true);

    public static S14ItemDefinitionCatalog ReadDefinitions(S14Document fixedData)
    {
        ArgumentNullException.ThrowIfNull(fixedData);
        (int start, int count) = fixedData.FormatMarker switch
        {
            0x33 => (206085, 201),
            0x5C => (207249, 201),
            0x66 => (207471, 202),
            _ => throw new InvalidDataException("Unsupported S14 edition for item definitions.")
        };
        const int stride = 22;
        ReadOnlySpan<byte> bytes = fixedData.DecodedPayload;
        ValidateTable(bytes, fixedData.FormatMarker, start, count, stride);
        var records = new S14ItemDefinition[count];
        for (int slot = 0; slot < records.Length; slot++)
        {
            int offset = start + slot * stride;
            ReadOnlySpan<byte> row = bytes.Slice(offset, stride);
            records[slot] = new S14ItemDefinition(slot, ReadText(row[..14], slot, "name"),
                null, row[15], row[16], offset, stride, row.ToArray(), Sha(row));
        }
        return new(fixedData.FormatMarker, start, stride, Sha(bytes),
            Sha(bytes.Slice(start, count * stride)), Array.AsReadOnly(records));
    }

    public static S14ScenarioItemCatalog ReadScenarioItems(S14Document scenario, string locale)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        bool simplified = locale switch
        {
            "SC" => true,
            "TC" => false,
            _ => throw new InvalidDataException($"Unsupported S14 locale for items: {locale}.")
        };
        (int scStart, int tcStart, int count, int stride, int idCount) = scenario.FormatMarker switch
        {
            0x33 => (592322, 505466, 201, 6, 1552),
            0x5C => (643345, 550889, 201, 65, 1652),
            0x66 => (733160, 629504, 202, 65, 1852),
            _ => throw new InvalidDataException("Unsupported S14 edition for scenario items.")
        };
        if (scenario.LeadingRecordCount != idCount)
            throw new InvalidDataException("S14 scenario item profile has a different leading character-ID count.");
        int start = simplified ? scStart : tcStart;
        ReadOnlySpan<byte> bytes = scenario.DecodedPayload;
        ValidateTable(bytes, scenario.FormatMarker, start, count, stride);
        var records = new S14ScenarioItem[count];
        for (int slot = 0; slot < records.Length; slot++)
        {
            int offset = start + slot * stride;
            ReadOnlySpan<byte> row = bytes.Slice(offset, stride);
            S14ItemDefinition? embedded = null;
            if (stride == 65)
            {
                ReadOnlySpan<byte> definition = row[6..];
                embedded = new S14ItemDefinition(slot, ReadText(definition[..14], slot, "name"),
                    ReadText(definition.Slice(14, 38), slot, "secondary text"),
                    row[59], row[60], offset + 6, definition.Length, definition.ToArray(), Sha(definition));
            }
            records[slot] = new S14ScenarioItem(slot, BinaryPrimitives.ReadUInt16LittleEndian(row),
                embedded, offset, stride, row.ToArray(), Sha(row)) { RawStatusCode = row[4] };
        }
        return new(scenario.FormatMarker, start, stride, Sha(bytes),
            Sha(bytes.Slice(start, count * stride)), Array.AsReadOnly(records));
    }

    private static void ValidateTable(ReadOnlySpan<byte> bytes, uint marker, int start, int count, int stride)
    {
        if (bytes.Length < 4 || BinaryPrimitives.ReadUInt32LittleEndian(bytes) != marker)
            throw new InvalidDataException("S14 item payload differs from its declared format marker.");
        RequireHeader(bytes, start - 8, 13, count);
        RequireHeader(bytes, start + count * stride, 14, 101);
    }

    private static void RequireHeader(ReadOnlySpan<byte> bytes, int offset, int section, int count)
    {
        if (offset < 0 || offset > bytes.Length - 8 ||
            BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, 4)) != section ||
            BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset + 4, 4)) != count)
            throw new InvalidDataException($"S14 item section {section} boundary or count differs.");
    }

    private static string ReadText(ReadOnlySpan<byte> bytes, int slot, string field)
    {
        string value;
        try { value = StrictUtf16.GetString(bytes); }
        catch (DecoderFallbackException error)
        {
            throw new InvalidDataException($"S14 item record {slot} {field} has invalid UTF-16LE.", error);
        }
        int end = value.IndexOf('\0');
        if (end < 0) return value;
        if (value.AsSpan(end).IndexOfAnyExcept('\0') >= 0)
            throw new InvalidDataException($"S14 item record {slot} {field} has nonzero text after its terminator.");
        return value[..end];
    }

    private static string Sha(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
