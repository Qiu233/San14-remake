using System.Buffers.Binary;

namespace San14.Data.Formats;

public sealed record LcskSpriteNodePart(int GroupIndex, int ComponentIndex,
    int EntryIndex, ushort Value0, ushort UvIndex, ushort SpriteIndex,
    string SpriteName, LcskUvQuad Uv, byte[] RawBytes);

public sealed record LcskSpriteNodeGroup(int Index, ushort EntryIndex,
    ushort ComponentCount, IReadOnlyList<LcskSpriteNodePart> Parts,
    byte[] RawScaleBytes, byte[] RawRenderBytes);

public sealed record LcskSpriteNode(int NodeIndex, ushort NodeType,
    IReadOnlyList<LcskSpriteNodeGroup> Groups, IReadOnlyList<float> SourceMargins,
    byte[] RawTintBytes, byte[] RawBytes);

/// <summary>
/// Reads verified type-1 sprite and type-3 nine-part references. Pool-4 components
/// are split only by the node's explicit count, with each component retaining its
/// complete 28 source bytes. UV quads are local to the named sprite, not the atlas.
/// Nine-part groups occur in column-major order. The paired entry 65535/count 0
/// source sentinel retains an empty group at its original index. Tint, rendering and scale bytes
/// retain their source representation without assigning unverified semantics.
/// </summary>
public static class LcskSpriteNodeReader
{
    public static LcskSpriteNode Read(LcskDocument layout, LcskNodeRecord node)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(node);
        int expectedLength = node.Type switch
        {
            1 => 144,
            3 => 736,
            _ => throw new InvalidDataException($"Unsupported LCSK sprite node type {node.Type} at node {node.Index}.")
        };
        if (!LcskLayoutReader.IsSupportedSignature(layout.Signature) ||
            node.RawBytes.Length != expectedLength)
            throw new InvalidDataException($"LCSK sprite node {node.Index} signature or record length differs.");
        LcskBlock[] blocks = layout.Blocks.Where(block => block.Tag == 1).ToArray();
        if (blocks.Length != 1 || blocks[0].Kind != 8)
            throw new InvalidDataException("LCSK sprite reader requires one kind-8 node block.");
        LcskBlock block = blocks[0];
        if (node.Index < 0 || node.Index >= block.RecordCount || node.Offset < 0 ||
            node.Offset > block.RecordBytes.Length - expectedLength ||
            !block.RecordBytes.AsSpan(node.Offset, expectedLength).SequenceEqual(node.RawBytes) ||
            U16(node.RawBytes, 0) != node.Index || U16(node.RawBytes, 56) != node.Type)
            throw new InvalidDataException($"LCSK sprite node {node.Index} does not match the supplied layout.");
        LcskPool[] partPools = block.Pools.Where(pool => pool.Type == 4).ToArray();
        LcskPool[] uvPools = block.Pools.Where(pool => pool.Type == 5).ToArray();
        if (partPools.Length != 1 || uvPools.Length != 1)
            throw new InvalidDataException($"LCSK sprite node {node.Index} part or UV pool is missing or repeated.");
        var groups = new List<LcskSpriteNodeGroup>();
        int groupCount = node.Type == 1 ? 1 : 9;
        for (int groupIndex = 0; groupIndex < groupCount; groupIndex++)
        {
            int referenceOffset = node.Type == 1 ? 112 : 328 + groupIndex * 12;
            ushort entryIndex = U16(node.RawBytes, referenceOffset);
            ushort count = U16(node.RawBytes, referenceOffset + 2);
            if (entryIndex == ushort.MaxValue && count == 0)
            {
                groups.Add(CreateGroup(node, groupIndex, referenceOffset, entryIndex, count, []));
                continue;
            }
            if (count == 0 || entryIndex == ushort.MaxValue || entryIndex >= partPools[0].Entries.Count)
                throw new InvalidDataException($"LCSK sprite node {node.Index} group {groupIndex} has an unpaired empty or invalid part reference.");
            LcskPoolEntry entry = partPools[0].Entries[entryIndex];
            if (entry.Data.Length != count * 28L)
                throw new InvalidDataException($"LCSK sprite node {node.Index} group {groupIndex} component count {count} differs from pool-4 length {entry.Data.Length}.");
            var parts = new List<LcskSpriteNodePart>(count);
            for (int componentIndex = 0; componentIndex < count; componentIndex++)
            {
                byte[] source = entry.Data.AsSpan(componentIndex * 28, 28).ToArray();
                ushort uvIndex = U16(source, 2), spriteIndex = U16(source, 4);
                if (uvIndex >= uvPools[0].Entries.Count ||
                    uvPools[0].Entries[uvIndex].Uv is not LcskUvQuad uv ||
                    spriteIndex >= layout.SpriteReferences.Count)
                    throw new InvalidDataException($"LCSK sprite node {node.Index} group {groupIndex} component {componentIndex} has an invalid UV or sprite reference.");
                parts.Add(new LcskSpriteNodePart(groupIndex, componentIndex, entryIndex,
                    U16(source, 0), uvIndex, spriteIndex,
                    layout.SpriteReferences[spriteIndex].Name, uv, source));
            }
            groups.Add(CreateGroup(node, groupIndex, referenceOffset, entryIndex, count, parts));
        }
        float[] margins = [];
        if (node.Type == 3)
        {
            margins = new float[4];
            for (int i = 0; i < margins.Length; i++)
            {
                float value = BinaryPrimitives.ReadSingleLittleEndian(node.RawBytes.AsSpan(680 + i * 4, 4));
                if (!float.IsFinite(value))
                    throw new InvalidDataException($"LCSK sprite node {node.Index} margin {i} is not finite.");
                margins[i] = value;
            }
        }
        return new LcskSpriteNode(node.Index, node.Type, groups, margins,
            node.RawBytes.AsSpan(64, 16).ToArray(), node.RawBytes.ToArray());
    }

    private static LcskSpriteNodeGroup CreateGroup(LcskNodeRecord node, int index,
        int referenceOffset, ushort entryIndex, ushort count,
        IReadOnlyList<LcskSpriteNodePart> parts)
    {
        int renderOffset = node.Type == 1 ? 80 : 104 + index * 24;
        return new LcskSpriteNodeGroup(index, entryIndex, count, parts,
            node.RawBytes.AsSpan(referenceOffset - 8, 8).ToArray(),
            node.RawBytes.AsSpan(renderOffset, 24).ToArray());
    }

    private static ushort U16(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset, 2));
}
