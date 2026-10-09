namespace San14.Data;

/// <summary>The five current Camera fields tested by E46EC0. This is a
/// finite prepared-Camera read set, not a complete18C0 Camera allocation.</summary>
public sealed record SkyPatternCurrentCameraInputs(float CloudWeight640,
    float CirrusStrength748, float Pattern27C, bool PatternMap1680Present, int CameraKind3Ac)
{
    public static SkyPatternCurrentCameraInputs CaptureCurrent(
        DeferredEarlyIndirectPatternCameraTail actualCamera,
        SkyAppliedSourceCloudWeatherFields actualWeather,
        IDeferredEarlyIndirectCurrentPatternResources actualResources)
    {
        ArgumentNullException.ThrowIfNull(actualCamera); ArgumentNullException.ThrowIfNull(actualWeather);
        ArgumentNullException.ThrowIfNull(actualResources);
        actualCamera.RequireCurrent(actualCamera.Frame); actualResources.RequireCurrent(actualCamera.Frame);
        var present = actualWeather.AcceptedPresent;
        actualWeather.ValidateCurrent(present);
        if (!ReferenceEquals(present.AcceptedSeasonCamera.Inputs, actualCamera.MainInputs))
            throw new InvalidDataException("Sky Pattern Camera and weather must use the same current Main preparation.");
        return new(actualWeather.ReadFloat(0x640), actualWeather.ReadFloat(0x748),
            actualCamera.CameraPattern27C, actualResources.PatternMap1680Present, actualCamera.CameraKind3Ac);
    }
}

/// <summary>Actual named target lookup and wrapper initialization. Null may
/// only be an actual absent lookup or failed initializer. The held wrapper's
/// lifetime belongs to this backend, including its original retain/release.
/// InvokeCurrentProjection implements D49EE0, including its early returns;
/// it writes matrix180 only at the actual CPU matrix writer and does not
/// report a completed GPU draw merely because the caller invoked it.</summary>
public interface ISkyPatternPaletteBackend<TWrapper> where TWrapper : class
{
    TWrapper? PrepareCurrentNamedTarget(RenderTextureDefinition declaredTarget);
    void InvokeCurrentProjection(TWrapper actualWrapper, SkyPatternCurrentCameraInputs actualCamera,
        Memory<byte> actualRetainedMatrix180);
}

public sealed record SkyPatternPaletteUpdate(uint State168, bool WroteSelector,
    uint SelectedPatternIndex, bool ProjectionWasInvoked, bool PublishedNewWrapper,
    string SourceEvidence);

/// <summary>Loaded once from the actual SC/TC constructor, graph3 caller,
/// four-key loader, Host Q80 setter/getter and current Pattern callback.</summary>
public sealed class LoadedSkyPatternPaletteSource
{
    internal LoadedSkyPatternPaletteSource(string locale, string evidence)
        => (Locale, SourceEvidence) = (locale, evidence);
    public string Locale { get; }
    public string SourceEvidence { get; }
    public static LoadedSkyPatternPaletteSource Load(GameResourceRepository repository, string locale)
        => RenderSamplerSourceAdapters.LoadSkyPatternPaletteSource(repository.SourceDirectory, locale);
    public SourceCreatedSkyPatternPalette<TWrapper> ConstructFreshOwned<TWrapper>(
        LoadedSkyAmbientResources actualAmbient) where TWrapper : class
    {
        ArgumentNullException.ThrowIfNull(actualAmbient);
        if (actualAmbient.Object.ClassId != SkyAmbientResources.ClassId ||
            actualAmbient.PatternTargets.Count != 4 ||
            actualAmbient.PatternTargets.Any(t => t.Kind != RenderTextureKind.Texture2D))
            throw new InvalidDataException("Sky CPU constructor requires its actual four ordered2D Pattern declarations.");
        return new(this, actualAmbient.PatternTargets.ToArray());
    }
}

/// <summary>The limited retained Sky CPU40/170/180 field group. Fresh
/// E38440 explicitly writes all four wrappers and all64 matrix bytes zero.
/// The unused CPU60 stays null in this finite owner. This does not infer
/// current original-process values or target pixels from that constructor.</summary>
public sealed class SourceCreatedSkyPatternPalette<TWrapper> where TWrapper : class
{
    private readonly RenderTextureDefinition[] targets;
    private readonly TWrapper?[] wrappers = new TWrapper?[4];
    private readonly byte[] matrix180 = new byte[64];
    private uint selectorBits;
    internal SourceCreatedSkyPatternPalette(LoadedSkyPatternPaletteSource source,
        RenderTextureDefinition[] targets) => (Source, this.targets) = (source, targets);
    public LoadedSkyPatternPaletteSource Source { get; }
    public IReadOnlyList<TWrapper?> CurrentWrappers40 => Array.AsReadOnly(wrappers);
    public ReadOnlyMemory<byte> CurrentMatrix180 => matrix180;
    public uint CombineCurrentState168(uint actualWorkAndCubeState168)
        => (actualWorkAndCubeState168 & 0xFFFFE3FFu) | selectorBits;

    /// <summary>E46EC0 current call. A null Common table returns before any
    /// state write. Invalid/missing View or failed Camera gate writes selector4.
    /// Passing the gate writes0..3 even if actual lookup/initialization fails.
    /// The target and matrix are retained across ordinary calls.</summary>
    public SkyPatternPaletteUpdate ApplyCurrent(uint actualState168,
        bool actualCommonTablePresent, uint actualViewIndex, SkyPatternCurrentCameraInputs? actualCamera,
        ISkyPatternPaletteBackend<TWrapper> actualBackend)
    {
        ArgumentNullException.ThrowIfNull(actualBackend);
        if (!actualCommonTablePresent)
            return new(actualState168, false, (actualState168 >> 10) & 7, false, false,
                Source.SourceEvidence + ";E46EC0-Common-null-return-before-selector-store");
        uint index = 4; bool invoked = false, published = false;
        if (actualViewIndex < 32 && actualCamera is { } camera &&
            camera.CloudWeight640 > 0 && camera.CirrusStrength748 > 0 && camera.Pattern27C != 0 &&
            camera.PatternMap1680Present && unchecked((uint)(camera.CameraKind3Ac - 1)) < 4)
        {
            index = (uint)(camera.CameraKind3Ac - 1);
            TWrapper? wrapper = wrappers[index];
            if (wrapper is null)
            {
                wrapper = actualBackend.PrepareCurrentNamedTarget(targets[index]);
                if (wrapper is not null) { wrappers[index] = wrapper; published = true; }
            }
            if (wrapper is not null)
            {
                actualBackend.InvokeCurrentProjection(wrapper, camera, matrix180);
                invoked = true;
            }
        }
        selectorBits = index << 10;
        uint state = (actualState168 & 0xFFFFE3FFu) | selectorBits;
        return new(state, true, index, invoked, published,
            Source.SourceEvidence + ";E46EC0-current-640/748/27C/1680/3AC;retained40/180;selector10..12;AL=invocation-only");
    }
}
