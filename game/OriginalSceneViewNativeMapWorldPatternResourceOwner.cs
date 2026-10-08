using San14.Data;

/// <summary>Normal scene-owned ready STA Data and the Perspective package's
/// retained named results. The four physical images and white fallback remain
/// with their existing Sky/Lighting owners. This prepares resources, not an
/// early Indirect draw or a replacement for the Sky palette's matrix.</summary>
public sealed class OriginalSceneViewNativeMapWorldPatternResourceOwner : IAsyncDisposable
{
    private readonly LoadedOriginalPatternCameraResourceSource loader;
    private readonly OriginalPatternCameraOwnedNamedRegistry registry;
    private readonly Dictionary<OriginalMapSeasonCameraPackage, OriginalDeferredEarlyIndirectPatternResourceOwner> packages = new();
    private readonly uint contextFlags3E4;
    private bool retired;
    public OriginalPatternCameraStaDataOwner Sta { get; }
    public OriginalSkyAtmoPlaneDataOwner Plane { get; }
    public IReadOnlyList<OriginalPatternCameraStaReadyPlaneEntry> PlaneEntries { get; }
    public OriginalPatternCameraDeviceTextureData White { get; }
    public OriginalDeferredEarlyIndirectCurrentPatternResources? Current { get; private set; }
    public IReadOnlyList<OriginalDeferredEarlyIndirectCurrentRecord>? CurrentIndirectRecords { get; private set; }

    private OriginalSceneViewNativeMapWorldPatternResourceOwner(LoadedOriginalPatternCameraResourceSource loader,
        OriginalPatternCameraStaDataOwner sta, OriginalSkyAtmoPlaneDataOwner plane,
        IReadOnlyList<OriginalPatternCameraStaReadyPlaneEntry> entries, OriginalPatternCameraDeviceTextureData white,
        OriginalSkyDirectionalOptionalSourceOwner context, uint flags)
    {
        this.loader = loader; Sta = sta; Plane = plane; PlaneEntries = entries; White = white; contextFlags3E4 = flags;
        registry = new([sta, white], () => { ObjectDisposedException.ThrowIf(retired, this); context.RequireCurrentResourceContext(); },
            loader.SourceEvidence + ";normal-source-resource-context;actual-ready-STA-and-existing-Static-white");
    }

    public static async Task<OriginalSceneViewNativeMapWorldPatternResourceOwner> BuildAsync(
        OriginalGameRepository repository, string locale,
        OriginalSceneViewNativeMapWorldF103SourceOwner.CurrentFrame bootstrap,
        OriginalSkyDirectionalOptionalSourceOwner actualPreparationContext,
        OriginalMapSourceCreatedDirectionalLightingDeviceOwner lighting)
    {
        var source = OriginalSkyAtmoTableReadySource.Load(repository, locale,
            bootstrap.Rgb.Inputs.Source.Transmittance.Tables,
            actualPreparationContext.StaPlaneSource);
        var entries = await actualPreparationContext.BorrowReadyStaPlaneEntriesAsync();
        var plane = OriginalPatternCameraStaDataOwner.CreatePlaneFromReadyEntries(source, entries);
        var sta = OriginalPatternCameraStaDataOwner.CreateFresh(source, plane, repository,
            actualPreparationContext, actualPreparationContext.RequireCurrentResourceContext);
        try
        {
            if (sta.PrepareCurrent() != OriginalSkyAtmoReadyResult.Ready)
                throw new InvalidDataException("Normal STA Data has not completed the actual four-texture and registered-table preparation.");
            var owner = new OriginalSceneViewNativeMapWorldPatternResourceOwner(
                OriginalPatternCameraResourceSource.Load(repository, locale), sta, plane, entries,
                lighting.BorrowCurrentPatternCameraWhite(), actualPreparationContext, bootstrap.First.Lighting.ContextFlags3E4);
            owner.CaptureCurrent(bootstrap);
            return owner;
        }
        catch { sta.Dispose(); plane.Retire(); throw; }
    }

    public OriginalDeferredEarlyIndirectCurrentPatternResources CaptureCurrent(
        OriginalSceneViewNativeMapWorldF103SourceOwner.CurrentFrame world)
    {
        ObjectDisposedException.ThrowIf(retired, this);
        var tail = world.EarlyPatternCameraTail;
        var package = tail.MainInputs.Source.Selection.FirstPackage;
        if (!packages.TryGetValue(package, out var owner))
        {
            owner = OriginalDeferredEarlyIndirectPatternResourceOwner.BindLoadedPackage(loader, package, registry, contextFlags3E4);
            packages.Add(package, owner);
        }
        var resources = owner.CaptureCurrent(tail);
        CurrentIndirectRecords = OriginalDeferredEarlyIndirectCurrentGroups.Prepare(
            world.GeometryFirst, world.GeometryWorld, tail, resources);
        return Current = resources;
    }

    public ValueTask DisposeAsync()
    {
        if (!retired) { retired = true; Current = null; CurrentIndirectRecords = null; packages.Clear(); Sta.Dispose(); Plane.Retire(); }
        return ValueTask.CompletedTask;
    }
}
