using San14.Core;
using San14.Data;

/// <summary>Loaded CPU declarations for one source season. Main, View, clocks,
/// runtime light selection and GPU resources remain owned by the live session.
/// Loading this bundle does not publish a ready device or a completed frame.</summary>
public sealed class OriginalSceneViewNativeMapSeasonResourceBundle
{
    private OriginalSceneViewNativeMapSeasonResourceBundle(string locale,
        OriginalSkySeasonMainCameraSource camera,
        OriginalMapSeasonDirectionalLightSourceSet directional,
        IReadOnlyList<OriginalMapWorldKind1SourceMetadata> metadata,
        OriginalSkyAppliedSeasonCloudTextureSources cloudTextures)
    { Locale = locale; CameraSource = camera; DirectionalDeclarations = directional; LightMetadata = metadata;CloudTextures=cloudTextures; }

    public string Locale { get; }
    public OriginalMapSeasonSelection Selection => CameraSource.Season.Resources.Selection;
    public OriginalSkySeasonMainCameraSource CameraSource { get; }
    public OriginalMapSeasonDirectionalLightSourceSet DirectionalDeclarations { get; }
    public IReadOnlyList<OriginalMapWorldKind1SourceMetadata> LightMetadata { get; }
    public OriginalSkyAppliedSeasonCloudTextureSources CloudTextures{get;}

    /// <summary>Resolve and validate declarations at actual load/reload. The
    /// five lights are read from the very same loaded seasonal database as the
    /// Camera provider, rather than from a second inferred date or file key.</summary>
    public static OriginalSceneViewNativeMapSeasonResourceBundle Load(
        OriginalGameRepository repository, NativeMapSeasonState ownedSeason, string locale, long sourceFrameNumber)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(ownedSeason);
        var camera = OriginalSkySeasonMainCameraProducer.ResolveOwnedSource(repository,
            OriginalMapSeasonRuntime.ResolveSourceFrame(repository, ownedSeason, sourceFrameNumber), locale);
        var directional = OriginalMapSeasonDirectionalLightSources.Read(camera.Selection.Provider.Database);
        var metadata = OriginalMapSourceCreatedWorldKind1Publication.ReadSeasonSourceRecords(directional);
        return new(locale, camera, directional, metadata,OriginalSkyAppliedSeasonCloudTextures.Load(repository,camera));
    }

    internal void RequireCurrentLightSources(IReadOnlyList<OriginalMapSourceCreatedDirectionalLightingSource> sources)
    {
        if (!sources.Select(source => source.Construction.SourceObjectId).SequenceEqual(LightMetadata.Select(row => row.SourceObjectId)) ||
            sources.Any(source => source.Construction.SourceUri != DirectionalDeclarations.Source.ResourceUri ||
                source.Construction.SourceSha256 != DirectionalDeclarations.Source.DecodedSha256))
            throw new InvalidDataException("Current World lights must be the actual ordered constructors of the selected season database.");
    }
}
