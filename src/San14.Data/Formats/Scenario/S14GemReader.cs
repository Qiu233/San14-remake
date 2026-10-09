using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace San14.Data.Formats;

public sealed record S14GemDefinition(int Slot, string Name, byte RawField52,
    byte RawField53, byte[] RawBytes);

public sealed record S14GemCatalog(uint FormatMarker, int RecordOffset,
    int RecordStride, string DecodedSha256, IReadOnlyList<S14GemDefinition> Gems);

/// <summary>CE fixed section19 CGemData, independently from section13 CItemData.
/// Scenario slot5 does not overwrite the fixed52/53 bytes.</summary>
public static class S14GemReader
{
    public const int Count = 31;
    public const int RecordStart = 257754;
    public const int RecordStride = 21;
    private static readonly UnicodeEncoding StrictUtf16 = new(false, false, true);

    public static S14GemCatalog ReadFixed(S14Document fixedData)
    {
        ArgumentNullException.ThrowIfNull(fixedData);
        ReadOnlySpan<byte> bytes = fixedData.DecodedPayload;
        if (fixedData.FormatMarker != 0x66 || bytes.Length < 4 ||
            BinaryPrimitives.ReadUInt32LittleEndian(bytes) != fixedData.FormatMarker)
            throw new InvalidDataException("Gem definitions require the inspected CE fixed-data profile.");
        Header(bytes, RecordStart - 8, 19, Count);
        Header(bytes, RecordStart + Count * RecordStride, 20, 400);
        var gems = new S14GemDefinition[Count];
        for (int slot = 0; slot < Count; slot++)
        {
            ReadOnlySpan<byte> row = bytes.Slice(RecordStart + slot * RecordStride, RecordStride);
            int end = 0;
            while (end < 14 && (row[end] != 0 || row[end + 1] != 0)) end += 2;
            string name;
            try { name = StrictUtf16.GetString(row[..end]); }
            catch (DecoderFallbackException error)
            { throw new InvalidDataException($"Gem definition {slot} has invalid UTF16 text.", error); }
            gems[slot] = new(slot, name, row[14], row[15], row.ToArray());
        }
        return new(fixedData.FormatMarker, RecordStart, RecordStride,
            Convert.ToHexString(SHA256.HashData(bytes)), Array.AsReadOnly(gems));
    }

    private static void Header(ReadOnlySpan<byte> bytes, int offset, int section, int count)
    {
        if (offset < 0 || offset > bytes.Length - 8 ||
            BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, 4)) != section ||
            BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset + 4, 4)) != count)
            throw new InvalidDataException($"Gem fixed section {section} boundary or count differs.");
    }
}
