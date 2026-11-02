using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

public sealed record TerrainLodDrawRow(TerrainLodPatchSelection Patch, int Group, float Distance);
public sealed record TerrainLodDrawPlan(IReadOnlyList<TerrainLodDrawRow> Rows, int SelectedCount, int CulledCount);

/// <summary>Ordinary single-field producer. Equal sort keys retain an explicit replica index order.</summary>
public sealed class TerrainLodDrawPlanner
{
    private readonly SpatialTreeDocument tree;

    public TerrainLodDrawPlanner(SpatialTreeDocument tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        TerrainLodSelectionMath.ValidateTree(tree);
        this.tree = tree;
    }

    public static TerrainLodDrawPlan Build(SpatialTreeDocument tree, Vector3 eye, float distanceScale,
        TerrainFrustumPlanes frustum, bool expandHeightBounds = true)
        => new TerrainLodDrawPlanner(tree).Build(eye, distanceScale, frustum, expandHeightBounds);

    public TerrainLodDrawPlan Build(Vector3 eye, float distanceScale,
        TerrainFrustumPlanes frustum, bool expandHeightBounds = true)
    {
        IReadOnlyList<SpatialTreeNode> selected = TerrainLodSelectionMath.SelectPrepared(tree, eye, distanceScale);
        TerrainLodEdgeCandidate[] candidates = selected.Select(node =>
            new TerrainLodEdgeCandidate(node, 4, TerrainLodSelectionMath.PlanarDistance(node, eye)))
            .OrderBy(row => row.Node!.Level).ThenBy(row => row.RuntimeDistance).ThenBy(row => row.Node!.Index).ToArray();
        var coordinates = new Dictionary<(int Level, int X, int Y), int>(candidates.Length);
        for (int i = 0; i < candidates.Length; i++)
        {
            var node = candidates[i].Node!;
            coordinates.Add((node.Level, node.GridX, node.GridY), i);
        }
        var rows = new List<TerrainLodDrawRow>();
        int group0Count = 0, culled = 0;
        for (int i = 0; i < candidates.Length; i++)
        {
            SpatialTreeNode node = candidates[i].Node!;
            float minX = node.GridX * node.Size, minZ = node.GridY * node.Size;
            float minY = node.SampleMinimum - (expandHeightBounds ? 1000f : 0f);
            float maxY = node.SampleMaximum + (expandHeightBounds ? 1000f : 0f);
            if (!TerrainFrustumMath.IntersectsAabb(frustum, new(minX,minY,minZ), new(minX+node.Size,maxY,minZ+node.Size)))
            {
                candidates[i] = candidates[i] with { RuntimeDistance = -1 };
                culled++;
                continue;
            }
            int group = node.Level == 0 ? (group0Count < 4 ? 0 : 1) : 2;
            if (group == 0) group0Count++;
            uint flags = TerrainLodEdgeMath.ComputePrepared(node, candidates, coordinates);
            rows.Add(new(new(node, flags), group, candidates[i].RuntimeDistance));
        }
        return new(rows.AsReadOnly(), selected.Count, culled);
    }
}
