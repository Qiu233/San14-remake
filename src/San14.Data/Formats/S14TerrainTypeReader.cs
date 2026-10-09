using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace San14.Data.Formats;

public sealed record S14TerrainTypeDefinition(int Slot, string Name, string SecondaryText,
    ushort RawField2A, byte RawField2C, string ThirdText,
    byte RawField40, byte RawField41, byte RawField42, byte RawField43, byte[] RawBytes)
{
    public bool CountsTowardRegionalArea => RawField2A > 0;
}

public sealed record S14TerrainTypeCatalog(uint FormatMarker, int RecordOffset,
    int RecordStride, string DecodedSha256, IReadOnlyList<S14TerrainTypeDefinition> Types);

/// <summary>Reads fixed section8 terrain records. The positive count flag
/// marks terrain included in regional area totals; other undecoded fields stay at the format boundary.</summary>
public static class S14TerrainTypeReader
{
    private static readonly UnicodeEncoding StrictUtf16 = new(false, false, true);
    public static S14TerrainTypeCatalog Read(S14Document fixedData)
    {
        ArgumentNullException.ThrowIfNull(fixedData);
        int start = fixedData.FormatMarker switch
        {
            0x33 => 182098,
            0x5C or 0x66 => 182121,
            _ => throw new InvalidDataException("Unsupported S14 edition for terrain type definitions.")
        };
        ReadOnlySpan<byte> bytes = fixedData.DecodedPayload;
        if (bytes.Length < 4 || BinaryPrimitives.ReadUInt32LittleEndian(bytes) != fixedData.FormatMarker)
            throw new InvalidDataException("S14 terrain type payload marker differs.");
        RequireHeader(bytes, start - 8, 8, 31);
        RequireHeader(bytes, start + 31 * 51, 9, 31);
        var types = new S14TerrainTypeDefinition[31];
        for (int slot = 0; slot < types.Length; slot++)
        {
            ReadOnlySpan<byte> row = bytes.Slice(start + slot * 51, 51);
            types[slot] = new(slot, Text(row[..8], slot), Text(row.Slice(8, 18), slot),
                BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(26, 2)), row[28],
                Text(row.Slice(29, 18), slot), row[47], row[48], row[49], row[50], row.ToArray());
        }
        return new(fixedData.FormatMarker, start, 51, Convert.ToHexString(SHA256.HashData(bytes)),
            Array.AsReadOnly(types));
    }

    private static string Text(ReadOnlySpan<byte> raw, int slot)
    {
        int end = 0;
        while (end < raw.Length && (raw[end] != 0 || raw[end + 1] != 0)) end += 2;
        try { return StrictUtf16.GetString(raw[..end]); }
        catch (DecoderFallbackException error)
        { throw new InvalidDataException($"S14 terrain type {slot} has invalid UTF16 text.", error); }
    }

    private static void RequireHeader(ReadOnlySpan<byte> bytes, int offset, int section, int count)
    {
        if (offset < 0 || offset > bytes.Length - 8 ||
            BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, 4)) != section ||
            BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset + 4, 4)) != count)
            throw new InvalidDataException($"S14 terrain type section {section} boundary or count differs.");
    }
}
