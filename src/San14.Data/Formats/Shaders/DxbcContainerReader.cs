using System.Buffers.Binary;
using System.Text;

namespace San14.Data.Formats;

/// <summary>A range in the original DXBC bytes; no shader payload is copied.</summary>
public sealed record DxbcRange(int Offset, int Length)
{
    public ReadOnlySpan<byte> Slice(ReadOnlySpan<byte> source)
    {
        if (Offset < 0 || Length < 0 || Offset > source.Length - (long)Length)
            throw new InvalidDataException($"DXBC source range {Offset}+{Length} is outside {source.Length} bytes.");
        return source.Slice(Offset, Length);
    }
}

/// <summary>The two original tokenized-program words; instructions and shader stage are not interpreted.</summary>
public sealed record DxbcTokenHeader(uint RawVersionToken, uint DeclaredWordLength);

public sealed record DxbcPart(int DirectoryIndex, int HeaderOffset, uint FourCcValue, string FourCc,
    int PayloadLength, DxbcRange EncodedRange, DxbcRange PayloadRange, DxbcTokenHeader? TokenHeader);

public sealed record DxbcContainer(int SourceByteLength, ushort MajorVersion, ushort MinorVersion,
    byte[] RawHeader, byte[] Checksum, DxbcRange DirectoryRange, IReadOnlyList<DxbcPart> Parts,
    IReadOnlyList<DxbcRange> UninterpretedRanges)
{
    /// <summary>The original proprietary digest is preserved, not checked by this reader.</summary>
    public bool ChecksumVerified => false;
}

/// <summary>Reads DXContainer 1.0 boundaries without compiling, validating instructions, or executing shaders.</summary>
public static class DxbcContainerReader
{
    public static DxbcContainer Read(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 32 || !bytes[..4].SequenceEqual("DXBC"u8))
            throw new InvalidDataException("DXBC header is truncated or its signature is invalid.");
        ushort major = BinaryPrimitives.ReadUInt16LittleEndian(bytes[20..]);
        ushort minor = BinaryPrimitives.ReadUInt16LittleEndian(bytes[22..]);
        if (major != 1 || minor != 0)
            throw new InvalidDataException($"DXBC container version {major}.{minor} is unsupported.");
        uint declaredLength = BinaryPrimitives.ReadUInt32LittleEndian(bytes[24..]);
        if (declaredLength != bytes.Length)
            throw new InvalidDataException($"DXBC declared length {declaredLength} differs from source length {bytes.Length}.");
        uint count = BinaryPrimitives.ReadUInt32LittleEndian(bytes[28..]);
        // Bound the count before converting or allocating; all ranges below then fit an int-sized source span.
        if (count > (bytes.Length - 32L) / 4)
            throw new InvalidDataException($"DXBC part directory count {count} exceeds source length {bytes.Length}.");
        int directoryLength = (int)count * 4;
        int directoryEnd = 32 + directoryLength;
        var parts = new DxbcPart[(int)count];
        for (int index = 0; index < parts.Length; index++)
        {
            uint rawOffset = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(32 + index * 4)..]);
            if (rawOffset < directoryEnd || rawOffset > bytes.Length - 8L)
                throw new InvalidDataException($"DXBC part {index} header offset {rawOffset} intersects the directory or is outside the source.");
            int offset = (int)rawOffset;
            uint rawLength = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(offset + 4)..]);
            if (rawLength > bytes.Length - offset - 8L)
                throw new InvalidDataException($"DXBC part {index} payload length {rawLength} is outside the source at offset {offset}.");
            int length = (int)rawLength;
            uint tag = BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
            string fourCc = Encoding.Latin1.GetString(bytes.Slice(offset, 4));
            DxbcTokenHeader? tokens = null;
            if (bytes.Slice(offset, 4).SequenceEqual("SHEX"u8) || bytes.Slice(offset, 4).SequenceEqual("SHDR"u8))
            {
                if (length < 8)
                    throw new InvalidDataException($"DXBC part {index} {fourCc} token header is truncated.");
                uint versionToken = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(offset + 8)..]);
                uint words = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(offset + 12)..]);
                if (words < 2 || words * 4UL != rawLength)
                    throw new InvalidDataException($"DXBC part {index} {fourCc} declared word length {words} differs from payload length {rawLength}.");
                tokens = new DxbcTokenHeader(versionToken, words);
            }
            parts[index] = new DxbcPart(index, offset, tag, fourCc, length,
                new DxbcRange(offset, 8 + length), new DxbcRange(offset + 8, length), tokens);
        }
        // Physical sorting is used only for overlap and uncovered ranges. Public parts remain in directory order.
        var gaps = new List<DxbcRange>();
        int cursor = directoryEnd;
        foreach (DxbcPart part in parts.OrderBy(p => p.HeaderOffset))
        {
            if (part.HeaderOffset < cursor)
                throw new InvalidDataException($"DXBC part {part.DirectoryIndex} at offset {part.HeaderOffset} overlaps another part ending at {cursor}.");
            if (part.HeaderOffset > cursor) gaps.Add(new DxbcRange(cursor, part.HeaderOffset - cursor));
            cursor = part.HeaderOffset + part.EncodedRange.Length;
        }
        if (cursor < bytes.Length) gaps.Add(new DxbcRange(cursor, bytes.Length - cursor));
        return new DxbcContainer(bytes.Length, major, minor, bytes[..32].ToArray(), bytes.Slice(4, 16).ToArray(),
            new DxbcRange(32, directoryLength), Array.AsReadOnly(parts), gaps.AsReadOnly());
    }
}
