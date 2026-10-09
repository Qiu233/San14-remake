using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The original ordinary Landscape instance stream; XY of blend weight are uint bits.</summary>
public sealed record TerrainPatchInput(Vector3 Origin, Vector3 Extent, Vector4 UvBounds,
    uint EdgeFlags, uint GridSize, float ControlFrequency, float ControlCount)
{
    public byte[] EncodeInstance()
    {
        byte[] bytes = new byte[56];
        float[] first = [Origin.X, Origin.Y, Origin.Z, Extent.X, Extent.Y, Extent.Z,
            UvBounds.X, UvBounds.Y, UvBounds.Z, UvBounds.W];
        for (int i = 0; i < first.Length; i++)
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i * 4, 4), first[i]);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(40, 4), EdgeFlags);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(44, 4), GridSize);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(48, 4), ControlFrequency);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(52, 4), ControlCount);
        return bytes;
    }
}

/// <summary>Source BB0E00/BA86C0/BA2BD0 and VS1. Camera node selection is a separate input.</summary>
public static class TerrainPatchInputMath
{
    private static readonly int[] GridDivisors = [0, 1, 2, 4, 8, 16, 32];

    public static TerrainPatchInput Ordinary(SpatialTreeDocument tree, SpatialTreeNode node,
        int heightWidth, int controlWidth, uint edgeFlags = 0)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(node);
        if (tree.LevelCount < 1 || tree.LevelCount >= GridDivisors.Length ||
            node.Level < 0 || node.Level >= tree.LevelCount || heightWidth < 2 || controlWidth < 2)
            throw new InvalidDataException("Unsupported original patch tree level or source texture dimensions.");
        int size = heightWidth / GridDivisors[tree.LevelCount] + 1;
        ValidateGrid(size);
        int side = 1 << (tree.LevelCount - node.Level - 1);
        if ((uint)node.GridX >= side || (uint)node.GridY >= side ||
            !float.IsFinite(node.Size) || node.Size <= 0 || node.Size != tree.RootSize / side ||
            (edgeFlags & 0xFFFF0000) != 0)
            throw new InvalidDataException("Invalid ordinary patch node extent, grid or edge bitpack.");
        // Original frequency divides uint values before conversion to float.
        return new(new(node.GridX * node.Size, 0, node.GridY * node.Size), new(node.Size, 1, node.Size),
            new((float)node.GridX / side, (float)node.GridY / side,
                (float)(node.GridX + 1) / side, (float)(node.GridY + 1) / side),
            edgeFlags, (uint)size, (controlWidth - 1) / side, controlWidth);
    }

    public static Vector2 LocalCoordinates(TerrainPatchInput patch, int vertexId)
    {
        int n = checked((int)patch.GridSize);
        ValidateGrid(n);
        if ((uint)vertexId >= (uint)(n * n)) throw new ArgumentOutOfRangeException(nameof(vertexId));
        int row = vertexId / n, col = vertexId % n;
        float x = (float)col / (n - 1), z = (float)row / (n - 1), inv = 1f / (n - 1);
        uint b = patch.EdgeFlags;
        int n0 = (int)(b & 15), n1 = (int)(b >> 4 & 15), n2 = (int)(b >> 8 & 15), n3 = (int)(b >> 12 & 15);
        if (n0 != 0 && vertexId < n) x -= vertexId % (2 * n0) * inv;
        // Retain the original strict comparison, including its excluded corner.
        if (n1 != 0 && vertexId > n * (n - 1)) x -= vertexId % (2 * n1) * inv;
        if (n2 != 0 && col == 0) z -= row % (2 * n2) * inv;
        if (n3 != 0 && col == n - 1) z -= row % (2 * n3) * inv;
        return new(x, z);
    }

    public static Vector2 HeightUv(TerrainPatchInput patch, Vector2 local) =>
        new(patch.UvBounds.X + (patch.UvBounds.Z - patch.UvBounds.X) * local.X,
            patch.UvBounds.Y + (patch.UvBounds.W - patch.UvBounds.Y) * local.Y);

    public static ushort[] TriangleIndices(int gridSize)
    {
        ValidateGrid(gridSize);
        ushort[] indices = new ushort[checked(6 * (gridSize - 1) * (gridSize - 1))];
        int next = 0;
        for (int row = 0; row < gridSize - 1; row++)
            for (int col = 0; col < gridSize - 1; col++)
            {
                int a = row * gridSize + col, b = a + 1, c = a + gridSize, d = c + 1;
                int[] six = ((row + col) & 1) == 0 ? [a, d, b, a, c, d] : [a, c, b, c, d, b];
                foreach (int index in six) indices[next++] = (ushort)index;
            }
        return indices;
    }

    /// <summary>Original BA86C0 row-major float3 VB, before shader edge correction.
    /// A float4 POSITION signature receives the API's missing-component w=1.</summary>
    public static byte[] EncodePositionStream(int gridSize)
    {
        ValidateGrid(gridSize);
        byte[] bytes = new byte[checked(gridSize * gridSize * 12)];
        for (int row = 0; row < gridSize; row++)
            for (int col = 0; col < gridSize; col++)
            {
                int at = (row * gridSize + col) * 12;
                BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(at, 4), (float)col / (gridSize - 1));
                BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(at + 8, 4), (float)row / (gridSize - 1));
            }
        return bytes;
    }

    private static void ValidateGrid(int n)
    {
        if (n < 2 || (long)n * n > ushort.MaxValue + 1L)
            throw new InvalidDataException("Original patch grid cannot be encoded by the u16 index stream.");
    }
}
