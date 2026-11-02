namespace San14.Data;

/// <summary>Selected scene's static terrain, Field and Ocean source closure.
/// It owns no World, accepted camera, current publication or output directory.
/// Resource generations and residency belong to the supplying repository.</summary>
public sealed class D3D11SceneMediaSources
{
    internal D3D11SceneMediaSources(D3D11TerrainInputSources terrain,
        D3D11ModelInputSources fieldModels, NamedViewRendererResources renderer,
        OceanShaderResources oceanShaders, D3D11OceanTextureInputPreparation oceanTextures,
        LoadedReflectionSceneRootSource sceneRoot, CommonRenderSettings common)
    {
        Terrain = terrain; FieldModels = fieldModels; Renderer = renderer;
        OceanShaders = oceanShaders; OceanTextures = oceanTextures; SceneRoot = sceneRoot; Common = common;
    }

    public D3D11TerrainInputSources Terrain { get; }
    public D3D11ModelInputSources FieldModels { get; }
    public NamedViewRendererResources Renderer { get; }
    public OceanShaderResources OceanShaders { get; }
    public D3D11OceanTextureInputPreparation OceanTextures { get; }
    public LoadedReflectionSceneRootSource SceneRoot { get; }
    internal CommonRenderSettings Common { get; }
    public string Locale => Terrain.Locale;
}
