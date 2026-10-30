using San14.Data;
using San14.Data.Formats;

/// <summary>CPU scene and terrain definitions for one requested load. No Godot
/// mesh, texture, view or live campaign owner enters the worker.</summary>
internal sealed record MapTerrainSourcePreparation(LoadedSceneResources Scene,
    ScenePlacements Placements, LoadedTerrainSceneResources Terrain, G2ShaderTextureBases TextureBases)
{
    internal static MapTerrainSourcePreparation Prepare(string sourceDirectory, string? overrideDirectory,
        string archive, uint sceneId, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var repository = new GameResourceRepository(sourceDirectory, overrideDirectory);
        var scene = SceneResources.Load(repository, archive, sceneId);
        token.ThrowIfCancellationRequested();
        var placements = ScenePlacementReader.Read(scene);
        var source = TerrainSceneResources.Load(repository, scene);
        token.ThrowIfCancellationRequested();
        var textureBases = G2ShaderTextureBaseReader.Read(source.Material.Shader.Shader.Source.Bytes);
        token.ThrowIfCancellationRequested();
        return new(scene, placements, source, textureBases);
    }
}
