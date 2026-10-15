namespace San14.Data;

/// <summary>One scene's inert terrain sources. Geometry, device materials and
/// native input preparation share this owner; reloading creates a new owner.</summary>
public sealed class LoadedTerrainSceneResources
{
    internal LoadedTerrainSceneResources(LoadedSceneResources scene, LoadedTerrainResources terrain,
        LoadedTerrainParameterResources parameters, LoadedTerrainGbiMapResources material,
        LoadedTerrainInitialHeightResources height)
        => (Scene, Terrain, Parameters, Material, Height) = (scene, terrain, parameters, material, height);

    public LoadedSceneResources Scene { get; }
    public LoadedTerrainResources Terrain { get; }
    public LoadedTerrainParameterResources Parameters { get; }
    public LoadedTerrainGbiMapResources Material { get; }
    public LoadedTerrainInitialHeightResources Height { get; }
}

public static class TerrainSceneResources
{
    public static LoadedTerrainSceneResources Load(GameResourceRepository repository, LoadedSceneResources scene)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(scene);
        var terrain = TerrainResources.Load(repository, scene.TerrainBundle);
        var parameters = TerrainParameterResources.Read(scene);
        var material = TerrainGbiMapResources.LoadStockDefault(repository, terrain, parameters);
        var height = TerrainInitialHeightResources.Load(repository, terrain, parameters, material.Field);
        return new(scene, terrain, parameters, material, height);
    }
}
