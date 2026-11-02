using San14.Data;

public partial class MapSceneView
{
    private MapStaticResources? _staticMapResources;
    private MapStaticSceneResources? _loadedStaticMapScene;

    internal void SetStaticMapResources(MapStaticResources resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        if (_loaded || _sceneLoadInProgress)
            throw new InvalidOperationException("Bind static map sources before the scene starts loading.");
        _staticMapResources = resources;
    }

    private MapStaticSceneResources? CaptureStaticMapScene(string archive, uint scene)
    {
        if (!_sceneConfig.TryGetProjection(scene, out _))
        {
            if (_worldScene)
                throw new NotSupportedException("The product world requires a declared projected map source.");
            // The explicitly selected nonprojected developer preview is not
            // part of the product startup closure; keep its existing loader.
            return null;
        }
        return (_staticMapResources ?? throw new InvalidOperationException(
            "Projected map sources were not bound by application startup."))
            .RequireScene(archive, scene, _text.SourceLocale);
    }

    private MapStaticSceneResources RequireLoadedStaticMapScene(LoadedTerrainSceneResources terrain)
    {
        var source = _loadedStaticMapScene ?? throw new InvalidOperationException(
            "The Native map has no loaded application static source.");
        source.RequireCurrentRevision();
        if (!ReferenceEquals(source.Terrain.Terrain, terrain) || source.Media.Locale != _text.SourceLocale)
            throw new InvalidDataException("The Native map opening has another static source generation or locale.");
        return source;
    }
}
