namespace San14.Data;

/// <summary>Read-only source declarations for ordinary terrain. This object
/// owns no camera, frame, output directory or replaceable parameter lifetime.</summary>
public sealed class D3D11TerrainInputSources
{
    private D3D11TerrainInputSources(LoadedTerrainSceneResources source, string locale,
        byte callFlags, LoadedRenderGraphTargets targets, TerrainShadowResources shadow)
        => (Source, Locale, OrdinaryCallFlags, Targets, Shadow) = (source, locale, callFlags, targets, shadow);

    public LoadedTerrainSceneResources Source { get; }
    public string Locale { get; }
    internal byte OrdinaryCallFlags { get; }
    internal LoadedRenderGraphTargets Targets { get; }
    internal TerrainShadowResources Shadow { get; }

    public static D3D11TerrainInputSources Load(GameResourceRepository repository,
        LoadedTerrainSceneResources source, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(source);
        if (locale is not ("SC" or "TC"))
            throw new InvalidDataException("Ordinary terrain sources require SC or TC.");
        byte callFlags = MainSceneNode51Source.Load(repository, locale, 56).InitialCallFlags;
        var graph = repository.LoadRenderGraph("system", 0x024496EE);
        var targets = RenderGraphTargetResources.Read(graph, 53, repository.LoadObjects("system", 0x93FB3280));
        return new(source, locale, callFlags, targets, TerrainShadowResources.Load(repository, source.Parameters));
    }
}
