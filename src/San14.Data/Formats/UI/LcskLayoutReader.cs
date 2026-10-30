using System.Buffers.Binary;
using System.Text;

namespace San14.Data.Formats;

public sealed record LcskUvQuad(SpriteUv TopLeft, SpriteUv TopRight,
    SpriteUv BottomLeft, SpriteUv BottomRight);

public sealed record LcskPoolEntry(int Index, int DataOffset, byte[] Data)
{
    public string? Text { get; init; }
    public LcskUvQuad? Uv { get; init; }
}

public sealed record LcskPool(int Type, int Offset, int ByteLength,
    IReadOnlyList<LcskPoolEntry> Entries, byte[] RawBytes);

public sealed record LcskSpriteReference(ushort Value0, ushort NameIndex,
    uint Value4, string Name);

public sealed record LcskSpritePartReference(int EntryIndex, ushort Value0,
    ushort UvIndex, ushort SpriteIndex, string SpriteName, LcskUvQuad Uv,
    byte[] RemainingBytes);

public sealed record LcskUnparsedSpritePart(int EntryIndex, int ByteLength,
    string Reason);

public sealed record LcskBlock(int Tag, int Offset, int ByteLength, ushort Kind,
    ushort RecordCount, int RecordOffset, int PoolOffset, byte[] RecordBytes,
    IReadOnlyList<LcskPool> Pools, byte[] RawBytes);

public sealed record LcskViewport(uint Width, uint Height);

public sealed record LcskDocument(string Signature, byte[] HeaderBytes,
    IReadOnlyList<LcskBlock> Blocks)
{
    public LcskViewport? Viewport { get; init; }
    public IReadOnlyList<LcskSpriteReference> SpriteReferences { get; init; } = [];
    public IReadOnlyList<LcskSpritePartReference> SpriteParts { get; init; } = [];
    public IReadOnlyList<LcskUnparsedSpritePart> UnparsedSpriteParts { get; init; } = [];
}

/// <summary>
/// Reads verified LCSK 4500/5500/6500/7500/8500/9500 containers, named sprite references, and UV pools.
/// Node records, animation records, and other pools retain their complete source bytes.
/// The older signatures are verified for the audited static UI nodes only;
/// retaining animation bytes does not interpret their version-specific format.
/// This reader does not infer node transforms or draw source layouts.
/// </summary>
public static class LcskLayoutReader
{
    public const uint RdbTypeId = 0x5599AA51;
    private const int HeaderSize = 76;
    private const int BlockHeaderSize = 28;
    private static readonly Encoding TextEncoding = new UTF8Encoding(false, true);

    internal static bool IsSupportedSignature(string signature) =>
        signature is "LCSK4500" or "LCSK5500" or "LCSK6500" or "LCSK7500" or "LCSK8500" or "LCSK9500";

    public static LcskDocument Read(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < HeaderSize)
            throw new InvalidDataException("LCSK layout header is truncated.");
        string signature = Encoding.ASCII.GetString(bytes[..8]);
        if (!IsSupportedSignature(signature))
            throw new InvalidDataException($"Unsupported LCSK layout signature {signature}.");
        if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(8, 4)) != bytes.Length)
            throw new InvalidDataException("LCSK layout declared length differs.");

        var blocks = new List<LcskBlock>();
        var tags = new HashSet<int>();
        int position = HeaderSize;
        while (position < bytes.Length)
        {
            if (bytes.Length - position < BlockHeaderSize)
                throw new InvalidDataException($"LCSK block header is truncated at {position}.");
            ReadOnlySpan<byte> header = bytes.Slice(position, BlockHeaderSize);
            int tag = ReadInt(header, 0);
            int blockBytes = ReadInt(header, 4);
            int recordBytes = ReadInt(header, 8);
            int poolBytes = ReadInt(header, 12);
            ushort kind = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(16, 2));
            ushort recordCount = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(18, 2));
            int recordOffset = ReadInt(header, 20);
            int poolOffset = ReadInt(header, 24);
            if (tag is not (0 or 1 or 2 or 3 or 4 or 5 or 6 or 7 or 9) || !tags.Add(tag))
                throw new InvalidDataException($"Unsupported or repeated LCSK block tag {tag} at {position}.");
            if (recordBytes < 0 || poolBytes < 0 || blockBytes < BlockHeaderSize ||
                blockBytes > bytes.Length - position ||
                blockBytes != BlockHeaderSize + (long)recordBytes + poolBytes ||
                recordOffset != BlockHeaderSize ||
                (poolBytes != 0 && poolOffset != BlockHeaderSize + (long)recordBytes) ||
                (poolBytes == 0 && poolOffset != -1 &&
                    poolOffset != BlockHeaderSize + (long)recordBytes && !(tag == 6 && poolOffset == 0)))
                throw new InvalidDataException($"LCSK block {tag} boundary differs at {position}.");

            var pools = new List<LcskPool>();
            int poolPosition = position + BlockHeaderSize + recordBytes;
            int blockEnd = position + blockBytes;
            var poolTypes = new HashSet<int>();
            while (poolPosition < blockEnd)
            {
                LcskPool pool = ReadPool(bytes, poolPosition, blockEnd);
                if (!poolTypes.Add(pool.Type))
                    throw new InvalidDataException($"Repeated LCSK pool type {pool.Type} in block {tag}.");
                pools.Add(pool);
                poolPosition += pool.ByteLength;
            }
            blocks.Add(new LcskBlock(tag, position, blockBytes, kind, recordCount,
                recordOffset, poolOffset,
                bytes.Slice(position + recordOffset, recordBytes).ToArray(), pools,
                bytes.Slice(position, blockBytes).ToArray()));
            position = blockEnd;
        }

        LcskBlock references = blocks.SingleOrDefault(block => block.Tag == 0)
            ?? throw new InvalidDataException("LCSK sprite reference block is missing.");
        LcskBlock nodes = blocks.SingleOrDefault(block => block.Tag == 1)
            ?? throw new InvalidDataException("LCSK node block is missing.");
        IReadOnlyList<LcskSpriteReference> sprites = ReadSpriteReferences(references);
        LcskPool nodeNames = nodes.Pools.SingleOrDefault(pool => pool.Type == 1)
            ?? throw new InvalidDataException("LCSK node name pool is missing.");
        if (nodeNames.Entries.Count != nodes.RecordCount)
            throw new InvalidDataException("LCSK node names and declared record count differ.");
        IReadOnlyList<LcskSpritePartReference> parts = ReadSpriteParts(nodes, sprites,
            out IReadOnlyList<LcskUnparsedSpritePart> unparsedParts);
        LcskViewport? viewport = null;
        LcskBlock? canvas = blocks.SingleOrDefault(block => block.Tag == 5);
        if (canvas is { Kind: 0, RecordCount: 1 } && canvas.RecordBytes.Length == 112)
        {
            uint width = BinaryPrimitives.ReadUInt32LittleEndian(canvas.RecordBytes.AsSpan(56, 4));
            uint height = BinaryPrimitives.ReadUInt32LittleEndian(canvas.RecordBytes.AsSpan(60, 4));
            if (width == 0 || height == 0)
                throw new InvalidDataException("LCSK source viewport has zero dimensions.");
            viewport = new LcskViewport(width, height);
        }
        return new LcskDocument(signature, bytes[..HeaderSize].ToArray(), blocks)
        {
            Viewport = viewport,
            SpriteReferences = sprites,
            SpriteParts = parts,
            UnparsedSpriteParts = unparsedParts
        };
    }

    private static LcskPool ReadPool(ReadOnlySpan<byte> bytes, int position, int end)
    {
        if (end - position < 12)
            throw new InvalidDataException($"LCSK pool header is truncated at {position}.");
        int type = ReadInt(bytes, position);
        int count = ReadInt(bytes, position + 4);
        int length = ReadInt(bytes, position + 8);
        if (type < 0 || count < 0 || length > end - position || length < 12L + count * 4L)
            throw new InvalidDataException($"LCSK pool {type} boundary differs at {position}.");
        int dataStart = position + 12 + count * 4;
        var entries = new List<LcskPoolEntry>(count);
        for (int index = 0; index < count; index++)
        {
            int offset = ReadInt(bytes, position + 12 + index * 4);
            if (offset < 0 || offset > position + length - dataStart - 4)
                throw new InvalidDataException($"LCSK pool {type} entry {index} offset differs.");
            int entry = dataStart + offset;
            int dataBytes = ReadInt(bytes, entry);
            if (dataBytes < 0 || dataBytes > position + length - entry - 4)
                throw new InvalidDataException($"LCSK pool {type} entry {index} length differs.");
            byte[] data = bytes.Slice(entry + 4, dataBytes).ToArray();
            string? text = null;
            // Pool1 is an opaque narrow-byte control identifier in the original
            // loader. Its bytes are copied and NUL-terminated, never decoded as
            // localized text. Preserve each byte one-to-one for diagnostics and
            // ASCII-name comparisons; Data remains the actual byte identity.
            // Pool0 resource names retain their existing stricter text contract.
            if (type == 1) text = Encoding.Latin1.GetString(data);
            if (type == 0)
            {
                try { text = TextEncoding.GetString(data); }
                catch (DecoderFallbackException error)
                {
                    throw new InvalidDataException(
                        $"LCSK name pool {type} entry {index} has unsupported text encoding.", error);
                }
            }
            LcskUvQuad? uv = null;
            if (type == 5)
            {
                if (dataBytes != 32)
                    throw new InvalidDataException($"Unsupported LCSK UV entry length {dataBytes}.");
                uv = new LcskUvQuad(ReadUv(data, 0), ReadUv(data, 8),
                    ReadUv(data, 16), ReadUv(data, 24));
            }
            entries.Add(new LcskPoolEntry(index, entry + 4, data) { Text = text, Uv = uv });
        }
        return new LcskPool(type, position, length, entries,
            bytes.Slice(position, length).ToArray());
    }

    private static IReadOnlyList<LcskSpriteReference> ReadSpriteReferences(LcskBlock block)
    {
        LcskPool names = block.Pools.SingleOrDefault(pool => pool.Type == 0)
            ?? throw new InvalidDataException("LCSK sprite name pool is missing.");
        if (block.Kind != 1 || block.RecordBytes.Length != block.RecordCount * 8)
            throw new InvalidDataException("Unsupported LCSK sprite reference record structure.");
        var result = new List<LcskSpriteReference>(block.RecordCount);
        for (int index = 0; index < block.RecordCount; index++)
        {
            ReadOnlySpan<byte> row = block.RecordBytes.AsSpan(index * 8, 8);
            ushort nameIndex = BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(2, 2));
            if (nameIndex >= names.Entries.Count)
                throw new InvalidDataException($"LCSK sprite reference {index} name index differs.");
            result.Add(new LcskSpriteReference(BinaryPrimitives.ReadUInt16LittleEndian(row),
                nameIndex, BinaryPrimitives.ReadUInt32LittleEndian(row.Slice(4, 4)),
                names.Entries[nameIndex].Text!));
        }
        return result;
    }

    private static IReadOnlyList<LcskSpritePartReference> ReadSpriteParts(LcskBlock block,
        IReadOnlyList<LcskSpriteReference> sprites,
        out IReadOnlyList<LcskUnparsedSpritePart> unparsedParts)
    {
        var unknown = new List<LcskUnparsedSpritePart>();
        unparsedParts = unknown;
        LcskPool? parts = block.Pools.SingleOrDefault(pool => pool.Type == 4);
        if (parts is null || parts.Entries.Count == 0) return [];
        LcskPool uv = block.Pools.SingleOrDefault(pool => pool.Type == 5)
            ?? throw new InvalidDataException("LCSK sprite parts have no UV pool.");
        var result = new List<LcskSpritePartReference>(parts.Entries.Count);
        foreach (LcskPoolEntry entry in parts.Entries)
        {
            if (entry.Data.Length != 28)
            {
                unknown.Add(new LcskUnparsedSpritePart(entry.Index, entry.Data.Length,
                    "Source sprite-part record structure has not been verified; complete bytes remain in pool 4."));
                continue;
            }
            ReadOnlySpan<byte> row = entry.Data;
            ushort uvIndex = BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(2, 2));
            ushort spriteIndex = BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(4, 2));
            if (uvIndex >= uv.Entries.Count || uv.Entries[uvIndex].Uv is null || spriteIndex >= sprites.Count)
                throw new InvalidDataException($"LCSK sprite part {entry.Index} has an invalid UV or sprite reference.");
            result.Add(new LcskSpritePartReference(entry.Index,
                BinaryPrimitives.ReadUInt16LittleEndian(row), uvIndex, spriteIndex,
                sprites[spriteIndex].Name, uv.Entries[uvIndex].Uv!, row[6..].ToArray()));
        }
        return result;
    }

    private static SpriteUv ReadUv(ReadOnlySpan<byte> bytes, int offset)
    {
        float u = BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(offset, 4));
        float v = BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(offset + 4, 4));
        if (!float.IsFinite(u) || !float.IsFinite(v))
            throw new InvalidDataException("LCSK UV coordinates are not finite.");
        return new SpriteUv(u, v);
    }

    private static int ReadInt(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, 4));
}
