using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>Owns one scene's terrain uploads, materials and adaptive geometry.
/// Scene nodes borrow patches; detach them before committing or releasing geometry.</summary>
internal sealed class MapTerrainResources : IDisposable
{
    private TerrainMeshCache? _cache;
    public LoadedTerrainSceneResources Source { get; }
    public TerrainMeshResult? Meshes { get; private set; }
    public LoadedTerrainInitialHeightResources InitialHeight => Source.Height;
    public LoadedTerrainShaderResources Shader => Source.Material.Shader;
    public G2ShaderTextureBases TextureBases { get; }
    public TerrainGbiMapMaterial? Gbi { get; }
    public ITerrainSurface? Surface => Gbi;
    public LoadedRdbAsset ProjectionSource { get; }
    public IReadOnlyList<TerrainPatch> ActivePatches { get; private set; }
    public TerrainPicker? Picker { get; private set; }
    public float LodDistanceScale { get; private set; }
    public bool HasAdaptiveGeometry => _cache is not null;
    public int CachedPatchCount => _cache?.Count ?? 0;

    private MapTerrainResources(TerrainMeshResult meshes, LoadedTerrainSceneResources source,
        G2ShaderTextureBases textureBases, TerrainGbiMapMaterial? gbi)
    {
        (Meshes, Source, TextureBases, Gbi) = (meshes, source, textureBases, gbi);
        ProjectionSource = source.Height.ControlBinding.Resource.Source;
        ActivePatches = meshes.Patches;
    }

    public static async Task<MapTerrainResources> PrepareAsync(MapTerrainSourcePreparation prepared,
        int depth, bool nativeMap)
    {
        LoadedTerrainSceneResources source = prepared.Terrain;
        G2ShaderTextureBases textureBases = prepared.TextureBases;
        // Playable scenes start with a root prototype, then use the camera's mixed-node plan.
        TerrainMeshResult meshes = TerrainMesh.Build(source.Terrain, source.Height, depth);
        TerrainGbiMapMaterial? gbi = null;
        try
        {
            if (!nativeMap)
                gbi = await TerrainGbiMapMaterial.BuildAsync(meshes, source.Material);
        }
        catch { meshes.Dispose(); throw; }
        return new(meshes, source, textureBases, gbi);
    }

    public void InitializeAdaptiveGeometry()
    {
        LodDistanceScale = TerrainParameterResources.ReadLodDistanceScale(Source.Parameters);
        _cache = new(Meshes!, InitialHeight);
    }

    public void BuildPicker() => Picker = TerrainPicker.Build(Meshes!);

    public TerrainLodUpdate PrepareLod(Vector3 eye, System.Numerics.Matrix4x4 clip)
    {
        TerrainLodDrawPlan plan = TerrainLodDrawPlanner.Build(Meshes!.Source.SpatialTree,
            new(eye.X, eye.Y, eye.Z), LodDistanceScale, TerrainFrustumMath.Create(clip));
        TerrainLodPatchSelection[] desired = plan.Rows.Select(row => row.Patch).ToArray();
        bool changed = desired.Length != ActivePatches.Count || desired.Where((row, i) =>
            row.Node.Index != ActivePatches[i].SourceNode.Index ||
            row.EdgeFlags != (uint)ActivePatches[i].Mesh.GetMeta("source_patch_edge_flags").AsInt64()).Any();
        if (!changed) return new(plan, null, null);
        IReadOnlyList<TerrainPatch> next = _cache!.Get(desired);
        return new(plan, next, TerrainPicker.Build(next));
    }

    public void CommitLod(TerrainLodUpdate update)
    {
        ActivePatches = update.Patches!;
        Picker = update.Picker;
        _cache!.Trim(ActivePatches);
    }

    public void UpdateEye(Vector3 eye, ShaderMaterial? overlay)
    {
        if (Gbi is null) return;
        Gbi.SetEye(Gbi.Material, eye);
        if (overlay is not null) Gbi.SetEye(overlay, eye);
    }

    public void ReleaseGeometry()
    {
        Picker = null;
        ActivePatches = [];
        _cache?.Dispose(); _cache = null;
        Meshes?.Dispose(); Meshes = null;
    }

    /// <summary>An asynchronous load finished after its scene was superseded; no nodes borrowed it.</summary>
    public void DiscardPrepared()
    {
        Gbi?.Dispose();
        ReleaseGeometry();
    }

    public void Dispose()
    {
        ReleaseGeometry();
        Gbi?.Dispose();
    }
}

internal sealed record TerrainLodUpdate(TerrainLodDrawPlan Plan,
    IReadOnlyList<TerrainPatch>? Patches, TerrainPicker? Picker);
