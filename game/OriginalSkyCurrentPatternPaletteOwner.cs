using San14.Data;

/// <summary>The actual Q80 Sky's finite Pattern field owner. The caller
/// publishes this same Sky CPU after graph3 preparation. Backend callbacks
/// supply actual target readiness and D49EE0 effects; no declaration key is
/// promoted to a ready wrapper or GPU completion.</summary>
public sealed class OriginalSkyCurrentPatternPaletteOwner<TWrapper> :
    IOriginalDeferredEarlyIndirectCurrentPatternSky where TWrapper : class
{
    private readonly OriginalSkyCpuState168Owner workState;
    private readonly SourceCreatedOriginalSkyPatternPalette<TWrapper> palette;
    private OriginalMapSourceCreatedF103CurrentFrame? currentGeometry;
    public OriginalSkyCurrentPatternPaletteOwner(OriginalSkyCpuState168Owner actualSameSkyWorkState,
        SourceCreatedOriginalSkyPatternPalette<TWrapper> actualSameSkyPatternFields)
        => (workState, palette) = (actualSameSkyWorkState ?? throw new ArgumentNullException(nameof(actualSameSkyWorkState)),
            actualSameSkyPatternFields ?? throw new ArgumentNullException(nameof(actualSameSkyPatternFields)));
    public string SourceEvidence => palette.Source.SourceEvidence + ";same-owned-Sky=" + workState.OwnedSkyIdentity;
    public OriginalSkyPatternPaletteUpdate PrepareCurrent(OriginalDeferredEarlyIndirectPatternCameraTail actualCamera,
        OriginalSkyAppliedSourceCloudWeatherFields actualWeather,
        IOriginalDeferredEarlyIndirectCurrentPatternResources actualResources,
        uint actualSelectedViewIndex, IOriginalSkyPatternPaletteBackend<TWrapper> actualBackend)
    {
        var input = OriginalSkyPatternCurrentCameraInputs.CaptureCurrent(actualCamera, actualWeather, actualResources);
        var update = palette.ApplyCurrent(palette.CombineCurrentState168(workState.CurrentState168),
            true, actualSelectedViewIndex, input, actualBackend);
        currentGeometry = actualCamera.Frame;
        return update;
    }
    public void RequireCurrent(OriginalMapSourceCreatedF103CurrentFrame actualGeometry)
    {
        if (!ReferenceEquals(currentGeometry, actualGeometry))
            throw new InvalidDataException("Pattern palette must belong to the actual current graph3 Sky preparation.");
        if (actualGeometry.Camera.ExecutableLocale != palette.Source.Locale)
            throw new InvalidDataException("Pattern palette and current Camera use different native profiles.");
    }
    public OriginalDeferredEarlyIndirectPatternSkyPalette ReadCurrentSkyPalette(
        OriginalMapSourceCreatedF103CurrentFrame actualGeometry)
    {
        RequireCurrent(actualGeometry);
        return new(palette.CombineCurrentState168(workState.CurrentState168),
            palette.CurrentWrappers40.Select(w => (object?)w).ToArray(), palette.CurrentMatrix180);
    }
}
