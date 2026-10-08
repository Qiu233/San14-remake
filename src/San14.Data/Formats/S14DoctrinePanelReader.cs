using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace San14.Data.Formats;

public sealed record S14DoctrinePanelDefinition(int Slot, string Name, short RawValue,
    int RecordOffset, int RecordStride, byte[] RawBytes, string RawSha256);

public sealed record S14DoctrinePanelCatalog(uint FormatMarker, int RecordOffset,
    int RecordStride, string DecodedSha256, string TableSha256,
    IReadOnlyList<S14DoctrinePanelDefinition> Panels);

/// <summary>
/// Reads the inspected CE fixed-data CDoctrinePanelData table. The signed value
/// has different consumers depending on the slot; all other fields remain raw.
/// This catalog does not establish unlocked panels or runtime force bitsets.
/// </summary>
public static class S14DoctrinePanelReader
{
    private const int Start = 362464, Count = 80, Stride = 33;
    private static readonly UnicodeEncoding StrictUtf16 = new(false, false, true);

    public static S14DoctrinePanelCatalog Read(S14Document fixedData)
    {
        ArgumentNullException.ThrowIfNull(fixedData);
        if (fixedData.FormatMarker != 0x66)
            throw new InvalidDataException("Unsupported S14 edition for doctrine panels; only the inspected CE profile is supported.");
        ReadOnlySpan<byte> bytes = fixedData.DecodedPayload;
        if (bytes.Length < 4 || BinaryPrimitives.ReadUInt32LittleEndian(bytes) != fixedData.FormatMarker)
            throw new InvalidDataException("S14 doctrine-panel payload differs from its declared format marker.");
        RequireHeader(bytes, 361394, 40, 31);
        RequireHeader(bytes, Start - 8, 41, Count);
        RequireHeader(bytes, Start + Count * Stride, 42, 7);
        var records = new S14DoctrinePanelDefinition[Count];
        for (int slot = 0; slot < records.Length; slot++)
        {
            int offset = Start + slot * Stride;
            ReadOnlySpan<byte> row = bytes.Slice(offset, Stride);
            records[slot] = new(slot, ReadName(row[..18], slot),
                BinaryPrimitives.ReadInt16LittleEndian(row.Slice(23, 2)),
                offset, Stride, row.ToArray(), Sha(row));
        }
        return new(fixedData.FormatMarker, Start, Stride, Sha(bytes),
            Sha(bytes.Slice(Start, Count * Stride)), Array.AsReadOnly(records));
    }

    private static void RequireHeader(ReadOnlySpan<byte> bytes, int offset, int section, int count)
    {
        if (offset < 0 || offset > bytes.Length - 8 ||
            BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, 4)) != section ||
            BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset + 4, 4)) != count)
            throw new InvalidDataException($"S14 doctrine-panel section {section} boundary or count differs.");
    }

    private static string ReadName(ReadOnlySpan<byte> bytes, int slot)
    {
        string value;
        try { value = StrictUtf16.GetString(bytes); }
        catch (DecoderFallbackException error)
        {
            throw new InvalidDataException($"S14 doctrine-panel record {slot} name has invalid UTF-16LE.", error);
        }
        int end = value.IndexOf('\0');
        if (end < 0) return value;
        if (value.AsSpan(end).IndexOfAnyExcept('\0') >= 0)
            throw new InvalidDataException($"S14 doctrine-panel record {slot} name has nonzero text after its terminator.");
        return value[..end];
    }

    private static string Sha(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
