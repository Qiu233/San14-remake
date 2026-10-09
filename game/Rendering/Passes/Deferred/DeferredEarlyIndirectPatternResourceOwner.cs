using San14.Data;

/// <summary>A finite current registry over already published named Data.
/// Unknown requested keys need their real loader; they cannot masquerade as
/// a failed lookup. Existing Data owners keep their actual readiness/lifetime.</summary>
public sealed class PatternCameraOwnedNamedRegistry : IPatternCameraCurrentNamedRegistry
{
    private readonly IReadOnlyDictionary<uint, IPatternCameraCurrentNamedData> entries;
    private readonly Action requireActualResourceContext;
    public PatternCameraOwnedNamedRegistry(IReadOnlyList<IPatternCameraCurrentNamedData> actualPublishedData,
        Action requireActualCurrentResourceContext, string actualSourceEvidence)
    {
        ArgumentNullException.ThrowIfNull(actualPublishedData);
        requireActualResourceContext = requireActualCurrentResourceContext ?? throw new ArgumentNullException(nameof(requireActualCurrentResourceContext));
        ArgumentException.ThrowIfNullOrWhiteSpace(actualSourceEvidence);
        entries = actualPublishedData.ToDictionary(value => value.ObjectKey);
        SourceEvidence = actualSourceEvidence;
        foreach (var value in entries.Values) value.RequireCurrent();
    }
    public string SourceEvidence { get; }
    public void RequireCurrent() => requireActualResourceContext();
    public IPatternCameraCurrentNamedData? LookupCurrentData(uint objectKey)
    {
        RequireCurrent();
        if (objectKey == 0) return null;
        if (!entries.TryGetValue(objectKey, out var data))
            throw new NotSupportedException($"Named Pattern Data {objectKey:X8} has no actual ready producer in this finite registry.");
        data.RequireCurrent(); return data;
    }
}

/// <summary>The actual package loader's three retained Data results. Build
/// once while the selected source package/resource context activates. V468's
/// selected embedded projection is bound to the normal Main/Lighting packet;
/// it never substitutes the separate Main raw bank for selected resources.</summary>
public sealed class DeferredEarlyIndirectPatternResourceOwner
{
    private readonly MapSeasonCameraPackage package;
    private readonly PatternCameraResolvedResources resources;
    private DeferredEarlyIndirectPatternResourceOwner(MapSeasonCameraPackage actualPackage,
        PatternCameraResolvedResources actualResources)
        => (package, resources) = (actualPackage, actualResources);

    public PatternCameraResolvedResources RetainedResources => resources;
    public static DeferredEarlyIndirectPatternResourceOwner BindLoadedPackage(
        LoadedPatternCameraResourceSource loadedSource, MapSeasonCameraPackage actualPackage,
        IPatternCameraCurrentNamedRegistry actualRegistry, uint actualLoaderContextFlags3E4)
    {
        ArgumentNullException.ThrowIfNull(loadedSource); ArgumentNullException.ThrowIfNull(actualPackage);
        var declaration = loadedSource.ReadDeclaration(actualPackage.Camera, actualPackage.SourceEvidence);
        return new(actualPackage, loadedSource.BindFreshResources(declaration, actualRegistry, actualLoaderContextFlags3E4));
    }
    public DeferredEarlyIndirectCurrentPatternResources CaptureCurrent(
        DeferredEarlyIndirectPatternCameraTail actualPreparedTail)
    {
        ArgumentNullException.ThrowIfNull(actualPreparedTail);
        actualPreparedTail.RequireCurrent(actualPreparedTail.Frame); resources.RequireCurrent();
        if (!ReferenceEquals(actualPreparedTail.MainInputs.Source.Selection.FirstPackage, package) ||
            actualPreparedTail.MainInputs.Source.Selection.SecondPackage is not null)
            throw new InvalidDataException("Pattern retained Data does not belong to this actual Main-selected source package.");
        return new(actualPreparedTail.Frame, resources);
    }
}

/// <summary>Frame association for the actual retained Data references. The
/// booleans consumed by PatternCamera derive from these references only.</summary>
public sealed class DeferredEarlyIndirectCurrentPatternResources : IDeferredEarlyIndirectCurrentPatternResources
{
    private readonly MapSourceCreatedF103CurrentFrame geometry;
    private readonly PatternCameraResolvedResources resources;
    internal DeferredEarlyIndirectCurrentPatternResources(MapSourceCreatedF103CurrentFrame actualGeometry,
        PatternCameraResolvedResources actualResources)
        => (geometry, resources) = (actualGeometry, actualResources);
    public IPatternCameraCurrentNamedData? Resource1680 => resources.SkylightSta;
    public IPatternCameraCurrentNamedData? Resource1888 => resources.MergedPattern;
    public IPatternCameraCurrentNamedData? Resource1890 => resources.OfflinePattern;
    /// <summary>The actual ready STA Data retained at K1680. Its table
    /// values do not substitute for the separate current Sky palette's
    /// Pattern wrapper/matrix read by D74F30.</summary>
    public SkyAtmoTableCpuOwner? PatternMap1680StaData
    {
        get
        {
            if (Resource1680 is not { } data) return null;
            var cpu = data.CurrentDataOwner as SkyAtmoTableCpuOwner ??
                throw new NotSupportedException("Current Pattern K1680 Data is not the supported actual STA CPU owner.");
            cpu.RequireCurrent();
            return cpu;
        }
    }
    public bool PatternMap1680Present => Resource1680 is not null;
    public bool PatternMap1888Present => Resource1888 is not null;
    public bool PatternMap1890Present => Resource1890 is not null;
    public string SourceEvidence => resources.SourceEvidence + ";same-actual-Main-selected/prepared-Lighting-geometry";
    public void RequireCurrent(MapSourceCreatedF103CurrentFrame actualGeometry)
    {
        if (!ReferenceEquals(actualGeometry, geometry))
            throw new InvalidDataException("Pattern resource packet belongs to another current geometry.");
        resources.RequireCurrent();
    }
}
