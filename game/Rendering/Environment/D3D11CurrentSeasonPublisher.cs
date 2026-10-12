using San14.Core;
using San14.Data;

public sealed class D3D11CurrentSeasonLight
{
    internal D3D11CurrentSeasonLight(uint objectId, uint groupMask, byte[] data)
    {
        ObjectId = objectId; GroupMask = groupMask; CompleteData = data;
        Lighting = HdrLightEncoding.Read(data); Exposure = D3D11CurrentExposureEncoding.ReadLight(data);
        ReceiverBias = ShadowReceiverBiasEncoding.ReadLight(data);
        PatternModifier = PatternCurrentEncoding.ReadLight(data, Lighting.Modifier.Present);
    }
    public uint ObjectId { get; }
    public uint GroupMask { get; }
    public HdrLightState Lighting { get; }
    public LightExposureSettings Exposure { get; }
    public ShadowReceiverBias ReceiverBias { get; }
    public PatternLightModifier PatternModifier { get; }
    internal ReadOnlyMemory<byte> CompleteData { get; }
}

/// <summary>Same-request normal Main season/RGB/weather/local-light writes.
/// Geometry retains its preceding camera; post/Sky/lighting consume this
/// prepared prefix. This packet neither creates GPU owners nor advances World.</summary>
public sealed class D3D11CurrentSeasonFrame
{
    internal D3D11CurrentSeasonFrame(MainMapCameraModePacket mode,
        NativeMapSeasonState worldSeason, SkySeasonMainCameraRgbFrame rgb,
        MapSourceCreatedSeasonLightingFrame present,
        D3D11PreparedMainWord[] words, D3D11CurrentSeasonLight[] lights,
        D3D11CurrentAtmosphereFrame atmosphere,
        D3D11CurrentExposureLight exposureLight, CameraOpticsSettings optics, CameraViewSettings viewSettings,
        OceanWaveSettings oceanWaves, OceanSurfaceSettings oceanSurface, OceanOpticsSettings oceanOptics,
        OceanTextureMaterialSettings oceanTextureMaterial, HeightFogSettings heightFog,
        CommonRenderPolicy commonPolicy, OceanBubbleMaterialSettings oceanBubbleMaterial, SkyStarSettings stars,
        PatternCameraSettings patternSettings)
    {
        Mode = mode; WorldSeason = worldSeason; Rgb = rgb; Present = present;
        PreparedMain = present.PreparedCameraPrefix; PreparedWords = Array.AsReadOnly(words);
        Lights = Array.AsReadOnly(lights); Atmosphere = atmosphere; ExposureLight = exposureLight;
        Optics = optics; ViewSettings = viewSettings; OceanWaves = oceanWaves; OceanSurface = oceanSurface;
        OceanOptics = oceanOptics; OceanTextureMaterial = oceanTextureMaterial;
        HeightFog = heightFog;
        CommonPolicy = commonPolicy;
        OceanBubbleMaterial = oceanBubbleMaterial;
        Stars = stars;
        PatternSettings = patternSettings;
    }
    public MainMapCameraModePacket Mode { get; }
    public NativeMapSeasonState WorldSeason { get; }
    public SkySeasonMainCameraRgbFrame Rgb { get; }
    public MapSourceCreatedSeasonLightingFrame Present { get; }
    public ReadOnlyMemory<byte> PreparedMain { get; }
    public IReadOnlyList<D3D11PreparedMainWord> PreparedWords { get; }
    public IReadOnlyList<D3D11CurrentSeasonLight> Lights { get; }
    public D3D11CurrentAtmosphereFrame Atmosphere { get; }
    public D3D11CurrentExposureLight ExposureLight { get; }
    public CameraOpticsSettings Optics { get; }
    public CameraViewSettings ViewSettings { get; }
    public OceanWaveSettings OceanWaves { get; }
    public OceanSurfaceSettings OceanSurface { get; }
    public OceanOpticsSettings OceanOptics { get; }
    public OceanTextureMaterialSettings OceanTextureMaterial { get; }
    public OceanBubbleMaterialSettings OceanBubbleMaterial { get; }
    public SkyStarSettings Stars { get; }
    public PatternCameraSettings PatternSettings { get; }
    public HeightFogSettings HeightFog { get; }
    public CommonRenderPolicy CommonPolicy { get; }
    public int MainMode => Mode.Mode288;
    public int SeasonIndex => Rgb.Inputs.Source.Season.Resources.Selection.SeasonIndex;
    public SkyWeatherResult Weather => Present.Weather;
    public ReadOnlyMemory<byte> MainFog72 => PreparedMain.Slice(0x340,72);
    public uint HdrLutKey => Optics.ColorGrading.HdrLutKey;
    public uint LdrLutKey => Optics.ColorGrading.LdrLutKey;

}

/// <summary>Retained CPU current producer for the native D3D11 session. Load
/// kernels once; consume the immutable World and camera after coalescing delta.
/// Accept only after native success so skipped pending cameras do not advance
/// the previous RGB transition. A failed native session is closed by its owner.</summary>
public sealed class D3D11CurrentSeasonPublisher
{
    private D3D11CurrentSeasonResources _resources;
    private readonly MainMapCameraModeOwner _mode;
    private readonly MapMainCameraPostSelectionOwner _post;
    private readonly MapGameLightQualityOwner _quality;
    private readonly MapSeasonMainCameraPhaseSource _phase;
    private readonly SkySceneControllerSourceProfile _skyController;
    private readonly SkySceneSky2Parameters _sky;
    private readonly LoadedSkyDirectionalSetterColorNativeSource _color;
    private AcceptedSkyLightingState? _previousLighting;
    private readonly D3D11CurrentSeasonLibrary? _library;

    public D3D11CurrentSeasonPublisher(GameResourceRepository repository,
        D3D11CurrentSeasonResources resources,
        MainMapCameraModeOwner? actualMainMode = null,
        MapGameLightQualityOwner? actualQuality = null,
        D3D11CurrentSeasonLibrary? resourceLibrary = null)
    {
        _resources = resources;
        _library = resourceLibrary;
        string locale = resources.Source.Locale;
        _mode = actualMainMode ?? MainMapCameraModeOwner.CreateFresh(locale, "native-current-season/App");
        _quality = actualQuality ?? MapGameLightQualityOwner.CreateFreshPrimary(repository, locale, "native-current-season/Configs");
        _post = MapMainCameraPostSelectionOwner.CreateFresh(repository, _mode);
        _phase = MapSeasonMainCameraPhaseSource.LoadSourceDefaults(repository, locale);
        _skyController = SkySceneSourceDefaults.CreateControllerConstructor();
        _sky = SkySceneSourceDefaults.CreateSky2Parameters(107);
        _color = SkyDirectionalSetterColorNativeSource.Load(repository, locale);
    }

    public MapSeasonSelection LoadedSelection => _resources.Selection;
    public D3D11CurrentSeasonResources LoadedResources => _resources;

    /// <summary>Caller installs the already loaded generation while the render
    /// pump is drained. Main/App, previous RGB and native history stay alive.</summary>
    public void Install(D3D11CurrentSeasonResources resources)
    {
        if (resources.Source.Locale != _resources.Source.Locale)
            throw new InvalidDataException("A live Main season cannot switch executable locale.");
        _resources = resources;
    }

    public D3D11CurrentSeasonFrame PrepareCurrent(GameState world,
        MainMapCameraFrame currentMain, float elapsedSeconds)
    {
        var season = world.NativeMapSeason ?? throw new NotSupportedException(
            "Current World has no owned third-date season; Core.Date cannot supply it.");
        var calendar = MapSeasonRuntime.PrepareFrame(season, currentMain.Camera.FrameNumber);
        if (calendar.Selection != _resources.Selection && _library is not null)
            Install(_library.Resolve(season, currentMain).Resources);
        var selected = _resources.Source.CameraSource.WithCalendar(calendar);
        var mode = _mode.PrepareCurrent(currentMain);
        var post = _post.PrepareCurrent(selected, mode, _quality);
        var before = SkySeasonMainCameraProducer.PrepareBeforeFinalPreparation(
            selected, currentMain, mode.Mode288, _phase,
            previousAcceptedLightingState: _previousLighting, mainPostSelection: post);
        var rgb = SkySeasonMainCameraProducer.PrepareSunMoonAndSky(before,
            elapsedSeconds, NativeCameraTangentDispatch.Sse, _skyController, _sky);
        var weather = SkySeasonMainWeatherFog.PrepareSourceDatabaseResetFrame(rgb,
            selected.Transmittance.Power, NativeCameraTangentDispatch.Sse);
        var present = MapSourceCreatedSeasonLightingFrame.CapturePreparedSeason(rgb, weather);
        var actual = selected.PostEffectScalarSource.CaptureActualMainPreparation(before, currentMain.Camera);
        var viewSettings = CameraViewSettingsEncoding.ApplyPreparedOverrides(_resources.ViewDefaults, actual.Scalars);
        var surface = _resources.OceanSurface with
        { CutoffDistance = actual.Scalars.QualityPatch.OceanCutoffDistance ?? _resources.OceanSurface.CutoffDistance };
        var oceanOptics = _resources.OceanOptics with
        { RefractionIndex = actual.Scalars.QualityPatch.OceanRefractionIndex ?? _resources.OceanOptics.RefractionIndex };
        var heightFog = HeightFogSettingsEncoding.ApplyQualityPatch(_resources.HeightFog, actual.Scalars.QualityPatch);
        var words = D3D11CurrentSeasonEncoding.MainWrites(actual, _resources.ViewDefaults, _resources.OceanWaves, surface, oceanOptics,
            _resources.CloudShaderSettings);
        var prefix = present.PreparedCameraPrefix;
        var lights = _resources.Lights.Select(light => new D3D11CurrentSeasonLight(
            light.ObjectId, light.GroupMask,
            light.DefaultCaller.ApplyCurrentCamera(prefix.Span).Bytes)).ToArray();
        var atmosphere = D3D11CurrentAtmosphereFrame.Prepare(present,
            selected.Transmittance.Tangent, _color,
            lights.First(light => (light.GroupMask & 0x80) != 0).CompleteData.Span,
            _resources.ControllerGeometry40);
        return new(mode, season, rgb, present,
            words, lights, atmosphere, D3D11CurrentExposureLight.Capture(_resources.FreshExposureSelector, lights),
            CameraOpticsEncoding.DecodePrefix(currentMain.Camera.Source, prefix.Span, actual.Scalars.ApplyOpticalTailTo(currentMain.Camera.Optics)), viewSettings, _resources.OceanWaves, surface, oceanOptics, _resources.OceanTextureMaterial, heightFog, _resources.CommonPolicy, _resources.OceanBubbleMaterial, _resources.Stars, _resources.PatternSettings);
    }

    public void AcceptCurrent(D3D11CurrentSeasonFrame frame) => _previousLighting = AcceptedSkyLightingState.Capture(frame.Rgb);
}
