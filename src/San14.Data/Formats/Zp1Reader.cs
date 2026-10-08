using System.Buffers.Binary;
using System.IO.Compression;

namespace San14.Data.Formats;

public sealed record Zp1Chunk(int Index, int SourceOffset, uint StoredSize,
    uint CompressedSize, int DecodedOffset, int DecodedLength, byte[] PaddingBytes);

public sealed record Zp1Document(uint DecodedLength, uint ChunkSize, byte[] HeaderBytes,
    IReadOnlyList<Zp1Chunk> Chunks, byte[] DecodedPayload);

/// <summary>Reads the installed message files' zp1 container; all offsets and padding are checked.</summary>
public static class Zp1Reader
{
    private const int HeaderSize = 2048;
    private const int Alignment = 128;
    private const int MaximumDecodedSize = 512 * 1024 * 1024;

    public static Zp1Document Read(ReadOnlySpan<byte> source)
    {
        if (source.Length < HeaderSize || !source[..4].SequenceEqual("zp1\0"u8))
            throw new InvalidDataException("Zp1 header is missing or truncated.");
        uint decodedLength = U32(source, 4), chunkSize = U32(source, 8), count = U32(source, 12);
        if (decodedLength == 0 || decodedLength > MaximumDecodedSize || chunkSize == 0 ||
            chunkSize > MaximumDecodedSize || count == 0 || count > (HeaderSize - 16) / 4 ||
            count != ((ulong)decodedLength + chunkSize - 1) / chunkSize)
            throw new InvalidDataException("Zp1 decoded length, chunk size or chunk count is invalid.");
        int tableEnd = 16 + (int)count * 4;
        if (source.Slice(tableEnd, HeaderSize - tableEnd).IndexOfAnyExcept((byte)0) >= 0)
            throw new InvalidDataException("Zp1 header has unsupported nonzero trailing fields.");

        var output = new byte[(int)decodedLength];
        var chunks = new List<Zp1Chunk>((int)count);
        int position = HeaderSize, outputPosition = 0;
        for (int index = 0; index < count; index++)
        {
            uint storedSize = U32(source, 16 + index * 4);
            if (storedSize < 10 || storedSize > int.MaxValue || position > source.Length - 4 ||
                storedSize > source.Length - position)
                throw new InvalidDataException($"Zp1 chunk {index} extends outside its source.");
            uint compressedSize = U32(source, position);
            if (compressedSize != storedSize - 4)
                throw new InvalidDataException($"Zp1 chunk {index} length disagrees with the header table.");
            ReadOnlySpan<byte> compressed = source.Slice(position + 4, (int)compressedSize);
            if ((compressed[0] & 15) != 8 || (compressed[0] >> 4) > 7 ||
                ((compressed[0] << 8) + compressed[1]) % 31 != 0 || (compressed[1] & 32) != 0)
                throw new InvalidDataException($"Zp1 chunk {index} has an unsupported zlib header.");
            int expected = (int)Math.Min(chunkSize, decodedLength - (uint)outputPosition);
            try
            {
                // Limit every underlying read to one byte. ZLibStream otherwise reads ahead
                // beyond the first zlib stream, so MemoryStream.Position would hide a suffix.
                using var input = new SingleByteReadStream(compressed.ToArray());
                using var inflater = new ZLibStream(input, CompressionMode.Decompress);
                inflater.ReadExactly(output.AsSpan(outputPosition, expected));
                if (inflater.ReadByte() != -1)
                    throw new InvalidDataException($"Zp1 chunk {index} exceeds its declared decoded length.");
                if (input.Position != input.Length)
                    throw new InvalidDataException($"Zp1 chunk {index} contains bytes after its zlib stream.");
            }
            catch (Exception error) when (error is InvalidDataException or EndOfStreamException)
            {
                throw new InvalidDataException($"Zp1 chunk {index} cannot be decoded to its declared length.", error);
            }
            if (BinaryPrimitives.ReadUInt32BigEndian(compressed[^4..]) !=
                Adler32(output.AsSpan(outputPosition, expected)))
                throw new InvalidDataException($"Zp1 chunk {index} checksum differs from its decoded bytes.");
            int end = position + (int)storedSize;
            long aligned = ((long)end + Alignment - 1) / Alignment * Alignment;
            if (aligned > source.Length || source.Slice(end, (int)aligned - end).IndexOfAnyExcept((byte)0) >= 0)
                throw new InvalidDataException($"Zp1 chunk {index} padding is missing or nonzero.");
            chunks.Add(new Zp1Chunk(index, position, storedSize, compressedSize,
                outputPosition, expected, source.Slice(end, (int)aligned - end).ToArray()));
            position = (int)aligned;
            outputPosition += expected;
        }
        if (position != source.Length || outputPosition != output.Length)
            throw new InvalidDataException("Zp1 source or decoded payload has unindexed trailing bytes.");
        return new Zp1Document(decodedLength, chunkSize, source[..HeaderSize].ToArray(), chunks, output);
    }

    private static uint U32(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4));

    private sealed class SingleByteReadStream(byte[] bytes) : MemoryStream(bytes, writable: false)
    {
        public override int Read(byte[] buffer, int offset, int count) =>
            base.Read(buffer, offset, Math.Min(count, 1));

        public override int Read(Span<byte> buffer) =>
            base.Read(buffer[..Math.Min(buffer.Length, 1)]);
    }

    private static uint Adler32(ReadOnlySpan<byte> bytes)
    {
        uint a = 1, b = 0;
        foreach (byte value in bytes)
        {
            a = (a + value) % 65521;
            b = (b + a) % 65521;
        }
        return (b << 16) | a;
    }
}
