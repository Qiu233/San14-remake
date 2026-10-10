using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>Owns one scene's terrain uploads, materials and adaptive geometry.
/// Scene nodes borrow patches; detach them before committing or releasing geometry.</summary>
internal sealed class MapTerrainResources : IDisposable
{
    private TerrainMeshCache? _cache;
    private readonly LoadedTerrainParameterResources _parameters;
    public TerrainMeshResult? Meshes { get; private set; }
    public LoadedTerrainInitialHeightResources InitialHeight { get; }
    public LoadedTerrainShaderResources Shader { get; }
    public G2ShaderTextureBases TextureBases { get; }
    public TerrainPreviewMaterial? Preview { get; }
    public TerrainGbiMapMaterial? Gbi { get; }
    public ITerrainSurface? Surface => (ITerrainSurface?)Gbi ?? Preview;
    public LoadedRdbAsset ProjectionSource { get; }
    public IReadOnlyList<TerrainPatch> ActivePatches { get; private set; }
    public TerrainPicker? Picker { get; private set; }
    public float LodDistanceScale { get; private set; }
    public bool HasAdaptiveGeometry => _cache is not null;
    public int CachedPatchCount => _cache?.Count ?? 0;

    private MapTerrainResources(TerrainMeshResult meshes, LoadedTerrainInitialHeightResources height,
        LoadedTerrainParameterResources parameters, LoadedTerrainShaderResources shader,
        G2ShaderTextureBases textureBases, TerrainPreviewMaterial? preview,
        TerrainGbiMapMaterial? gbi, LoadedRdbAsset? nativeProjection)
    {
        (Meshes, InitialHeight, _parameters, Shader, TextureBases, Preview, Gbi) =
            (meshes, height, parameters, shader, textureBases, preview, gbi);
        ProjectionSource = nativeProjection ?? Surface!.ProjectionSource;
        ActivePatches = meshes.Patches;
    }

    public static async Task<MapTerrainResources> PrepareAsync(GameResourceRepository repository,
        string archive, LoadedSceneResources scene, int depth, bool nativeMap,
        TerrainTexturePreviewPolicy texturePolicy, string locale)
    {
        LoadedTerrainResources terrain = TerrainResources.Load(repository, archive, scene.TerrainBundle.Source.FileId);
        if (terrain.Objects.Source.ResourceUri != scene.TerrainBundle.Source.ResourceUri ||
            terrain.Objects.Source.DecodedSha256 != scene.TerrainBundle.Source.DecodedSha256)
            throw new InvalidDataException($"Terrain bundle {scene.TerrainBundle.Source.ResourceUri} changed while loading the scene.");
        LoadedTerrainParameterResources parameters = TerrainParameterResources.Read(scene);
        LoadedTerrainShaderResources shader = TerrainShaderResources.Load(repository, parameters);
        G2ShaderTextureBases textureBases = G2ShaderTextureBaseReader.Read(shader.Shader.Source.Bytes);
        LoadedTerrainInitialHeightResources height = TerrainInitialHeightResources.Load(repository, terrain, parameters);
        // Playable scenes start with a root prototype, then use the camera's mixed-node plan.
        TerrainMeshResult meshes = TerrainMesh.Build(terrain, height, depth);
        TerrainPreviewMaterial? preview = null;
        TerrainGbiMapMaterial? gbi = null;
        LoadedRdbAsset? nativeProjection = null;
        try
        {
            if (nativeMap)
            {
                var field = TerrainTextureResources.LoadGroup(repository, terrain, TerrainTextureGroupKind.Field);
                nativeProjection = TerrainInitialControlResources.LoadStockDefault(field).Binding.Resource.Source;
            }
            else
            {
                preview = await TerrainPreviewMaterial.BuildAsync(repository, meshes, texturePolicy, parameters);
                try { gbi = await TerrainGbiMapMaterial.BuildAsync(repository, meshes, parameters, locale); }
                catch { preview.Dispose(); throw; }
            }
        }
        catch { meshes.Dispose(); throw; }
        return new(meshes, height, parameters, shader, textureBases, preview, gbi, nativeProjection);
    }

    public void InitializeAdaptiveGeometry()
    {
        LodDistanceScale = TerrainParameterResources.ReadLodDistanceScale(_parameters);
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
        Preview?.Dispose();
        Gbi?.Dispose();
        ReleaseGeometry();
    }

    public void Dispose()
    {
        ReleaseGeometry();
        Gbi?.Dispose();
        Preview?.Dispose();
    }
}

internal sealed record TerrainLodUpdate(TerrainLodDrawPlan Plan,
    IReadOnlyList<TerrainPatch>? Patches, TerrainPicker? Picker);
