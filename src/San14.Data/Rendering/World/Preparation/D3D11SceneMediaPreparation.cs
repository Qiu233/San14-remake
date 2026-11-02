namespace San14.Data;

/// <summary>The static terrain, Field models and Ocean textures for one loaded
/// scene. Prepared files are local cache outputs; no prior render package is an
/// input. Other render graph media have their own preparation stages.</summary>
public sealed class D3D11SceneMediaPreparation
{
    private D3D11SceneMediaPreparation(D3D11TerrainInputPreparation terrain,
        D3D11ModelInputPreparation fieldModels, D3D11TerrainInputFrame initialTerrain,
        NamedViewRendererResources renderer, OceanShaderResources oceanShaders,
        LoadedReflectionSceneRootSource sceneRoot, D3D11ReflectionSceneInputPreparation reflectionScene,
        D3D11ReflectionInputPreparation reflectionInputs)
    {
        Terrain = terrain;
        FieldModels = fieldModels;
        InitialTerrain = initialTerrain;
        Renderer = renderer;
        OceanShaders = oceanShaders;
        SceneRoot = sceneRoot;
        ReflectionScene = reflectionScene;
        ReflectionInputs = reflectionInputs;
    }

    public D3D11TerrainInputPreparation Terrain { get; }
    public D3D11ModelInputPreparation FieldModels { get; }
    public D3D11TerrainInputFrame InitialTerrain { get; }
    public NamedViewRendererResources Renderer { get; }
    public OceanShaderResources OceanShaders { get; }
    public LoadedReflectionSceneRootSource SceneRoot { get; }
    public D3D11ReflectionSceneInputPreparation ReflectionScene { get; }
    public D3D11ReflectionInputPreparation ReflectionInputs { get; }

    /// <summary>Run once at scene loading, before any consumer opens a bank.
    /// Reuse these same model and terrain sources for camera and shadow setup.</summary>
    public static D3D11SceneMediaPreparation Prepare(GameResourceRepository repository,
        LoadedSceneResources scene, MapCameraConstructionPacket camera,
        string terrainDirectory, int width, int height)
        => Prepare(repository, TerrainSceneResources.Load(repository, scene), camera,
            terrainDirectory, width, height);

    public static D3D11SceneMediaPreparation Prepare(GameResourceRepository repository,
        LoadedTerrainSceneResources sources, MapCameraConstructionPacket camera,
        string terrainDirectory, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(camera);
        return PrepareOpening(repository, LoadSources(repository, sources, camera.ExecutableLocale),
            camera, terrainDirectory, width, height);
    }

    /// <summary>Load the static closure without a World, accepted camera or
    /// output directory. The application's repository generation retains it.</summary>
    public static D3D11SceneMediaSources LoadSources(GameResourceRepository repository,
        LoadedTerrainSceneResources sources, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(sources);
        var renderer = NamedViewRendererResources.Load(repository);
        var terrain = D3D11TerrainInputSources.Load(repository, sources, locale);
        var root = LoadedReflectionSceneRootSource.Load(repository, sources.Scene);
        var placements = root.CreateModelAndEffectPlacements();
        var models = D3D11ModelInputSources.Load(repository, sources.Scene, placements, locale);
        var common = CommonRenderResources.ImportSettings(repository.LoadObjects(
            "KIDSSystemResource", CommonRenderResources.SourceFileId));
        var oceanTextures = D3D11OceanTextureInputPreparation.Load(repository, renderer, "system", "RRPreview");
        var oceanShaders = OceanShaderResources.LoadPb2Wave(repository, renderer);
        return new(terrain, models, renderer, oceanShaders, oceanTextures, root, common);
    }

    /// <summary>Construct only this opening's publications and accepted-camera
    /// output bank from loaded source content. No current state is retained in
    /// the shared source object.</summary>
    public static D3D11SceneMediaPreparation PrepareOpening(GameResourceRepository repository,
        D3D11SceneMediaSources sources, MapCameraConstructionPacket camera,
        string terrainDirectory, int width, int height)
        => PrepareOpening(repository, sources, camera, terrainDirectory, width, height, null);

    public static D3D11SceneMediaPreparation PrepareOpening(GameResourceRepository repository,
        D3D11SceneMediaSources sources, MapCameraConstructionPacket camera,
        string terrainDirectory, int width, int height, D3D11TerrainPreparedPrograms? terrainPrograms)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(camera);
        if (sources.Locale != camera.ExecutableLocale)
            throw new InvalidDataException("Scene media sources differ from the accepted camera locale.");
        var renderer = sources.Renderer;
        var terrain = D3D11TerrainInputPreparation.CreateOpening(sources.Terrain);
        var initialTerrain = terrain.PrepareInitialFrame(camera, width, height);
        var root = sources.SceneRoot;
        var rootPlacements = sources.FieldModels.Placements;
        var models = D3D11ModelInputPreparation.PrepareOpening(repository, sources.FieldModels, camera,
            placement => MapStaticModelPublication.FreshUnparented($"scene-field/{placement.Source.Index}"));
        var common = sources.Common;
        var reflectionScene = D3D11ReflectionSceneInputPreparation.Prepare(
            root, rootPlacements, models, common, camera);
        var reflectionInputs = D3D11ReflectionInputPreparation.Prepare(repository, models, rootPlacements, camera);
        string inputs = Path.GetDirectoryName(Path.GetFullPath(terrainDirectory))!;
        reflectionScene.WriteNativeInputs(inputs);
        reflectionInputs.WriteNativeInputs(inputs);
        terrain.WriteNativeInputs(terrainDirectory, initialTerrain, terrainPrograms);
        models.WriteNativeInputs(Path.Combine(inputs, "model-continuous-input"));
        OceanGridSettingsEncoding.WriteNativeInputs(
            Path.Combine(inputs, OceanGridSettingsEncoding.NativeDirectory), renderer.OceanGrid);
        NamedViewRendererEncoding.WriteNativeInputs(
            Path.Combine(inputs, OceanGridSettingsEncoding.NativeDirectory), renderer, viewIndex: 0);
        sources.OceanTextures.WriteNativeInputs(Path.Combine(inputs, "ocean-texture-input"));
        var oceanShaders = sources.OceanShaders;
        oceanShaders.WriteNativeInputs(Path.Combine(inputs, "water-input"));
        return new(terrain, models, initialTerrain, renderer, oceanShaders, root, reflectionScene, reflectionInputs);
    }
}
