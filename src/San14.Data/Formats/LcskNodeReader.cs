using System.Buffers.Binary;

namespace San14.Data.Formats;

public sealed record LcskNodeRecord(int Index, ushort NameIndex, string Name,
    int? ParentIndex, ushort Type, int Offset, byte[] RawBytes,
    IReadOnlyList<float> HeaderValues, ushort Value44, ushort Value46,
    ushort Value48, ushort Value52, ushort Value54, ushort Value58);

/// <summary>
/// Reads the verified node record boundaries and parent/name references in LCSK
/// 8500/9500 kind-8 blocks. Offset is relative to the block's RecordBytes.
/// Header values and complete records retain source data without assigning
/// unverified transform, drawing, or node-type semantics.
/// </summary>
public static class LcskNodeReader
{
    public static IReadOnlyList<LcskNodeRecord> Read(LcskDocument layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (layout.Signature is not ("LCSK8500" or "LCSK9500"))
            throw new InvalidDataException($"Unsupported LCSK node signature {layout.Signature}.");
        LcskBlock[] blocks = layout.Blocks.Where(block => block.Tag == 1).ToArray();
        if (blocks.Length != 1 || blocks[0].Kind != 8)
            throw new InvalidDataException("LCSK node reader requires one kind-8 node block.");
        LcskBlock block = blocks[0];
        LcskPool[] namePools = block.Pools.Where(pool => pool.Type == 1).ToArray();
        if (namePools.Length != 1 || namePools[0].Entries.Count != block.RecordCount)
            throw new InvalidDataException("LCSK node name pool and declared record count differ.");
        IReadOnlyList<LcskPoolEntry> names = namePools[0].Entries;
        ReadOnlySpan<byte> bytes = block.RecordBytes;
        var result = new List<LcskNodeRecord>(block.RecordCount);
        int position = 0;
        for (int index = 0; index < block.RecordCount; index++)
        {
            if (bytes.Length - position < 88)
                throw new InvalidDataException($"LCSK node {index} header is truncated at {position}.");
            ReadOnlySpan<byte> header = bytes.Slice(position, 88);
            ushort sourceIndex = U16(header, 0);
            ushort nameIndex = U16(header, 2);
            if (sourceIndex != index || nameIndex != index ||
                BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(4, 4)) != 1 ||
                nameIndex >= names.Count || names[nameIndex].Text is null)
                throw new InvalidDataException($"LCSK node {index} ID or name reference differs at {position}.");
            ushort type = U16(header, 56);
            int length = type switch
            {
                0 => 88,
                1 or 6 => 144,
                2 => 224,
                3 => 736,
                8 => 228,
                _ => throw new InvalidDataException($"Unsupported LCSK node type {type} at {position}.")
            };
            if (length > bytes.Length - position)
                throw new InvalidDataException($"LCSK node {index} type {type} is truncated at {position}.");
            ushort sourceParent = U16(header, 50);
            int? parent = sourceParent == ushort.MaxValue ? null : sourceParent;
            if (parent >= index)
                throw new InvalidDataException($"LCSK node {index} parent {parent} does not precede it.");
            var values = new float[9];
            for (int valueIndex = 0; valueIndex < values.Length; valueIndex++)
            {
                float value = BinaryPrimitives.ReadSingleLittleEndian(header.Slice(8 + valueIndex * 4, 4));
                if (!float.IsFinite(value))
                    throw new InvalidDataException($"LCSK node {index} header value {valueIndex} is not finite.");
                values[valueIndex] = value;
            }
            result.Add(new LcskNodeRecord(index, nameIndex, names[nameIndex].Text!, parent,
                type, position, bytes.Slice(position, length).ToArray(), values,
                U16(header, 44), U16(header, 46), U16(header, 48),
                U16(header, 52), U16(header, 54), U16(header, 58)));
            position += length;
        }
        if (position != bytes.Length)
            throw new InvalidDataException($"LCSK nodes end at {position}, before record boundary {bytes.Length}.");
        return result;
    }

    private static ushort U16(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset, 2));
}
