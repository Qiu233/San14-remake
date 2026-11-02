using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>
/// Ordinary Landscape distance selection (BC1880/BC3020). The caller supplies
/// the original selection point; this does not infer it from a replica camera.
/// Source tree levels determine terminal depth. Draw groups and edge flags are separate.
/// </summary>
public static class TerrainLodSelectionMath
{
    public const int DefaultPrimaryCapacity = 1024;
    public static IReadOnlyList<SpatialTreeNode> SelectOrdinary(SpatialTreeDocument tree,
        Vector3 selectionPoint, float distanceScale, bool distanceSelectionEnabled = true,
        int outputCapacity = DefaultPrimaryCapacity)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ValidatePointAndScale(selectionPoint, distanceScale);
        ValidateTree(tree);
        return SelectCore(tree, selectionPoint, distanceScale, distanceSelectionEnabled, outputCapacity);
    }

    // The retained planner validates its immutable resource topology once.
    // Current camera inputs still pass their constant-time admission checks.
    internal static IReadOnlyList<SpatialTreeNode> SelectPrepared(SpatialTreeDocument tree,
        Vector3 selectionPoint, float distanceScale, bool distanceSelectionEnabled = true,
        int outputCapacity = DefaultPrimaryCapacity)
    {
        ValidatePointAndScale(selectionPoint, distanceScale);
        return SelectCore(tree, selectionPoint, distanceScale, distanceSelectionEnabled, outputCapacity);
    }

    private static IReadOnlyList<SpatialTreeNode> SelectCore(SpatialTreeDocument tree,
        Vector3 selectionPoint, float distanceScale, bool distanceSelectionEnabled, int outputCapacity)
    {
        if (outputCapacity < 0) throw new ArgumentOutOfRangeException(nameof(outputCapacity));
        var selected = new List<SpatialTreeNode>();
        Visit(0, 0);
        return selected.AsReadOnly();

        void Visit(int index, int depth)
        {
            SpatialTreeNode node = tree.Nodes[index];
            // The original bypasses the distance predicate at terminal depth.
            if (selected.Count < outputCapacity && (depth == tree.LevelCount - 1 || (distanceSelectionEnabled &&
                PlanarDistance(node, selectionPoint) > SplitThreshold(node.Size, distanceScale))))
            {
                selected.Add(node);
                return;
            }
            if (depth != tree.LevelCount - 1)
            {
                int firstChild = checked(index * 4 + 1);
                for (int child = 0; child < 4; child++) Visit(firstChild + child, depth + 1);
            }
        }
    }

    /// <summary>Source point Y is forced to zero before the rectangle distance call.</summary>
    public static float PlanarDistance(SpatialTreeNode node, Vector3 selectionPoint)
    {
        ArgumentNullException.ThrowIfNull(node);
        ValidatePointAndScale(selectionPoint, 0);
        if (!float.IsFinite(node.Size) || node.Size <= 0 || node.GridX < 0 || node.GridY < 0)
            throw new InvalidDataException("Invalid source LOD rectangle.");
        float minX = node.GridX * node.Size, minZ = node.GridY * node.Size;
        float maxX = minX + node.Size, maxZ = minZ + node.Size;
        if (!float.IsFinite(maxX) || !float.IsFinite(maxZ))
            throw new InvalidDataException("Source LOD rectangle exceeds finite coordinates.");
        float dx = Math.Clamp(selectionPoint.X, minX, maxX) - selectionPoint.X;
        float dz = Math.Clamp(selectionPoint.Z, minZ, maxZ) - selectionPoint.Z;
        // Retain the original 3D distance reduction with dy=0 and scalar sqrt.
        float xSquared = dx * dx, ySquared = 0, zSquared = dz * dz;
        float result = MathF.Sqrt((xSquared + ySquared) + zSquared);
        if (!float.IsFinite(result)) throw new InvalidDataException("Source LOD distance exceeds finite float arithmetic.");
        return result;
    }

    public static float SplitThreshold(float nodeSize, float distanceScale)
    {
        if (!float.IsFinite(nodeSize) || nodeSize <= 0 || !float.IsFinite(distanceScale) || distanceScale < 0)
            throw new InvalidDataException("Invalid source LOD threshold input.");
        float halfSize = nodeSize * 0.5f;
        float diagonalRadius = MathF.Sqrt(2f) * halfSize;
        float threshold = distanceScale * diagonalRadius;
        if (!float.IsFinite(threshold)) throw new InvalidDataException("Source LOD threshold exceeds finite float arithmetic.");
        return threshold;
    }

    private static void ValidatePointAndScale(Vector3 point, float scale)
    {
        if (!float.IsFinite(point.X) || !float.IsFinite(point.Y) || !float.IsFinite(point.Z) ||
            !float.IsFinite(scale) || scale < 0)
            throw new InvalidDataException("Nonfinite or unsupported source LOD selection input.");
    }

    internal static void ValidateTree(SpatialTreeDocument tree)
    {
        if (tree.LevelCount < 1 || tree.LevelCount > 15 || !float.IsFinite(tree.RootSize) || tree.RootSize <= 0)
            throw new InvalidDataException("Invalid ordinary source LOD tree.");
        long expectedCount = ((1L << (2 * tree.LevelCount)) - 1) / 3;
        if (expectedCount != tree.Nodes.Count)
            throw new InvalidDataException("Ordinary source LOD traversal requires a complete tree.");
        int index = 0, layerCount = 1;
        for (int depth = 0; depth < tree.LevelCount; depth++, layerCount *= 4)
            for (int local = 0; local < layerCount; local++, index++)
            {
                SpatialTreeNode node = tree.Nodes[index];
                int x = 0, y = 0;
                for (int bit = 0; bit < depth; bit++)
                {
                    x |= ((local >> (2 * bit)) & 1) << bit;
                    y |= ((local >> (2 * bit + 1)) & 1) << bit;
                }
                if (node.Index != index || node.Level != tree.LevelCount - depth - 1 ||
                    node.GridX != x || node.GridY != y || node.Size != tree.RootSize / (1 << depth))
                    throw new InvalidDataException("Source LOD node identity, extent or child order differs.");
            }
    }
}
