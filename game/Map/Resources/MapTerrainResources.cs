using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>Owns scene terrain geometry, optional Godot uploads and materials.
/// Scene nodes borrow patches; detach them before committing or releasing geometry.</summary>
internal sealed class MapTerrainResources : IDisposable
{
    private TerrainMeshCache? _cache;
    private readonly PreparedTerrainGeometryCatalogue? cpuGeometry;
    private readonly TerrainLodDrawPlanner lodPlanner;
    private TerrainLodDrawPlan? cachedLodPlan, committedLodPlan;
    private Vector3 cachedLodEye;
    private System.Numerics.Matrix4x4 cachedLodClip;
    private float cachedLodDistanceScale;
    public LoadedTerrainSceneResources Source { get; }
    public MapStaticModelGroundHeightSource WorldGroundHeight { get; }
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
        G2ShaderTextureBases textureBases, TerrainGbiMapMaterial? gbi,
        MapStaticModelGroundHeightSource worldGroundHeight, PreparedTerrainGeometryCatalogue? cpuGeometry)
    {
        this.cpuGeometry = cpuGeometry;
        (Meshes, Source, TextureBases, Gbi) = (meshes, source, textureBases, gbi);
        WorldGroundHeight = worldGroundHeight;
        ProjectionSource = source.Height.ControlBinding.Resource.Source;
        ActivePatches = meshes.Patches;
        lodPlanner = new(source.Terrain.SpatialTree);
    }

    public static async Task<MapTerrainResources> PrepareAsync(MapTerrainSourcePreparation prepared,
        int depth, bool nativeMap)
    {
        LoadedTerrainSceneResources source = prepared.Terrain;
        G2ShaderTextureBases textureBases = prepared.TextureBases;
        // Product views borrow their startup generation's CPU geometry. A
        // nonprojected developer preview retains the existing local producer.
        var cpuGeometry = nativeMap ? prepared.CpuGeometry : null;
        TerrainMeshResult meshes = nativeMap
            ? cpuGeometry is not null
                ? TerrainMesh.RetainCpu(cpuGeometry, TerrainMeshSelection.Uniform(source.Terrain.SpatialTree, depth))
                : TerrainMesh.PrepareCpu(source.Terrain, source.Height, depth)
            : TerrainMesh.Build(source.Terrain, source.Height, depth);
        TerrainGbiMapMaterial? gbi = null;
        try
        {
            if (!nativeMap)
                gbi = await TerrainGbiMapMaterial.BuildAsync(meshes, source.Material);
        }
        catch { meshes.Dispose(); throw; }
        return new(meshes, source, textureBases, gbi, prepared.WorldGroundHeight, cpuGeometry);
    }

    public void InitializeAdaptiveGeometry()
    {
        LodDistanceScale = TerrainParameterResources.ReadLodDistanceScale(Source.Parameters);
        _cache = new(Meshes!, InitialHeight, cpuGeometry: cpuGeometry);
    }

    public void BuildPicker() => Picker = TerrainPicker.Build(Meshes!);

    public TerrainLodUpdate PrepareLod(Vector3 eye, System.Numerics.Matrix4x4 clip)
    {
        TerrainLodDrawPlan plan;
        if (cachedLodPlan is { } cached && cachedLodEye == eye && cachedLodClip == clip &&
            cachedLodDistanceScale == LodDistanceScale)
            plan = cached;
        else
        {
            plan = lodPlanner.Build(new(eye.X, eye.Y, eye.Z), LodDistanceScale, TerrainFrustumMath.Create(clip));
            cachedLodPlan = plan;
            cachedLodEye = eye;
            cachedLodClip = clip;
            cachedLodDistanceScale = LodDistanceScale;
        }
        if (ReferenceEquals(plan, committedLodPlan)) return new(plan, null, null);
        TerrainLodPatchSelection[] desired = plan.Rows.Select(row => row.Patch).ToArray();
        bool changed = desired.Length != ActivePatches.Count || desired.Where((row, i) =>
            row.Node.Index != ActivePatches[i].SourceNode.Index ||
            row.EdgeFlags != ActivePatches[i].EdgeFlags).Any();
        if (!changed)
        {
            committedLodPlan = plan;
            return new(plan, null, null);
        }
        IReadOnlyList<TerrainPatch> next = _cache!.Get(desired);
        return new(plan, next, TerrainPicker.Build(next));
    }

    public void CommitLod(TerrainLodUpdate update)
    {
        ActivePatches = update.Patches!;
        Picker = update.Picker;
        committedLodPlan = update.Plan;
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
        cachedLodPlan = committedLodPlan = null;
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
