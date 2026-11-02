namespace San14.Data;

/// <summary>Finite Field model sources from the selected scene. Raw local
/// geometry and declarations are shared; current publication, SDF registration,
/// camera constants and output bytes are created by each opening.</summary>
public sealed class D3D11ModelInputSources
{
    private D3D11ModelInputSources(LoadedSceneResources scene, ScenePlacements placements,
        string locale, IReadOnlyList<MapStaticModelAsset> assets,
        ModelQueueStateSource queue54, ModelQueueStateSource queue55, LoadedObjects shaderObjects,
        WorldPrimitiveOrderSource order, IReadOnlyList<int> fields, IReadOnlyList<int> alphaFields,
        MapModelNativeFrustumCullingSource common)
    {
        Scene = scene; Placements = placements; Locale = locale; Assets = assets;
        Queue54 = queue54; Queue55 = queue55; ShaderObjects = shaderObjects;
        Order = order; Fields = fields; AlphaFields = alphaFields; Common = common;
    }

    public LoadedSceneResources Scene { get; }
    public ScenePlacements Placements { get; }
    public string Locale { get; }
    public IReadOnlyList<MapStaticModelAsset> Assets { get; }
    internal ModelQueueStateSource Queue54 { get; }
    internal ModelQueueStateSource Queue55 { get; }
    internal LoadedObjects ShaderObjects { get; }
    internal WorldPrimitiveOrderSource Order { get; }
    internal IReadOnlyList<int> Fields { get; }
    internal IReadOnlyList<int> AlphaFields { get; }
    internal MapModelNativeFrustumCullingSource Common { get; }

    public static D3D11ModelInputSources Load(GameResourceRepository repository,
        LoadedSceneResources scene, ScenePlacements placements, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(placements);
        if (locale is not ("SC" or "TC"))
            throw new InvalidDataException("Field model sources require SC or TC.");
        if (placements.Scene.Scene.Source.ResourceUri != scene.Scene.Source.ResourceUri ||
            placements.Scene.Scene.Source.DecodedSha256 != scene.Scene.Source.DecodedSha256)
            throw new InvalidDataException("Model placements differ from the selected source scene.");
        // This temporary importer has no SDF lifetime. Only its immutable raw
        // assets survive; an opening creates its own actual SDF registrations.
        var importer = new MapStaticModelAssetCatalog(repository, scene);
        var assets = new Dictionary<string, MapStaticModelAsset>(StringComparer.Ordinal);
        foreach (var placement in placements.Instances.Where(p => p.Model is not null))
        {
            var asset = importer.LoadSceneInstance(placement.Source);
            assets.TryAdd(asset.CategoryIdentity, asset);
        }
        var graph = repository.LoadRenderGraph("system", 0x024496EE);
        var queue54 = ModelRendererStateSourceReader.ReadQueue(graph, 54);
        var queue55 = ModelRendererStateSourceReader.ReadQueue(graph, 55);
        var shaderObjects = repository.LoadObjects("system", 0x718CCB8B);
        var order = WorldPrimitiveOrderSource.LoadDefaultView0(repository);
        var fields = WorldPrimitiveSortDescriptor.ReadActualFields(order.Descriptor(WorldPrimitiveSortFamily.Opaque).ToArray());
        var alphaFields = WorldPrimitiveSortDescriptor.ReadActualFields(order.Descriptor(WorldPrimitiveSortFamily.Alpha).ToArray());
        if (!alphaFields.SequenceEqual(new[] { 14 }))
            throw new NotSupportedException("Current Model alpha sort is outside source distance14.");
        if (!fields.SequenceEqual(new[] { 14, 3 }))
            throw new NotSupportedException("Current Model first-bank sort is outside opaque14->3.");
        var common = MapModelNativeVisibilitySources.ReadCommonViewTemplate(
            repository.LoadObjects("KIDSSystemResource", CommonRenderResources.SourceFileId), CommonRenderResources.SourceObjectId);
        return new(scene, placements, locale, Array.AsReadOnly(assets.Values.ToArray()),
            queue54, queue55, shaderObjects, order,
            Array.AsReadOnly(fields.ToArray()), Array.AsReadOnly(alphaFields.ToArray()), common);
    }
}
