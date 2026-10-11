namespace San14.Data;

/// <summary>The static terrain, Field models and Ocean textures for one loaded
/// scene. Prepared files are local cache outputs; no prior render package is an
/// input. Other render graph media have their own preparation stages.</summary>
public sealed class D3D11SceneMediaPreparation
{
    private D3D11SceneMediaPreparation(D3D11TerrainInputPreparation terrain,
        D3D11ModelInputPreparation fieldModels, D3D11TerrainInputFrame initialTerrain,
        NamedViewRendererResources renderer, OceanShaderResources oceanShaders)
    {
        Terrain = terrain;
        FieldModels = fieldModels;
        InitialTerrain = initialTerrain;
        Renderer = renderer;
        OceanShaders = oceanShaders;
    }

    public D3D11TerrainInputPreparation Terrain { get; }
    public D3D11ModelInputPreparation FieldModels { get; }
    public D3D11TerrainInputFrame InitialTerrain { get; }
    public NamedViewRendererResources Renderer { get; }
    public OceanShaderResources OceanShaders { get; }

    /// <summary>Run once at scene loading, before any consumer opens a bank.
    /// Reuse these same model and terrain sources for camera and shadow setup.</summary>
    public static D3D11SceneMediaPreparation Prepare(GameResourceRepository repository,
        LoadedSceneResources scene, MapCameraConstructionPacket camera,
        string terrainDirectory, int width, int height)
    {
        var renderer = NamedViewRendererResources.Load(repository);
        var terrain = D3D11TerrainInputPreparation.Load(repository, scene, camera.ExecutableLocale);
        var initialTerrain = terrain.PrepareInitialFrame(camera, width, height);
        var models = D3D11ModelInputPreparation.Load(repository, scene,
            ScenePlacementReader.Read(scene), camera,
            placement => MapStaticModelPublication.FreshUnparented($"scene-field/{placement.Source.Index}"));
        string inputs = Path.GetDirectoryName(Path.GetFullPath(terrainDirectory))!;
        terrain.WriteNativeInputs(terrainDirectory, initialTerrain);
        models.WriteNativeInputs(Path.Combine(inputs, "model-continuous-input"));
        OceanGridSettingsEncoding.WriteNativeInputs(
            Path.Combine(inputs, OceanGridSettingsEncoding.NativeDirectory), renderer.OceanGrid);
        NamedViewRendererEncoding.WriteNativeInputs(
            Path.Combine(inputs, OceanGridSettingsEncoding.NativeDirectory), renderer, viewIndex: 0);
        D3D11OceanTextureInputPreparation.Load(repository, renderer, "system", "RRPreview")
            .WriteNativeInputs(Path.Combine(inputs, "ocean-texture-input"));
        var oceanShaders = OceanShaderResources.LoadPb2Wave(repository, renderer);
        oceanShaders.WriteNativeInputs(Path.Combine(inputs, "water-input"));
        return new(terrain, models, initialTerrain, renderer, oceanShaders);
    }
}
