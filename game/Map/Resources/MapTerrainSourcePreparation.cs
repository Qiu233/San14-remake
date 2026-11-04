using San14.Data;
using San14.Data.Formats;

/// <summary>CPU scene and terrain definitions for one requested load. No Godot
/// mesh, texture, view or live campaign owner enters the worker.</summary>
internal sealed record MapTerrainSourcePreparation(LoadedSceneResources Scene,
    ScenePlacements Placements, LoadedTerrainSceneResources Terrain, G2ShaderTextureBases TextureBases,
    MapStaticModelGroundHeightSource WorldGroundHeight)
{
    // Filled by the application startup closure before either locale borrows
    // this source. Nonprojected developer previews keep their existing path.
    internal PreparedTerrainGeometryCatalogue? CpuGeometry { get; init; }

    internal static MapTerrainSourcePreparation Prepare(string sourceDirectory, string? overrideDirectory,
        string archive, uint sceneId, CancellationToken token)
        => Prepare(new GameResourceRepository(sourceDirectory, overrideDirectory), archive, sceneId, token);

    internal static MapTerrainSourcePreparation Prepare(GameResourceRepository repository,
        string archive, uint sceneId, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(repository);
        token.ThrowIfCancellationRequested();
        var scene = SceneResources.Load(repository, archive, sceneId);
        token.ThrowIfCancellationRequested();
        var placements = ScenePlacementReader.Read(scene);
        var source = TerrainSceneResources.Load(repository, scene);
        token.ThrowIfCancellationRequested();
        var textureBases = G2ShaderTextureBaseReader.Read(source.Material.Shader.Shader.Source.Bytes);
        token.ThrowIfCancellationRequested();
        // The Scene Landscape's fresh Field+B0 samples world X/Z. Keep this
        // immutable source with the existing startup terrain closure; the
        // camera's separately centered coordinate state is not this owner.
        var worldGroundHeight = MapStaticModelGroundHeightSource.PrepareCurrentField(repository, scene,
            UnitWorldGroundHeightFieldPreparationBranch.RetainedWrapperCreated, true, []);
        token.ThrowIfCancellationRequested();
        return new(scene, placements, source, textureBases, worldGroundHeight);
    }
}
