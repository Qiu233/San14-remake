using San14.Core;
using San14.Data;

/// <summary>Loaded CPU declarations for one source season. Main, View, clocks,
/// runtime light selection and GPU resources remain owned by the live session.
/// Loading this bundle does not publish a ready device or a completed frame.</summary>
public sealed class SceneViewNativeMapSeasonResourceBundle
{
    private SceneViewNativeMapSeasonResourceBundle(string locale,
        SkySeasonMainCameraSource camera,
        MapSeasonDirectionalLightSourceSet directional,
        IReadOnlyList<MapWorldKind1SourceMetadata> metadata,
        SkyAppliedSeasonCloudTextureSources cloudTextures)
    { Locale = locale; CameraSource = camera; DirectionalDeclarations = directional; LightMetadata = metadata;CloudTextures=cloudTextures; }

    public string Locale { get; }
    public MapSeasonSelection Selection => CameraSource.Season.Resources.Selection;
    public SkySeasonMainCameraSource CameraSource { get; }
    public MapSeasonDirectionalLightSourceSet DirectionalDeclarations { get; }
    public IReadOnlyList<MapWorldKind1SourceMetadata> LightMetadata { get; }
    public SkyAppliedSeasonCloudTextureSources CloudTextures{get;}

    /// <summary>Resolve and validate declarations at actual load/reload. The
    /// five lights are read from the very same loaded seasonal database as the
    /// Camera provider, rather than from a second inferred date or file key.</summary>
    public static SceneViewNativeMapSeasonResourceBundle Load(
        GameResourceRepository repository, NativeMapSeasonState ownedSeason, string locale, long sourceFrameNumber)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(ownedSeason);
        var camera = SkySeasonMainCameraProducer.ResolveOwnedSource(repository,
            MapSeasonRuntime.ResolveSourceFrame(repository, ownedSeason, sourceFrameNumber), locale);
        var directional = MapSeasonDirectionalLightSources.Read(camera.Selection.Provider.Database);
        var metadata = MapSourceCreatedWorldKind1Publication.ReadSeasonSourceRecords(directional);
        return new(locale, camera, directional, metadata,SkyAppliedSeasonCloudTextures.Load(repository,camera));
    }

    internal void RequireCurrentLightSources(IReadOnlyList<MapSourceCreatedDirectionalLightingSource> sources)
    {
        if (!sources.Select(source => source.Construction.SourceObjectId).SequenceEqual(LightMetadata.Select(row => row.SourceObjectId)) ||
            sources.Any(source => source.Construction.SourceUri != DirectionalDeclarations.Source.ResourceUri ||
                source.Construction.SourceSha256 != DirectionalDeclarations.Source.DecodedSha256))
            throw new InvalidDataException("Current World lights must be the actual ordered constructors of the selected season database.");
    }
}
