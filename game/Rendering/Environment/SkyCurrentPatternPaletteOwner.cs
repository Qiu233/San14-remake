using San14.Data;

/// <summary>The actual Q80 Sky's finite Pattern field owner. The caller
/// publishes this same Sky CPU after graph3 preparation. Backend callbacks
/// supply actual target readiness and D49EE0 effects; no declaration key is
/// promoted to a ready wrapper or GPU completion.</summary>
public sealed class SkyCurrentPatternPaletteOwner<TWrapper> :
    IDeferredEarlyIndirectCurrentPatternSky where TWrapper : class
{
    private readonly SkyCpuState168Owner workState;
    private readonly SourceCreatedSkyPatternPalette<TWrapper> palette;
    private MapSourceCreatedF103CurrentFrame? currentGeometry;
    public SkyCurrentPatternPaletteOwner(SkyCpuState168Owner actualSameSkyWorkState,
        SourceCreatedSkyPatternPalette<TWrapper> actualSameSkyPatternFields)
        => (workState, palette) = (actualSameSkyWorkState ?? throw new ArgumentNullException(nameof(actualSameSkyWorkState)),
            actualSameSkyPatternFields ?? throw new ArgumentNullException(nameof(actualSameSkyPatternFields)));
    public string SourceEvidence => palette.Source.SourceEvidence + ";same-owned-Sky=" + workState.OwnedSkyIdentity;
    public SkyPatternPaletteUpdate PrepareCurrent(DeferredEarlyIndirectPatternCameraTail actualCamera,
        SkyAppliedSourceCloudWeatherFields actualWeather,
        IDeferredEarlyIndirectCurrentPatternResources actualResources,
        uint actualSelectedViewIndex, ISkyPatternPaletteBackend<TWrapper> actualBackend)
    {
        var input = SkyPatternCurrentCameraInputs.CaptureCurrent(actualCamera, actualWeather, actualResources);
        var update = palette.ApplyCurrent(palette.CombineCurrentState168(workState.CurrentState168),
            true, actualSelectedViewIndex, input, actualBackend);
        currentGeometry = actualCamera.Frame;
        return update;
    }
    public void RequireCurrent(MapSourceCreatedF103CurrentFrame actualGeometry)
    {
        if (!ReferenceEquals(currentGeometry, actualGeometry))
            throw new InvalidDataException("Pattern palette must belong to the actual current graph3 Sky preparation.");
        if (actualGeometry.Camera.ExecutableLocale != palette.Source.Locale)
            throw new InvalidDataException("Pattern palette and current Camera use different native profiles.");
    }
    public DeferredEarlyIndirectPatternSkyPalette ReadCurrentSkyPalette(
        MapSourceCreatedF103CurrentFrame actualGeometry)
    {
        RequireCurrent(actualGeometry);
        return new(palette.CombineCurrentState168(workState.CurrentState168),
            palette.CurrentWrappers40.Select(w => (object?)w).ToArray(), palette.CurrentMatrix180);
    }
}
