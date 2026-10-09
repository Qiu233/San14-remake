namespace San14.Data;

/// <summary>Only the three actual references read by D49EE0 from the A40
/// View cache. This does not manufacture the other A40 bytes, a native
/// pointer, a complete renderer resource or a completed TextureGroup.</summary>
public sealed record SkyPatternCurrentViewCacheReferences(
    IPatternCameraCurrentNamedData? CameraPatternData88,
    SkyAppliedSourceCloudSelectionOwner? CloudRenderer9E0,
    SkyAppliedSourceCloudOwner? CloudInput9E8, string SourceEvidence)
{
    /// <summary>The supported ready STA branch follows actual Data20 to its
    /// CPU8 Plane Data. The returned owner represents the complete typed20
    /// group; its four ordered descriptors are not the group pointer itself.</summary>
    public SkyAtmoPlaneDataOwner? ReadCurrentReadyStaPlaneGroup()
    {
        if (CameraPatternData88 is null) return null;
        CameraPatternData88.RequireCurrent();
        if (CameraPatternData88.FactoryClassId != SkyAtmoTableResources.ClassId ||
            CameraPatternData88.CurrentDataOwner is not SkyAtmoTableCpuOwner cpu)
            throw new NotSupportedException("Pattern cache88 requires its supported actual STA CPU, not a declaration or descriptor.");
        cpu.RequireCurrent();
        var plane = cpu.PlaneData; plane.RequireCurrent();
        return plane;
    }
}

/// <summary>Load boundary for D4BD30 current field stores, D6ABE0 publish
/// and D5BEA0 query. Every actual prepared View has a fresh nullable cache;
/// the named View's long-lived RendererResource is a separate owner.</summary>
public sealed class LoadedSkyPatternCurrentViewCacheSource
{
    internal LoadedSkyPatternCurrentViewCacheSource(string locale, string evidence)
        => (Locale, SourceEvidence) = (locale, evidence);
    public string Locale { get; }
    public string SourceEvidence { get; }
    public static LoadedSkyPatternCurrentViewCacheSource Load(GameResourceRepository repository, string locale)
        => RenderSamplerSourceAdapters.LoadSkyPatternCurrentViewCacheSource(repository.SourceDirectory, locale);
    public SourceCreatedSkyPatternCurrentViewCache ConstructFreshPreparedViewSubset(
        MapSourceCreatedViewFieldsPacket actualPreparedView)
        => new(this, actualPreparedView ?? throw new ArgumentNullException(nameof(actualPreparedView)));

    /// <summary>The supported special-Main read set: actual Camera1680 is
    /// nonnull, so D4C798 does not read fallback1678. Renderer refs come from
    /// the real retained E37F10/11919E0 owners of this current Cloud frame,
    /// rather than its static shader declaration or a selected World record.</summary>
    public SkyPatternCurrentViewCacheReferences CaptureCurrentSpecialMainReferences(
        DeferredEarlyIndirectPatternCameraTail actualCamera,
        IDeferredEarlyIndirectCurrentPatternResources actualResources,
        IPatternCameraCurrentNamedData actualCameraData1680,
        SkyAppliedSourceCloudFrame actualCloud)
    {
        ArgumentNullException.ThrowIfNull(actualCamera); ArgumentNullException.ThrowIfNull(actualResources);
        ArgumentNullException.ThrowIfNull(actualCameraData1680); ArgumentNullException.ThrowIfNull(actualCloud);
        actualCamera.RequireCurrent(actualCamera.Frame); actualResources.RequireCurrent(actualCamera.Frame);
        actualCameraData1680.RequireCurrent();
        if (!actualResources.PatternMap1680Present || actualResources.PatternMap1680StaData is null ||
            !ReferenceEquals(actualCameraData1680.CurrentDataOwner, actualResources.PatternMap1680StaData) ||
            actualCameraData1680.FactoryClassId != SkyAtmoTableResources.ClassId ||
            !ReferenceEquals(actualCloud.AcceptedPresent.AcceptedSeasonCamera.Inputs, actualCamera.MainInputs) ||
            actualCamera.PreparedCamera.ExecutableLocale != Locale)
            throw new InvalidDataException("Pattern cache needs the exact current Main/STA/Cloud reference producers.");
        return new(actualCameraData1680, actualCloud.Selection.SourceCreatedRendererOwner,
            actualCloud.SourceCreatedInputOwner, SourceEvidence + ";D4C775-Camera1680/no-fallback1678;" +
            "D4D151/16E-actual-R148/R150;E49855-E33F00-E37F10/E49867-11919E0-11915C0;" +
            actualCameraData1680.SourceEvidence + ";" + actualCloud.SourceProvenance);
    }
}

/// <summary>The finite reference projection of one real prepared View1B90.
/// Current allocation results stay explicit. This object is retired with
/// that prepared View, never cached under a named View across requests.</summary>
public sealed class SourceCreatedSkyPatternCurrentViewCache
{
    private readonly MapSourceCreatedViewFieldsPacket preparedView;
    private SkyPatternCurrentViewCacheReferences? references;
    internal SourceCreatedSkyPatternCurrentViewCache(LoadedSkyPatternCurrentViewCacheSource source,
        MapSourceCreatedViewFieldsPacket actualView) => (Source, preparedView) = (source, actualView);
    public LoadedSkyPatternCurrentViewCacheSource Source { get; }
    public bool HasCurrentReferenceSubset => references is not null;

    /// <summary>D6ABE0's already-present branch returnsfalse before reading
    /// input or allocating. After successful current A40 allocation it copies
    /// the caller's fields once. False allocation leaves1B90 absent; it does
    /// not publish zero references as an allocated cache.</summary>
    public bool PublishAfterActualCurrentA40Allocation(bool allocationSucceeded,
        Func<SkyPatternCurrentViewCacheReferences> readActualCurrentFields)
    {
        ArgumentNullException.ThrowIfNull(readActualCurrentFields);
        if (references is not null || !allocationSucceeded) return false;
        references = readActualCurrentFields() ?? throw new InvalidDataException("Actual cache source fields are unresolved.");
        return true;
    }
    public bool TryReadCurrentReferenceSubset(MapSourceCreatedViewFieldsPacket actualPreparedView,
        out SkyPatternCurrentViewCacheReferences? actualReferences)
    {
        if (!ReferenceEquals(preparedView, actualPreparedView))
            throw new InvalidDataException("View1B90 field subset belongs to another prepared View.");
        actualReferences = references;
        return references is not null;
    }
}
