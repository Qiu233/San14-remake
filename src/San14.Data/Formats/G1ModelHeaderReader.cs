using System.Buffers.Binary;
using System.Text;

namespace San14.Data.Formats;

public sealed record G1ModelChunkHeader(int Index, string Signature, int Offset, int Length,
    byte[] RawHeader);

public sealed record G1ModelHeaders(string Signature, int DeclaredLength, int HeaderLength,
    uint UnknownValue16, IReadOnlyList<G1ModelChunkHeader> Chunks, byte[] RawHeader);

/// <summary>
/// Reads verified FieldEditor4 and CharacterEditor G1M header chains from a bounded decoded prefix.
/// Chunk bodies, vertices, materials and coordinates remain unparsed.
/// </summary>
public static class G1ModelHeaderReader
{
    public static G1ModelHeaders Read(ReadOnlySpan<byte> prefix, long indexedDecodedSize)
    {
        if (prefix.Length < 24 || !prefix[..8].SequenceEqual("_M1G7300"u8))
            throw new InvalidDataException("Unsupported or truncated G1M model header.");
        int total = ReadInt(prefix, 8);
        int headerLength = ReadInt(prefix, 12);
        uint unknown = BinaryPrimitives.ReadUInt32LittleEndian(prefix[16..20]);
        int count = ReadInt(prefix, 20);
        if (total != indexedDecodedSize || total < 24 || prefix.Length > total ||
            headerLength != 24 || count is not (4 or 5))
            throw new InvalidDataException("G1M size or header chain differs from the verified format.");
        var chunks = new List<G1ModelChunkHeader>(count);
        int offset = headerLength;
        for (int i = 0; i < count; i++)
        {
            if (offset > prefix.Length - 12)
                throw new InvalidDataException("The decoded prefix does not contain every G1M chunk header.");
            ReadOnlySpan<byte> header = prefix.Slice(offset, 12);
            bool supported = i switch
            {
                0 => header[..8].SequenceEqual("FM1G0300"u8) ||
                    header[..8].SequenceEqual("FM1G3300"u8) || header[..8].SequenceEqual("FM1G4300"u8),
                1 => header[..8].SequenceEqual("SM1G2300"u8),
                2 => header[..8].SequenceEqual("MM1G0200"u8),
                3 => header[..8].SequenceEqual("GM1G4400"u8),
                // Current CharacterEditor Army models retain this separate
                // four-byte extension body. It is not geometry or animation.
                4 => header[..8].SequenceEqual("RTXE1100"u8),
                _ => false
            };
            int length = ReadInt(header, 8);
            if (!supported || length < 12 || length > total - offset || (i == 4 && length != 16))
                throw new InvalidDataException($"G1M chunk {i} has an unsupported signature or invalid boundary.");
            chunks.Add(new(i, Encoding.ASCII.GetString(header[..8]), offset, length, header.ToArray()));
            offset += length;
        }
        if (offset != total)
            throw new InvalidDataException("G1M chunks do not cover the declared file length.");
        return new("_M1G7300", total, headerLength, unknown, chunks, prefix[..24].ToArray());
    }

    private static int ReadInt(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, 4));
}
