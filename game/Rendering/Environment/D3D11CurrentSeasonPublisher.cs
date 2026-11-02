using San14.Core;
using San14.Data;

public sealed class D3D11CurrentSeasonLight
{
    private readonly D3D11CurrentSeasonResources.Light source;
    private readonly ReadOnlyMemory<byte> cameraPrefix;

    internal D3D11CurrentSeasonLight(D3D11CurrentSeasonResources.Light source,
        DirectionalLightCurrentCamera camera, ReadOnlyMemory<byte> prefix)
    {
        this.source = source; cameraPrefix = prefix;
        ObjectId = source.ObjectId; GroupMask = source.GroupMask;
        State = source.CurrentSource.PrepareCamera(camera);
    }
    public uint ObjectId { get; }
    public uint SelectedObjectId => source.SelectedConstruction.SourceObjectId;
    public uint GroupMask { get; }
    public CurrentDirectionalLightState State { get; }
    public HdrLightState Lighting => State.Lighting;
    public LightExposureSettings Exposure => State.Exposure;
    public ShadowReceiverBias ReceiverBias => State.ReceiverBias;
    public PatternLightModifier PatternModifier => State.PatternModifier;
    // The implemented root/root provider preserves these resource slots;
    // camera preparation changes numeric lighting, not their captured owners.
    internal IReadOnlyList<DirectionalLightResourceSlot> ResourceSlots => source.SelectedConstruction.ResourceSlots;
    internal void WriteNumericPrefix(Span<byte> destination) =>
        source.NumericEncoding.Write(State, cameraPrefix.Span, destination);
}

/// <summary>Same-request normal Main season/RGB/weather/local-light writes.
/// Geometry retains its preceding camera; post/Sky/lighting consume this
/// prepared prefix. This packet neither creates GPU owners nor advances World.</summary>
public sealed class D3D11CurrentSeasonFrame
{
    internal D3D11CurrentSeasonFrame(MainMapCameraModePacket mode,
        MapSeasonCalendarFrame calendar, SkySeasonMainCameraRgbFrame rgb,
        MapSourceCreatedSeasonLightingFrame present,
        D3D11PreparedMainWord[] words, D3D11CurrentSeasonLight[] lights,
        D3D11CurrentAtmosphereFrame atmosphere,
        D3D11CurrentExposureLight exposureLight, CameraOpticsSettings optics, CameraViewSettings viewSettings,
        OceanWaveSettings oceanWaves, OceanSurfaceSettings oceanSurface, OceanOpticsSettings oceanOptics,
        OceanTextureMaterialSettings oceanTextureMaterial, HeightFogSettings heightFog,
        CommonRenderPolicy commonPolicy, OceanBubbleMaterialSettings oceanBubbleMaterial, SkyStarSettings stars,
        PatternCameraSettings patternSettings, RainDropCameraSettings rainSettings)
    {
        Mode = mode; Calendar = calendar; Rgb = rgb; Present = present;
        PreparedMain = present.PreparedCameraPrefix; PreparedWords = Array.AsReadOnly(words);
        Lights = Array.AsReadOnly(lights); Atmosphere = atmosphere; ExposureLight = exposureLight;
        Optics = optics; ViewSettings = viewSettings; OceanWaves = oceanWaves; OceanSurface = oceanSurface;
        OceanOptics = oceanOptics; OceanTextureMaterial = oceanTextureMaterial;
        HeightFog = heightFog;
        CommonPolicy = commonPolicy;
        OceanBubbleMaterial = oceanBubbleMaterial;
        Stars = stars;
        PatternSettings = patternSettings;
        RainSettings = rainSettings;
    }
    internal D3D11CurrentPostOpticsSnapshot? PostOptics {get;set;}
    public MainMapCameraModePacket Mode { get; }
    public MapSeasonCalendarFrame Calendar { get; }
    public NativeMapSeasonState WorldSeason => Calendar is MapSeasonRuntimeFrame live ? live.State
        : throw new InvalidOperationException("A scenario-preview frame has no owned strategy season state.");
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
    public RainDropCameraSettings RainSettings { get; }
    public RainDropProviderSource? RainProvider { get; private set; }
    internal void BindRainProvider(RainDropProviderSource provider) => RainProvider = provider;
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
    private MapGameLightQualityControlOwner _gameLightControls=null!;
    private D3D11CurrentSeasonResources.Light[] _gameLights=[];
    private readonly MainMapCameraModeOwner _mode;
    private readonly MapMainCameraPostSelectionOwner _post;
    private readonly MapGameLightQualityOwner _quality;
    private readonly MapSeasonMainCameraPhaseSource _phase;
    private readonly SkySceneControllerSourceProfile _skyController;
    private readonly SkySceneSky2Parameters _sky;
    private readonly LoadedSkyDirectionalSetterColorNativeSource _color;
    private AcceptedSkyLightingState? _previousLighting;
    private D3D11CurrentPostOpticsPublisher? _postOptics;
    internal void BindCurrentPost(D3D11NormalPostInitialConstants initial,D3D11CurrentSeasonFrame opening)
    {_postOptics=new(initial);opening.PostOptics=_postOptics.Prepare(opening);}
    private D3D11CurrentSeasonFrame? _lastPreparedFrame;
    private D3D11CurrentSeasonFrame? _lastAcceptedFrame;
    private readonly D3D11CurrentSeasonLibrary? _library;

    public D3D11CurrentSeasonPublisher(GameResourceRepository repository,
        D3D11CurrentSeasonResources resources,
        MainMapCameraModeOwner? actualMainMode = null,
        MapGameLightQualityOwner? actualQuality = null,
        D3D11CurrentSeasonLibrary? resourceLibrary = null)
    {
        _resources = resources;
        ResetGameLightControls(resources);
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

    public void ApplyCgQuality(CgQuality quality)
    {
        if (!Enum.IsDefined(quality)) throw new ArgumentOutOfRangeException(nameof(quality));
        _quality.SetOwnedCurrentQuality((int)quality,"application-current-CG-quality");
    }

    public MapSeasonSelection LoadedSelection => _resources.Selection;
    public D3D11CurrentSeasonResources LoadedResources => _resources;

    /// <summary>Caller installs the already loaded generation while the render
    /// pump is drained. Main/App, previous RGB and native history stay alive.</summary>
    public void Install(D3D11CurrentSeasonResources resources)
    {
        if (resources.Source.Locale != _resources.Source.Locale)
            throw new InvalidDataException("A live Main season cannot switch executable locale.");
        if(!ReferenceEquals(_resources,resources))ResetGameLightControls(resources);
        _resources = resources;
    }

    private void ResetGameLightControls(D3D11CurrentSeasonResources resources)
    {
        _gameLightControls=new(resources.Lights.Select(light=>light.DefaultCaller.Root).ToArray());
        _gameLights=resources.Lights.ToArray();
    }

    /// <summary>Called only after successful Start and a drained native pump.
    /// The last actual accepted frame proves this publisher's retained RGB;
    /// only its controller-focus identity crosses the explicit role boundary.</summary>
    public void AdoptStartedCampaignCalendar(NativeMapSeasonState actualNativeState,
        MainMapCameraFrame actualMain,D3D11CurrentSeasonFrame lastAcceptedSeasonFrame)
    {
        ArgumentNullException.ThrowIfNull(actualMain);ArgumentNullException.ThrowIfNull(lastAcceptedSeasonFrame);
        var calendar=MapSeasonRuntime.PrepareFrame(actualNativeState,actualMain.Camera.FrameNumber);
        if(calendar.Selection!=_resources.Selection)
            throw new NotSupportedException("Started calendar does not match the current loaded preview season.");
        if(!ReferenceEquals(_lastAcceptedFrame,lastAcceptedSeasonFrame)||
            !ReferenceEquals(_lastPreparedFrame,lastAcceptedSeasonFrame)||_previousLighting is not {} retained||
            retained.FrameNumber!=lastAcceptedSeasonFrame.Calendar.FrameNumber||
            !ReferenceEquals(retained.Lighting,lastAcceptedSeasonFrame.Rgb.SunMoonRgb.Lighting)||
            !ReferenceEquals(lastAcceptedSeasonFrame.Rgb.Inputs.Source.Season.Resources,
                _resources.Source.CameraSource.Season.Resources))
            throw new InvalidDataException("Calendar adoption requires this publisher's last actual prepared and accepted season frame.");
        if(retained.FrameNumber>=actualMain.Camera.FrameNumber||
            lastAcceptedSeasonFrame.Rgb.Inputs.MainFrame.Camera.ExecutableLocale!=actualMain.Camera.ExecutableLocale||
            actualMain.Camera.ExecutableLocale!=_resources.Source.Locale||
            retained.CameraResourceUri!=actualMain.Camera.Source.Resource.Source.ResourceUri||
            retained.CameraResourceSha256!=actualMain.Camera.Source.Resource.Source.DecodedSha256)
            throw new InvalidDataException("Calendar adoption requires the same Camera resource and locale with a later actual Main frame.");
        if(_library is not null)
        {
            if(!ReferenceEquals(_library.RequireLoaded(_resources.Selection.SeasonIndex).Resources,_resources))
                throw new InvalidDataException("Publisher and library must share the exact preview CPU resource owner.");
            Install(_library.AdoptStartedCampaignCalendar(actualNativeState,actualMain).Resources);
        }
        else _resources.AdoptStartedCampaignCalendar(actualNativeState,actualMain);
        _previousLighting=retained with{InitialFocus=actualMain.InitialFocus};
    }

    public D3D11CurrentSeasonFrame PrepareCurrent(GameState world,
        MainMapCameraFrame currentMain, float elapsedSeconds)
        => PrepareCurrent(world.NativeMapSeason ?? throw new NotSupportedException(
            "Current World has no owned third-date season; Core.Date cannot supply it."), currentMain, elapsedSeconds);

    public D3D11CurrentSeasonFrame PrepareCurrent(NativeMapSeasonState season,
        MainMapCameraFrame currentMain, float elapsedSeconds)
    {
        var calendar = MapSeasonRuntime.PrepareFrame(season, currentMain.Camera.FrameNumber);
        if (calendar.Selection != _resources.Selection && _library is not null)
            Install(_library.Resolve(season, currentMain).Resources);
        return PrepareCalendar(calendar, currentMain, elapsedSeconds);
    }

    public D3D11CurrentSeasonFrame PrepareScenarioPreview(MapSeasonScenarioPreviewFrame preview,
        MainMapCameraFrame currentMain, float elapsedSeconds)
    {
        var calendar = MapSeasonRuntime.PrepareScenarioPreviewFrame(preview, currentMain.Camera.FrameNumber);
        if (calendar.Selection != _resources.Selection && _library is not null)
            Install(_library.ResolveScenarioPreview(calendar, currentMain).Resources);
        return PrepareCalendar(calendar, currentMain, elapsedSeconds);
    }

    private D3D11CurrentSeasonFrame PrepareCalendar(MapSeasonCalendarFrame calendar,
        MainMapCameraFrame currentMain, float elapsedSeconds)
    {
        var mode = _mode.PrepareCurrent(currentMain);
        var selected = _resources.CameraModes.Prepare(calendar,mode);
        var settings = _resources.ModeSettings[mode.Mode288];
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
        var viewSettings = CameraViewSettingsEncoding.ApplyPreparedOverrides(settings.View, actual.Scalars);
        var surface = settings.Surface with
        { CutoffDistance = actual.Scalars.QualityPatch.OceanCutoffDistance ?? settings.Surface.CutoffDistance };
        var oceanOptics = settings.Optics with
        { RefractionIndex = actual.Scalars.QualityPatch.OceanRefractionIndex ?? settings.Optics.RefractionIndex };
        var heightFog = HeightFogSettingsEncoding.ApplyQualityPatch(settings.Fog, actual.Scalars.QualityPatch);
        var words = D3D11CurrentSeasonEncoding.MainWrites(actual, settings.View, settings.Waves, surface, oceanOptics,
            settings.Cloud);
        var prefix = present.PreparedCameraPrefix;
        var lightCamera = DirectionalLightCurrentCamera.Capture(prefix.Span);
        _gameLightControls.ApplyCurrent(_quality.CurrentQualityOption18,mode.Mode288);
        for(int index=0;index<_gameLights.Length;index++)
        {
            var registered=_resources.Lights[index];
            var actor=_resources.SelectModeLight(index,selected.SuccessfulMainModeSelection!.Value);
            var controls=_gameLightControls.ReadCurrentControls(actor.SourceObjectId);
            if(ReferenceEquals(_gameLights[index].SelectedConstruction,actor)&&_gameLights[index].GameControls==controls)continue;
            _gameLights[index]=new(registered,actor,controls);
        }
        var lights = _gameLights.Select(light => new D3D11CurrentSeasonLight(
            light, lightCamera, prefix)).ToArray();
        var atmosphereLight = lights.First(light => (light.GroupMask & 0x80) != 0).State;
        var atmosphere = D3D11CurrentAtmosphereFrame.Prepare(present,
            selected.Transmittance.Tangent, _color,
            atmosphereLight.SunBaseline, atmosphereLight.MoonBaseline,
            settings.Stars.ExposureScale);
        var prepared = new D3D11CurrentSeasonFrame(mode, calendar, rgb, present,
            words, lights, atmosphere, D3D11CurrentExposureLight.Capture(_resources.FreshExposureSelector, lights),
            CameraOpticsEncoding.DecodePrefix(currentMain.Camera.Source, prefix.Span, actual.Scalars.ApplyOpticalTailTo(currentMain.Camera.Optics)), viewSettings, settings.Waves, surface, oceanOptics, settings.Texture, heightFog, _resources.CommonPolicy, settings.Bubble, settings.Stars, settings.Pattern, settings.Rain);
        prepared.PostOptics=_postOptics?.Prepare(prepared);
        _lastPreparedFrame=prepared;
        return prepared;
    }

    public void AcceptCurrent(D3D11CurrentSeasonFrame frame)
    {
        if(!ReferenceEquals(_lastPreparedFrame,frame))
            throw new InvalidDataException("Only this publisher's actual prepared season frame can be accepted.");
        _previousLighting=AcceptedSkyLightingState.Capture(frame.Rgb);
        _lastAcceptedFrame=frame;
        if(frame.PostOptics is {} post)_postOptics!.Accept(post);
    }
}
