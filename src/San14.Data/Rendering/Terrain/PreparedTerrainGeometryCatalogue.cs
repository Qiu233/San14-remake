using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Immutable initial-height CPU geometry for one imported map generation.
/// It owns the actual picking geometry/BVH and packed samples, not a recipe that
/// recreates them when a view opens. No camera, World or device enters this owner.</summary>
public sealed class PreparedTerrainGeometryCatalogue
{
    private readonly FrozenDictionary<(int Node, uint Flags), PreparedTerrainMeshPatch> patches;

    private PreparedTerrainGeometryCatalogue(LoadedTerrainResources source,
        LoadedTerrainInitialHeightResources initialHeight,
        FrozenDictionary<(int Node, uint Flags), PreparedTerrainMeshPatch> patches)
        => (Source, InitialHeight, this.patches) = (source, initialHeight, patches);

    public LoadedTerrainResources Source { get; }
    public LoadedTerrainInitialHeightResources InitialHeight { get; }
    public int Count => patches.Count;

    public bool TryGet(int nodeIndex, uint edgeFlags,
        [NotNullWhen(true)] out PreparedTerrainMeshPatch? patch)
        => patches.TryGetValue((nodeIndex, edgeFlags), out patch);

    /// <summary>Prepare a finite cover of the ordinary producer before publication.
    /// Public mesh preparation still supports its complete explicit-flags contract;
    /// this catalogue covers the current ordinary ancestor-free LOD consumer.</summary>
    public static PreparedTerrainGeometryCatalogue Prepare(LoadedTerrainResources source,
        LoadedTerrainInitialHeightResources initialHeight, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(initialHeight);
        if (!ReferenceEquals(initialHeight.Terrain, source))
            throw new InvalidDataException("CPU terrain catalogue requires one initial-height source generation.");
        TerrainLodSelectionMath.ValidateTree(source.SpatialTree);
        var patches = new Dictionary<(int Node, uint Flags), PreparedTerrainMeshPatch>();
        foreach (var node in source.SpatialTree.Nodes)
            foreach (uint flags in EnumerateOrdinaryFlags(source.SpatialTree, node))
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Multiple flags for one node and ancestor/descendant nodes do
                // not form a legal mesh selection. Prepare each immutable patch
                // with the existing producer, preserving its geometry and BVH.
                var selection = TerrainMeshSelection.Explicit(source.SpatialTree,
                    [new TerrainLodPatchSelection(node, flags)]);
                var prepared = TerrainMeshPreparation.PrepareGeometry(source, initialHeight, selection);
                patches.Add((node.Index, flags), prepared.Patches[0]);
            }
        cancellationToken.ThrowIfCancellationRequested();
        var frozen = patches.ToFrozenDictionary();
        cancellationToken.ThrowIfCancellationRequested();
        return new(source, initialHeight, frozen);
    }

    private static IEnumerable<uint> EnumerateOrdinaryFlags(SpatialTreeDocument tree, SpatialTreeNode node)
    {
        int side = 1 << (tree.LevelCount - node.Level - 1);
        var horizontal = AxisOptions(node.GridX, side, node.Level, tree.LevelCount, 8, 12);
        var vertical = AxisOptions(node.GridY, side, node.Level, tree.LevelCount, 0, 4);
        foreach (uint x in horizontal)
            foreach (uint y in vertical)
                yield return x | y;
    }

    private static List<uint> AxisOptions(int coordinate, int side, int level, int levels,
        int negativeShift, int positiveShift)
    {
        List<uint> options = [0];
        // Match TerrainLodEdgeMath's two coarser-neighbor levels. Along one
        // axis the possible neighbors are on the same side. When both exist,
        // one is the other's ancestor, so SelectCore cannot select both.
        // Include each option independently; frustum/state gates may remove it.
        for (int difference = 1; difference <= 2 && level + difference < levels; difference++)
        {
            int width = 1 << difference, coarse = coordinate >> difference;
            int remainder = coordinate & (width - 1), coarseSide = side / width;
            uint value = 1u << (difference - 1);
            if (remainder == 0 && coarse > 0) options.Add(value << negativeShift);
            if (remainder == width - 1 && coarse + 1 < coarseSide) options.Add(value << positiveShift);
        }
        return options;
    }
}
