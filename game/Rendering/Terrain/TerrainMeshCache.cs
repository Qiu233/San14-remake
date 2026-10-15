using San14.Data;

/// <summary>Scene-owned geometry batches, keyed by source node and original stitch flags.</summary>
public sealed class TerrainMeshCache : IDisposable
{
    private readonly TerrainMeshResult _prototype;
    private readonly LoadedTerrainInitialHeightResources _height;
    private readonly Dictionary<(int Node, uint Flags), TerrainPatch> _patches = [];
    private readonly List<TerrainMeshResult> _batches = [];
    private readonly int _capacity;
    private bool _disposed;
    public int Count => _patches.Count;

    public TerrainMeshCache(TerrainMeshResult prototype, LoadedTerrainInitialHeightResources height,
        int capacity = 512)
    {
        if (prototype.Source != height.Terrain || capacity < 1 || prototype.Patches.Count == 0 ||
            prototype.Patches.Any(patch => !patch.Mesh.GetMeta("original_initial_height_adapter", false).AsBool() ||
                !patch.Mesh.HasMeta("source_patch_edge_flags")))
            throw new InvalidDataException("Terrain geometry cache source or capacity differs.");
        (_prototype, _height, _capacity) = (prototype, height, capacity);
        foreach (TerrainPatch patch in prototype.Patches)
            _patches.Add(Key(patch), patch);
    }

    public IReadOnlyList<TerrainPatch> Get(IReadOnlyList<TerrainLodPatchSelection> selection)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (selection.Count == 0) return Array.Empty<TerrainPatch>();
        TerrainMeshSelection selected = TerrainMeshSelection.Explicit(_prototype.Source.SpatialTree, selection);
        TerrainMeshSelection missing = selected.Filter(row => !_patches.ContainsKey((row.Node.Index,row.EdgeFlags)));
        if (missing.Rows.Count != 0)
        {
            // Validate the full selection once, then prepare only uncached patches.
            TerrainMeshResult batch = TerrainMesh.Upload(
                TerrainMeshPreparation.Prepare(_prototype.Source, _height, missing));
            _batches.Add(batch);
            foreach (TerrainPatch patch in batch.Patches) _patches.Add(Key(patch), patch);
        }
        return Array.AsReadOnly(selected.Rows.Select(row => _patches[(row.Node.Index,row.EdgeFlags)]).ToArray());
    }

    /// <summary>Call after old instances have detached; active batches are never evicted.</summary>
    public void Trim(IReadOnlyList<TerrainPatch> active)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var retained = active.Select(Key).ToHashSet();
        for (int i = 0; i < _batches.Count && _patches.Count > _capacity;)
        {
            TerrainMeshResult batch = _batches[i];
            if (batch.Patches.Any(patch => retained.Contains(Key(patch)))) { i++; continue; }
            foreach (TerrainPatch patch in batch.Patches) _patches.Remove(Key(patch));
            _batches.RemoveAt(i);
            batch.Dispose();
        }
    }

    private static (int Node,uint Flags) Key(TerrainPatch patch) =>
        (patch.SourceNode.Index, checked((uint)patch.Mesh.GetMeta("source_patch_edge_flags").AsInt64()));
    public void Dispose()
    {
        if (_disposed) return;
        foreach (TerrainMeshResult batch in _batches) batch.Dispose();
        _batches.Clear(); _patches.Clear(); _disposed = true;
        // The caller owns the prototype and its shared preview material.
    }
}
