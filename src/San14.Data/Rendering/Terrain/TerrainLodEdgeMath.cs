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

    // A complete prepared tree has unique grid coordinates. Look up the
    // eight possible coarse neighbors, reading their current candidate state
    // so earlier culls and later untested rows retain the source semantics.
    internal static uint ComputePrepared(SpatialTreeNode current,
        IReadOnlyList<TerrainLodEdgeCandidate> candidates,
        IReadOnlyDictionary<(int Level, int X, int Y), int> coordinates)
    {
        uint flags = 0;
        for (int difference = 1; difference <= 2; difference++)
        {
            int width = 1 << difference;
            int x = current.GridX >> difference, y = current.GridY >> difference;
            int mx = current.GridX & (width - 1), my = current.GridY & (width - 1);
            int level = current.Level + difference;
            uint value = 1u << (difference - 1);
            if (my == 0 && Eligible(level, x, y - 1)) flags |= value;
            if (my == width - 1 && Eligible(level, x, y + 1)) flags |= value << 4;
            if (mx == 0 && Eligible(level, x - 1, y)) flags |= value << 8;
            if (mx == width - 1 && Eligible(level, x + 1, y)) flags |= value << 12;
        }
        return flags;

        bool Eligible(int level, int x, int y) => coordinates.TryGetValue((level, x, y), out int index) &&
            candidates[index].State == 4 && candidates[index].RuntimeDistance >= 0;
    }
}
