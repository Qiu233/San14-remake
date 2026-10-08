using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace San14.Data.Formats;

public sealed record S14CreditRankDefinition(int Slot, string Name, string AuxiliaryText,
    ushort RawThreshold, short SignedThreshold, byte DoctrinePointAddition,
    uint RawTailValue, byte[] RawBytes);

public sealed record S14CreditRankCatalog(uint FormatMarker, int RecordOffset,
    int RecordStride, string DecodedSha256, IReadOnlyList<S14CreditRankDefinition> Ranks);

/// <summary>
/// Reads the verified CE CCreditRankData definitions. Its signed threshold is
/// distinct from the source word's raw bit view. Auxiliary text and the final
/// dword are preserved without assigning unverified UI or gameplay semantics.
/// Reading these definitions does not initialize any officer's merit or rank.
/// </summary>
public static class S14CreditRankReader
{
    private const int Start = 365112, Count = 7, Stride = 39;
    private static readonly UnicodeEncoding StrictUtf16 = new(false, false, true);

    public static S14CreditRankCatalog Read(S14Document fixedData)
    {
        ArgumentNullException.ThrowIfNull(fixedData);
        if (fixedData.FormatMarker != 0x66)
            throw new InvalidDataException("Unsupported S14 edition for credit ranks; only the inspected CE profile is supported.");
        ReadOnlySpan<byte> bytes = fixedData.DecodedPayload;
        if (bytes.Length < 4 || BinaryPrimitives.ReadUInt32LittleEndian(bytes) != fixedData.FormatMarker)
            throw new InvalidDataException("S14 credit-rank payload differs from its declared format marker.");
        RequireHeader(bytes, Start - 8, 42, Count);
        RequireHeader(bytes, Start + Count * Stride, 43, 100);
        var ranks = new S14CreditRankDefinition[Count];
        for (int slot = 0; slot < ranks.Length; slot++)
        {
            ReadOnlySpan<byte> row = bytes.Slice(Start + slot * Stride, Stride);
            ranks[slot] = new(slot, ReadText(row[..10], slot, "name"),
                ReadText(row.Slice(10, 22), slot, "auxiliary text"),
                BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(32, 2)),
                BinaryPrimitives.ReadInt16LittleEndian(row.Slice(32, 2)), row[34],
                BinaryPrimitives.ReadUInt32LittleEndian(row.Slice(35, 4)), row.ToArray());
        }
        return new(fixedData.FormatMarker, Start, Stride,
            Convert.ToHexString(SHA256.HashData(bytes)), Array.AsReadOnly(ranks));
    }

    private static void RequireHeader(ReadOnlySpan<byte> bytes, int offset, int section, int count)
    {
        if (offset < 0 || offset > bytes.Length - 8 ||
            BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, 4)) != section ||
            BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset + 4, 4)) != count)
            throw new InvalidDataException($"S14 credit-rank section {section} boundary or count differs.");
    }

    private static string ReadText(ReadOnlySpan<byte> bytes, int slot, string field)
    {
        string value;
        try { value = StrictUtf16.GetString(bytes); }
        catch (DecoderFallbackException error)
        {
            throw new InvalidDataException($"S14 credit-rank record {slot} {field} has invalid UTF-16LE.", error);
        }
        int end = value.IndexOf('\0');
        if (end < 0) return value;
        if (value.AsSpan(end).IndexOfAnyExcept('\0') >= 0)
            throw new InvalidDataException($"S14 credit-rank record {slot} {field} has nonzero text after its terminator.");
        return value[..end];
    }
}
