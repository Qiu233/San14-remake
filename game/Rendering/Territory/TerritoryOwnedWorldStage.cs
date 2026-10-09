using San14.Data;

/// <summary>One detached owned-world input and the caller's current source raster state.</summary>
public sealed record TerritoryOwnedWorldStageInputs(
    TerritoryOwnedWorldSnapshot Ownership, ModelRasterInputs Raster);

/// <summary>Composes the original ScreenMap consumer with a live source Exposure producer.
/// Capture ownership on its owner thread before publishing it here. This owner keeps
/// the territory renderer alive; Exposure and the preceding scene stages are borrowed.
/// Retire the composed scene before disposing this owner, then dispose Exposure after
/// every consumer has retired. Callbacks execute under the scene's serial gate.</summary>
public sealed class TerritoryOwnedWorldStage : IAsyncDisposable
{
    public TerritorySceneProvider Provider { get; }
    public MapExposureOwner Exposure { get; }
    public TerritoryOwnedWorldSnapshot? LastCapturedSnapshot { get; private set; }

    private TerritoryOwnedWorldStage(TerritorySceneProvider provider,
        MapExposureOwner exposure) => (Provider, Exposure) = (provider, exposure);

    public static async Task<TerritoryOwnedWorldStage> BuildAsync(
        GameResourceRepository repository, LoadedTerritoryResources source,
        LoadedMap map, LoadedTerrainTypes terrain, string locale, Vkd3dShaderCompiler compiler,
        MapExposureOwner exposure,
        Func<NativeMapFrameContext, TerritoryOwnedWorldStageInputs> current,
        Func<NativeMapFrameContext, MapExposurePacket> currentExposure)
    {
        ArgumentNullException.ThrowIfNull(exposure);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(currentExposure);
        TerritoryOwnedWorldStage? owner = null;
        var provider = await TerritorySceneProvider.BuildAsync(repository, source, map,
            terrain, locale, compiler, frame =>
            {
                var input = current(frame) ?? throw new InvalidDataException("Owned territory frame input is missing.");
                var snapshot = input.Ownership ?? throw new InvalidDataException("Owned territory snapshot is missing.");
                if (snapshot.SourceIdentity.MapSha256 != map.Source.Sha256 ||
                    snapshot.SourceIdentity.MapResourceUri != map.Source.ResourceUri)
                    throw new InvalidDataException("Owned territory snapshot differs from the selected source map.");
                if (snapshot.SourceIdentity.FixedDataResourceUri != terrain.Source.ResourceUri ||
                    !StringComparer.OrdinalIgnoreCase.Equals(snapshot.SourceIdentity.FixedDataSha256, terrain.Source.Sha256))
                    throw new InvalidDataException("Owned territory snapshot differs from the selected source terrain fixed data.");
                var raster = input.Raster ?? throw new InvalidDataException("Current territory raster state is missing.");
                if (raster.Attachments is null) throw new InvalidDataException("Current territory raster attachments are missing.");
                // Exposure may await a render-thread producer before the renderer takes
                // its own packet snapshot. Retain this accepted frame's attachments now.
                var capturedRaster = raster with { Attachments = Array.AsReadOnly(raster.Attachments.ToArray()) };
                owner!.LastCapturedSnapshot = snapshot;
                return new(snapshot.CreateWorldCopy(), snapshot.SourceForceColorIndices,
                    snapshot.Style, snapshot.StyleInvalidationGeneration, capturedRaster);
            }, frame => exposure.PrepareAndBorrowAsync(frame,
                currentExposure(frame) ?? throw new InvalidDataException("Same-frame source Exposure packet is missing.")));
        owner = new(provider, exposure);
        return owner;
    }

    /// <summary>The complete stage record preserves the prior final-output stage.
    /// Territory draws after prior terrain/models and retires before parent targets.</summary>
    public NativeMapSceneStages Stages(NativeMapSceneStages? previous = null)
    {
        return Provider.Stages(previous);
    }

    public Task RenderAsync(NativeMapFrameContext frame) => Provider.RenderAsync(frame);
    public Task RetireTargetAsync() => Provider.RetireTargetAsync();
    public ValueTask DisposeAsync() => Provider.DisposeAsync();
}
