using San14.Data.Formats;

namespace San14.Data;

/// <summary>Explicit current producer eligibility; source node raw bytes are never used as runtime state.</summary>
public readonly record struct TerrainLodEdgeCandidate(SpatialTreeNode? Node, byte State, float RuntimeDistance);

/// <summary>An explicit source node and the original four-nibble vertex stitch input.</summary>
public sealed record TerrainLodPatchSelection(SpatialTreeNode Node, uint EdgeFlags);

/// <summary>Original BB1393 ordinary producer adjacency, before the VS edge-coordinate adjustment.</summary>
public static class TerrainLodEdgeMath
{
    public static uint Compute(SpatialTreeNode current, IReadOnlyList<TerrainLodEdgeCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(candidates);
        ValidateCoordinates(current);
        uint flags = 0;
        foreach (TerrainLodEdgeCandidate candidate in candidates)
        {
            // Preserve the original explicit null/state/distance gates. A later
            // candidate can still be eligible before its visibility test runs.
            if (candidate.Node is not SpatialTreeNode neighbor || candidate.State != 4) continue;
            if (!float.IsFinite(candidate.RuntimeDistance))
                throw new InvalidDataException("Unsupported nonfinite runtime LOD neighbor distance.");
            if (candidate.RuntimeDistance < 0) continue;
            ValidateCoordinates(neighbor);
            long difference = (long)neighbor.Level - current.Level;
            if (difference is not (1 or 2)) continue;
            int d = (int)difference, k = 1 << d;
            int x = current.GridX >> d, y = current.GridY >> d;
            int mx = current.GridX & (k - 1), my = current.GridY & (k - 1);
            uint value = 1u << (d - 1);
            if (my == 0 && neighbor.GridX == x && neighbor.GridY == y - 1) flags |= value;
            if (my == k - 1 && neighbor.GridX == x && neighbor.GridY == y + 1) flags |= value << 4;
            if (mx == 0 && neighbor.GridX == x - 1 && neighbor.GridY == y) flags |= value << 8;
            if (mx == k - 1 && neighbor.GridX == x + 1 && neighbor.GridY == y) flags |= value << 12;
        }
        return flags;
    }

    private static void ValidateCoordinates(SpatialTreeNode node)
    {
        if (node.Level < 0 || node.GridX < 0 || node.GridY < 0)
            throw new InvalidDataException("Invalid original LOD adjacency coordinates.");
    }
}
