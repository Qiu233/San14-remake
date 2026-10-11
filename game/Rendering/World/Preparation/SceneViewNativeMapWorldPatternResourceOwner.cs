using San14.Data;

/// <summary>Normal scene-owned ready STA Data and the Perspective package's
/// retained named results. The four physical images and white fallback remain
/// with their existing Sky/Lighting owners. This prepares resources, not an
/// early Indirect draw or a replacement for the Sky palette's matrix.</summary>
public sealed class SceneViewNativeMapWorldPatternResourceOwner : IAsyncDisposable
{
    private readonly LoadedPatternCameraResourceSource loader;
    private readonly PatternCameraOwnedNamedRegistry registry;
    private readonly Dictionary<MapSeasonCameraPackage, DeferredEarlyIndirectPatternResourceOwner> packages = new();
    private readonly uint contextFlags3E4;
    private bool retired;
    public PatternCameraStaDataOwner Sta { get; }
    public SkyAtmoPlaneDataOwner Plane { get; }
    public IReadOnlyList<PatternCameraStaReadyPlaneEntry> PlaneEntries { get; }
    public PatternCameraDeviceTextureData White { get; }
    public DeferredEarlyIndirectCurrentPatternResources? Current { get; private set; }
    public IReadOnlyList<DeferredEarlyIndirectCurrentRecord>? CurrentIndirectRecords { get; private set; }

    private SceneViewNativeMapWorldPatternResourceOwner(LoadedPatternCameraResourceSource loader,
        PatternCameraStaDataOwner sta, SkyAtmoPlaneDataOwner plane,
        IReadOnlyList<PatternCameraStaReadyPlaneEntry> entries, PatternCameraDeviceTextureData white,
        SkyDirectionalOptionalSourceOwner context, uint flags)
    {
        this.loader = loader; Sta = sta; Plane = plane; PlaneEntries = entries; White = white; contextFlags3E4 = flags;
        registry = new([sta, white], () => { ObjectDisposedException.ThrowIf(retired, this); context.RequireCurrentResourceContext(); },
            loader.SourceEvidence + ";normal-source-resource-context;actual-ready-STA-and-existing-Static-white");
    }

    public static async Task<SceneViewNativeMapWorldPatternResourceOwner> BuildAsync(
        GameResourceRepository repository, string locale,
        SceneViewNativeMapWorldF103SourceOwner.CurrentFrame bootstrap,
        SkyDirectionalOptionalSourceOwner actualPreparationContext,
        MapSourceCreatedDirectionalLightingDeviceOwner lighting)
    {
        var source = SkyAtmoTableReadySource.Load(repository, locale,
            bootstrap.Rgb.Inputs.Source.Transmittance.Tables,
            actualPreparationContext.StaPlaneSource);
        var entries = await actualPreparationContext.BorrowReadyStaPlaneEntriesAsync();
        var plane = PatternCameraStaDataOwner.CreatePlaneFromReadyEntries(source, entries);
        var sta = PatternCameraStaDataOwner.CreateFresh(source, plane, repository,
            actualPreparationContext, actualPreparationContext.RequireCurrentResourceContext);
        try
        {
            if (sta.PrepareCurrent() != SkyAtmoReadyResult.Ready)
                throw new InvalidDataException("Normal STA Data has not completed the actual four-texture and registered-table preparation.");
            var owner = new SceneViewNativeMapWorldPatternResourceOwner(
                PatternCameraResourceSource.Load(repository, locale), sta, plane, entries,
                lighting.BorrowCurrentPatternCameraWhite(), actualPreparationContext, bootstrap.First.Lighting.ContextFlags3E4);
            owner.CaptureCurrent(bootstrap);
            return owner;
        }
        catch { sta.Dispose(); plane.Retire(); throw; }
    }

    internal bool UsesTables(LoadedSkyAtmoTableSources tables)=>Sta.Source.Tables.SameResources(tables);

    /// <summary>Build a new STA/Plane and registry generation from the candidate
    /// physical entries. It owns no submitted frame and leaves the old cache alive.</summary>
    internal async Task<SceneViewNativeMapWorldPatternResourceOwner> PrepareSeasonReplacementAsync(
        GameResourceRepository repository,string locale,SceneViewNativeMapSeasonResourceBundle resources,
        SceneViewNativeMapSkyCloudTextureOwner textures,SkyDirectionalOptionalSourceOwner context)
    {
        ObjectDisposedException.ThrowIf(retired,this);
        var planeSource=resources.CloudTextures.SkylightSta;
        var source=SkyAtmoTableReadySource.Load(repository,locale,resources.CameraSource.Transmittance.Tables,planeSource);
        var readyEntries=await textures.BorrowReadyStaPlaneEntriesAsync();
        // A new declaration can name the same physical images. Adopt its keys
        // while borrowing the already prepared descriptors and lifetime owner.
        var entries=readyEntries.Select((entry,index)=>entry with{Source=planeSource.Entries[index]}).ToArray();
        var plane=PatternCameraStaDataOwner.CreatePlaneFromReadyEntries(source,entries);
        var sta=PatternCameraStaDataOwner.CreateFresh(source,plane,repository,context,context.RequireCurrentResourceContext);
        try
        {
            if(sta.PrepareCurrent()!=SkyAtmoReadyResult.Ready)
                throw new InvalidDataException("Replacement STA has not completed its four actual textures and registered tables.");
            var replacement=new SceneViewNativeMapWorldPatternResourceOwner(loader,sta,plane,entries,White,context,contextFlags3E4);
            replacement.BindPackage(resources.CameraSource.Selection.FirstPackage);
            return replacement;
        }
        catch{sta.Dispose();plane.Retire();throw;}
    }
    private DeferredEarlyIndirectPatternResourceOwner BindPackage(MapSeasonCameraPackage package)
    {
        if(!packages.TryGetValue(package,out var owner))
        {
            owner=DeferredEarlyIndirectPatternResourceOwner.BindLoadedPackage(loader,package,registry,contextFlags3E4);
            packages.Add(package,owner);
        }
        return owner;
    }

    public DeferredEarlyIndirectCurrentPatternResources CaptureCurrent(
        SceneViewNativeMapWorldF103SourceOwner.CurrentFrame world)
    {
        ObjectDisposedException.ThrowIf(retired, this);
        var tail = world.EarlyPatternCameraTail;
        var package = tail.MainInputs.Source.Selection.FirstPackage;
        var resources = BindPackage(package).CaptureCurrent(tail);
        CurrentIndirectRecords = DeferredEarlyIndirectCurrentGroups.Prepare(
            world.GeometryFirst, world.GeometryWorld, tail, resources);
        return Current = resources;
    }

    public ValueTask DisposeAsync()
    {
        if (!retired) { retired = true; Current = null; CurrentIndirectRecords = null; packages.Clear(); Sta.Dispose(); Plane.Retire(); }
        return ValueTask.CompletedTask;
    }
}
