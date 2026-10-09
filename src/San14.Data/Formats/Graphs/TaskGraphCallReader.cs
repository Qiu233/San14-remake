using System.Buffers.Binary;

namespace San14.Data.Formats;

public sealed record TaskGraphArgument(int Index, uint RawType, uint RawElementCount, uint NameHash,
    uint DescriptorWord, int LiteralSourceOffset, byte[] RawValue)
{
    public int EffectiveTypeIndex => (int)((DescriptorWord >> 24) & 15);
    public uint EffectiveElementCount => DescriptorWord & 0xFFFFFF;
}

public sealed record TaskGraphCallMetadata(int SourceOffset, uint LookupKey, uint FactoryClassId,
    uint RawWord14, IReadOnlyList<uint> ConnectedNodeIndices, IReadOnlyList<TaskGraphArgument> Arguments,
    int ParsedPrefixBytes, byte[] RawBytes)
{
    public int UninterpretedTrailingBytes => RawBytes.Length - ParsedPrefixBytes;
}

/// <summary>Original compiled call triples and literal progression, independent of DOK property widths.</summary>
public static class TaskGraphCallReader
{
    private static readonly int[] Widths = [1,1,2,2,4,4,8,8,4,8,16,64,8,12,0];

    /// <summary>Read an identified metadata fragment M. SourceOffset annotates its parent-source location.</summary>
    public static TaskGraphCallMetadata Read(ReadOnlySpan<byte> metadata, int sourceOffset = 0)
    {
        if (sourceOffset < 0 || (long)sourceOffset + metadata.Length > int.MaxValue || metadata.Length < 0x20)
            throw new InvalidDataException("Original TaskGraph call metadata has invalid source bounds.");
        uint argc = U32(metadata, 0x18), edgeCount = U32(metadata, 0x1C);
        long triplesAt = 0x20L + (long)edgeCount * 4;
        long literalAt = triplesAt + (long)argc * 12;
        if (literalAt > metadata.Length)
            throw new InvalidDataException("Original TaskGraph connections or parameter triples exceed the source fragment.");
        // Validate every literal range before count-dependent allocations.
        // Native descriptors OR raw count into the shifted type; high count
        // bits can change the descriptor, so widths use its effective fields.
        long end = literalAt;
        for (int i = 0; i < (int)argc; i++)
        {
            int at = (int)triplesAt + i * 12;
            uint packed = unchecked((U32(metadata, at) << 24) | U32(metadata, at + 4));
            end += LiteralBytes(packed);
            if (end > metadata.Length)
                throw new InvalidDataException("Original TaskGraph parameter literals exceed the source fragment.");
        }
        var edges = new uint[(int)edgeCount];
        for (int i = 0; i < edges.Length; i++) edges[i] = U32(metadata, 0x20 + i * 4);
        var args = new TaskGraphArgument[(int)argc];
        int cursor = (int)literalAt;
        for (int i = 0; i < args.Length; i++)
        {
            int at = (int)triplesAt + i * 12;
            uint type = U32(metadata, at), count = U32(metadata, at + 4), name = U32(metadata, at + 8);
            uint packed = unchecked((type << 24) | count);
            int length = (int)LiteralBytes(packed);
            args[i] = new(i, type, count, name, packed, sourceOffset + cursor, metadata.Slice(cursor, length).ToArray());
            cursor += length; // Native literal regions have no per-argument alignment.
        }
        return new(sourceOffset, U32(metadata, 0x0C), U32(metadata, 0x10), U32(metadata, 0x14),
            Array.AsReadOnly(edges), Array.AsReadOnly(args), (int)end, metadata.ToArray());
    }

    private static long LiteralBytes(uint packed)
    {
        int type = (int)((packed >> 24) & 15);
        if (type >= Widths.Length)
            throw new InvalidDataException("Unsupported original TaskGraph parameter type nibble F.");
        return (long)Widths[type] * (packed & 0xFFFFFF);
    }
    private static uint U32(ReadOnlySpan<byte> source, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(offset, 4));
}
