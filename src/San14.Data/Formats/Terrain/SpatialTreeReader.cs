using System.Buffers.Binary;

namespace San14.Data.Formats;

public sealed record SpatialTreeNode(int Index, int Level, int GridX, int GridY,
    float SampleMinimum, float SampleMaximum, float Size, uint Identifier20,
    byte[] RawBytes);

public sealed record SpatialTreeDocument(float RootSize, int LevelCount,
    IReadOnlyList<SpatialTreeNode> Nodes, byte[] RawHeader);

/// <summary>
/// Reads the verified six-level, 1365-node terrain spatial containers.
/// Minimum and maximum describe each node's own 65-by-65 sampled height grid,
/// rather than a bound enclosing all descendants. Identifier20 is preserved:
/// it is not established as an RDB file ID or a directly loadable model ID.
/// </summary>
public static class SpatialTreeReader
{
    private const int HeaderSize = 20;
    private const int NodeSize = 32;
    private const int SupportedLevels = 6;
    private const int SupportedNodes = 1365;

    public static SpatialTreeDocument Read(ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderSize || U32(data, 0) != 0 || U32(data, 4) != 0 ||
            U32(data, 8) != data.Length)
            throw new InvalidDataException("Unsupported spatial tree header or invalid file length.");
        int count = U16(data, 16), levels = U16(data, 18);
        if (count != SupportedNodes || levels != SupportedLevels ||
            data.Length != HeaderSize + count * NodeSize)
            throw new InvalidDataException("Unsupported spatial tree shape; verified layout has 1365 nodes and six levels.");
        float rootSize = Float(data, 12);
        if (!float.IsFinite(rootSize) || rootSize <= 0)
            throw new InvalidDataException("Spatial tree root size must be finite and positive.");

        var nodes = new List<SpatialTreeNode>(count);
        int index = 0, layerCount = 1;
        for (int depth = 0; depth < levels; depth++, layerCount *= 4)
        {
            int level = levels - depth - 1;
            float expectedSize = rootSize / (1 << depth);
            for (int local = 0; local < layerCount; local++, index++)
            {
                ReadOnlySpan<byte> row = data.Slice(HeaderSize + index * NodeSize, NodeSize);
                int x = MortonCoordinate(local, 0), y = MortonCoordinate(local, 1);
                if (row[0] != 0 || row[1] != level || row[2] != x || row[3] != y ||
                    U16(row, 4) != 0 || U16(row, 6) != index ||
                    U32(row, 24) != 0 || U32(row, 28) != 0)
                    throw new InvalidDataException($"Spatial node {index} identity, grid order or reserved fields differ.");
                float minimum = Float(row, 8), maximum = Float(row, 12), size = Float(row, 16);
                if (!float.IsFinite(minimum) || !float.IsFinite(maximum) || minimum > maximum ||
                    !float.IsFinite(size) || size != expectedSize)
                    throw new InvalidDataException($"Spatial node {index} sample range or size is invalid.");
                nodes.Add(new SpatialTreeNode(index, level, x, y, minimum, maximum,
                    size, U32(row, 20), row.ToArray()));
            }
        }
        return new SpatialTreeDocument(rootSize, levels, nodes.AsReadOnly(), data[..HeaderSize].ToArray());
    }

    private static int MortonCoordinate(int value, int bit)
    {
        int result = 0;
        for (int index = 0; index < SupportedLevels - 1; index++)
            result |= ((value >> (2 * index + bit)) & 1) << index;
        return result;
    }

    private static ushort U16(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset, 2));
    private static uint U32(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset, 4));
    private static float Float(ReadOnlySpan<byte> data, int offset) =>
        BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset, 4)));
}
