using System.Buffers.Binary;
using San14.Core;

namespace San14.Data;

/// <summary>An owned source season selection. World third-date selection,
/// pending SceneDatabase clock/weather seed and selected Camera Skylight
/// fields retain independent identities. Its tables and Cloud images are imported
/// once per explicit resource load and shared by calendar updates.</summary>
public sealed class SkySeasonMainCameraSource
{
    private readonly Lazy<SkyCloudWeatherSettings> cloudWeatherSettings;
    internal SkySeasonMainCameraSource(GameResourceRepository repository,MapSeasonResolvedSourceFrame season,
        MapSeasonCameraSelection selection,MapSeasonSceneClockSeed seed,
        CameraLightingController controller,float kelvin,
        SkySeasonTransmittanceSource transmittance,bool sourceEmptyAxes,
        LoadedMapSeasonCameraPostEffectScalarSource postEffectScalarSource)
    {Season=season;Selection=selection;SceneClockSeed=seed;Controller=controller;
        SourceColorTemperature21C=kelvin;Transmittance=transmittance;UsesSourceCreatedEmptyAxes=sourceEmptyAxes;
        CameraPrefixScalars=MapSeasonCameraPrefixScalars.ReadDirectSelection(selection);
        PostEffectScalarSource=postEffectScalarSource;
        uint fallback=SkySceneSourceDefaults.ReadPerspectiveCamera(selection.FirstPackage.Objects,
            selection.FirstPackage.Camera.ObjectId).References.PlaneSta;
        RenderingAtmosphere=transmittance.Tables ?? (fallback==0 ? null : SkyAtmoTableResources.Load(repository,fallback));
        AtmosphereScattering=SkyAtmosphereNativeSource.Load(repository,transmittance.Atan.Locale,NativeLogDispatch.Sse);
        CloudTextures=SkyAppliedSeasonCloudTextures.Read(repository,this);
        cloudWeatherSettings=new(() => SkyCloudWeatherSettingsImport.Import(selection.FirstPackage));}
    private SkySeasonMainCameraSource(MapSeasonCalendarFrame calendar,
        SkySeasonMainCameraSource loaded)
    {
        Season=new(calendar,loaded.Season.Resources);Selection=loaded.Selection;
        SceneClockSeed=loaded.SceneClockSeed;Controller=loaded.Controller;
        SourceColorTemperature21C=loaded.SourceColorTemperature21C;Transmittance=loaded.Transmittance;
        UsesSourceCreatedEmptyAxes=loaded.UsesSourceCreatedEmptyAxes;
        SuccessfulMainModeSelection=loaded.SuccessfulMainModeSelection;
        CameraPrefixScalars=loaded.CameraPrefixScalars;PostEffectScalarSource=loaded.PostEffectScalarSource;
        CloudTextures=loaded.CloudTextures;
        RenderingAtmosphere=loaded.RenderingAtmosphere;
        AtmosphereScattering=loaded.AtmosphereScattering;
        cloudWeatherSettings=loaded.cloudWeatherSettings;
    }
    /// <summary>Bind this request's calendar while retaining the parsed source
    /// package. A different season requires its actual resource generation.</summary>
    public SkySeasonMainCameraSource WithCalendar(MapSeasonCalendarFrame calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);MapSeasonRuntime.ValidateFrame(calendar);
        if(calendar.GetType()!=Season.Calendar.GetType())
            throw new InvalidDataException("Live and scenario-preview calendars require separate Main Camera source owners.");
        if(calendar.Selection!=Season.Resources.Selection)
            throw new NotSupportedException("A season transition requires a new Main Camera source package generation.");
        return new(calendar,this);
    }
    /// <summary>Explicit preview-to-started-campaign ownership transfer. The
    /// caller has drained old requests and supplies the real started calendar
    /// and next Main capture. Parsed resources and scene seeds survive.</summary>
    public SkySeasonMainCameraSource AdoptStartedCampaignCalendar(
        NativeMapSeasonState actualNativeState,MainMapCameraFrame actualMain)
    {
        ArgumentNullException.ThrowIfNull(actualMain);
        var calendar=MapSeasonRuntime.PrepareFrame(actualNativeState,actualMain.Camera.FrameNumber);
        if(Season.Calendar is not MapSeasonScenarioPreviewFrame preview)
            throw new InvalidOperationException("Calendar adoption requires an actual scenario-preview source owner.");
        MapSeasonRuntime.ValidateFrame(preview);
        if(actualMain.Camera.Source.Camera.ObjectId!=MainMapCameraAdapter.MainCameraObjectId||
            actualMain.Camera.ExecutableLocale!=Transmittance.Atan.Locale)
            throw new InvalidDataException("Calendar adoption requires this source locale and its actual Main Camera capture.");
        if(actualNativeState.SourceScenarioUri!=preview.Source.Source.ResourceUri||
            !actualNativeState.SourceScenarioSha256.Equals(preview.Source.Source.Sha256,StringComparison.OrdinalIgnoreCase)||
            !actualNativeState.SourceWorldSha256.Equals(preview.Source.Document.WorldSha256,StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Started campaign and preview belong to different scenario or World resource generations.");
        if(calendar.Selection!=Season.Resources.Selection)
            throw new NotSupportedException("Calendar adoption requires the actually loaded preview season; a different season needs its real resource installation.");
        return new(calendar,this);
    }
    public MapSeasonResolvedSourceFrame Season{get;}
    public MapSeasonCameraSelection Selection{get;}
    public MapSeasonSceneClockSeed SceneClockSeed{get;}
    public CameraLightingController Controller{get;}
    public float SourceColorTemperature21C{get;}
    public SkySeasonTransmittanceSource Transmittance{get;}
    /// <summary>The actual rendering provider prefers the primary atmosphere,
    /// then the independently declared Plane provider. It does not change the
    /// primary-table presence used by seasonal lighting or Pattern.</summary>
    public LoadedSkyAtmoTableSources? RenderingAtmosphere{get;}
    public LoadedSkyAtmosphereNativeSource AtmosphereScattering{get;}
    public LoadedSkyAtmoTableSources RequireRenderingAtmosphere() => RenderingAtmosphere ??
        throw new NotSupportedException("This rendering operation has no selected atmosphere provider.");
    public SkyAppliedSeasonCloudTextureSources CloudTextures{get;}
    public bool UsesSourceCreatedEmptyAxes{get;}
    /// <summary>Only the successful ordinary Main V468 direct application
    /// publishes these late controls. Invocation15C8 is not an external selector.</summary>
    public int? SuccessfulMainModeSelection { get; private init; }
    public bool UsesSourceDatabaseReset => UsesSourceCreatedEmptyAxes || SuccessfulMainModeSelection.HasValue;
    internal SkySeasonMainCameraSource ApplySuccessfulMainMode(MapSeasonCalendarFrame calendar,
        MainMapCameraModePacket mode)
    {
        MapSeasonRuntime.ValidateFrame(calendar);
        if(calendar.GetType()!=Season.Calendar.GetType())
            throw new InvalidDataException("Adopt the actual started calendar before preparing its Main mode packages.");
        if(mode.FrameNumber!=calendar.FrameNumber||mode.AcceptedMainFrame.Camera.ExecutableLocale!=Transmittance.Atan.Locale||
            mode.Mode288!=Selection.FirstIndex||Selection.SecondPackage is not null||
            Selection.Provider.ExternalSelectorObjectId!=0)
            throw new InvalidDataException("Successful Main application requires its actual mode, direct package and absent external selector.");
        return new(calendar,this){SuccessfulMainModeSelection=mode.Mode288};
    }
    public MapSeasonCameraPrefixScalars CameraPrefixScalars{get;}
    public WeatherFogProfile WeatherProfile => WeatherFogProfileImport.FromPackage(Selection.FirstPackage);
    /// <summary>Cloud declarations belong to this loaded source, independently
    /// of calendar changes. Lighting-only consumers need not prepare Cloud.</summary>
    public SkyCloudWeatherSettings CloudWeatherSettings => cloudWeatherSettings.Value;
    public LoadedMapSeasonCameraPostEffectScalarSource PostEffectScalarSource{get;}
    public long FrameNumber=>Season.Calendar.FrameNumber;
    public string SourceProvenance=>Season.Calendar.SourceProvenance+";"+Selection.SourceEvidence+
        ";source-selected-V468-controller-fields;"+CameraPrefixScalars.SourceEvidence+
        ";source-Kelvin6500;"+Transmittance.SourceProvenance;
}

public sealed class SkySeasonMainCameraInputs
{
    private readonly byte[] prefix;
    internal SkySeasonMainCameraInputs(SkySeasonMainCameraSource source,
        MainMapCameraFrame main,MapSeasonMainCameraPhaseResult phase,byte[] prefix,float kelvin,
        MapMainCameraPostSelectionPacket? mainPostSelection, SkyLightingState lighting, SkyWeatherFrameInputs weather)
    {Weather=weather;Lighting=lighting;Source=source;MainFrame=main with{Camera=main.Camera.CopyAcceptedSnapshot()};
        Phase=phase;this.prefix=prefix;CurrentColorTemperature21C=kelvin;MainPostSelection=mainPostSelection;}
    public SkyWeatherFrameInputs Weather { get; }
    public SkyLightingState Lighting { get; }
    public SkySeasonMainCameraSource Source{get;}
    public MainMapCameraFrame MainFrame{get;}
    public MapSeasonMainCameraPhaseResult Phase{get;}
    public MapMainCameraPostSelectionPacket? MainPostSelection{get;}
    public ReadOnlyMemory<byte> BeforeFinalPreparationPrefix=>prefix;
    public float CurrentColorTemperature21C{get;}
    public long FrameNumber=>Source.FrameNumber;
}

public sealed record SkySeasonMainCameraRgbFrame(SkySeasonMainCameraInputs Inputs,
    PreparedCameraLightingPacket SunMoonRgb,
    SkySceneCurrentCameraParameters CurrentSkyCamera)
{
    public float? PreparationFrameDelta { get; internal init; }
    public NativeCameraTangentDispatch? PreparationDispatch { get; internal init; }
    public bool CompleteWeatherFogPrepared=>false;
    public string SourceProvenance=>Inputs.Source.SourceProvenance+
        ";Main-V468-source-package ->17BD0-post-phase ->resource-selected-SunMoon-RGB ->selected-View-D7-Sky-setters";
}

/// <summary>Callable owned World calendar→season resources→actual external
/// Camera package→same Main current zoom phase→present-table Sun/Moon→Sky
/// phase producer. Full weather/Fog and selected View light-record ownership
/// are separate producers, never inferred from this Camera's color rows.</summary>
public static class SkySeasonMainCameraProducer
{
    public static SkySeasonMainCameraSource ResolveOwnedSource(GameResourceRepository repository,
        MapSeasonResolvedSourceFrame ownedSeason,string locale,
        MapSeasonCameraSelection? explicitlySelectedPackage=null)
    {
        ArgumentNullException.ThrowIfNull(ownedSeason);
        if(ownedSeason.Calendar is not MapSeasonRuntimeFrame)
            throw new InvalidDataException("Owned Sky season source requires an actual owned World-third-date frame.");
        return ResolveSource(repository,ownedSeason,locale,explicitlySelectedPackage);
    }

    public static SkySeasonMainCameraSource ResolveScenarioPreviewSource(GameResourceRepository repository,
        MapSeasonResolvedSourceFrame previewSeason,string locale,
        MapSeasonCameraSelection? explicitlySelectedPackage=null)
    {
        ArgumentNullException.ThrowIfNull(previewSeason);
        if(previewSeason.Calendar is not MapSeasonScenarioPreviewFrame)
            throw new InvalidDataException("Preview Sky season source requires an actual scenario resource third date.");
        return ResolveSource(repository,previewSeason,locale,explicitlySelectedPackage);
    }

    private static SkySeasonMainCameraSource ResolveSource(GameResourceRepository repository,
        MapSeasonResolvedSourceFrame season,string locale,
        MapSeasonCameraSelection? explicitlySelectedPackage)
    {
        ArgumentNullException.ThrowIfNull(repository);MapSeasonRuntime.ValidateFrame(season.Calendar);
        if(season.Resources.Selection!=season.Calendar.Selection)
            throw new InvalidDataException("Sky calendar and its resolved source season keys disagree.");
        var provider=MapSeasonCameraApplication.ReadProvider(season.Resources.Action);
        var seed=MapSeasonCameraApplication.ReadSourceSceneClockSeed(provider);
        // This factory creates the source selector with its source0/0 axes and
        // absent external selector. A later current selector is explicit.
        var selection=explicitlySelectedPackage??MapSeasonCameraApplication.SelectSourceEmptyAxes(
            provider,seed.Controls.SelectorScalar1A0,seed.Controls.SelectorScalar1A4);
        selection.Validate();
        if(selection.Provider.Database.Action.Selection!=season.Calendar.Selection||
            selection.Provider.Database.Objects.Source.ResourceUri!=provider.Database.Objects.Source.ResourceUri||
            selection.Provider.Database.Objects.Source.DecodedSha256!=provider.Database.Objects.Source.DecodedSha256)
            throw new InvalidDataException("Current selected Camera package belongs to another owned source season generation.");
        if(selection.SecondPackage is not null)
            throw new NotSupportedException("The seven Skylight pair interpolation is available separately; this full source-controller producer requires its exact direct selected package until other controller blend instructions are closed.");
        var controller=PreparedCameraLightingSource.ReadController(selection.FirstPackage.Camera);
        float kelvin=SkySeasonMoonTemperature.ReadKelvin(selection);
        return new(repository,season,selection,seed,controller,kelvin,
            SkySeasonTransmittanceSource.Load(repository,selection.FirstPackage,locale),explicitlySelectedPackage is null,
            MapSeasonCameraPostEffectScalarSource.Load(repository,locale));
    }

    /// <summary>V468 source fields precede the same Main17BD0 owner phase.
    /// Current mode288 remains explicit. Primary ECb0 reads the proved embedded
    /// Main+50 controller230/234 fields of this immutable same-frame packet.
    /// A later Kelvin edit is explicit rather than silently reset to source.</summary>
    public static SkySeasonMainCameraInputs PrepareBeforeFinalPreparation(
        SkySeasonMainCameraSource source,MainMapCameraFrame currentMain,
        int currentOwnerMode288,MapSeasonMainCameraPhaseSource nativePhase,
        float? explicitlyEditedColorTemperature21C=null,
        AcceptedSkyLightingState? previousAcceptedLightingState=null,
        MapMainCameraPostSelectionPacket? mainPostSelection=null)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(currentMain);
        ArgumentNullException.ThrowIfNull(nativePhase);
        if(source.FrameNumber!=currentMain.Camera.FrameNumber||source.FrameNumber<1||
            currentMain.Camera.Source.Camera.ObjectId!=MainMapCameraAdapter.MainCameraObjectId||
            nativePhase.Locale!=currentMain.Camera.ExecutableLocale||
            source.Transmittance.Atan.Locale!=currentMain.Camera.ExecutableLocale)
            throw new InvalidDataException("Applied season Camera requires this same actual source Main38D frame and executable generation.");
        string owner=currentMain.Camera.FrameProvenance+";primary-Main+50-controller=Main230/234";
        float ecB0=MapSeasonMainCameraPhase.FromOwnerZoomFields(
            currentMain.ZoomSelection.Band1E0,currentMain.ZoomSelection.Fraction1E4);
        var phase=MapSeasonMainCameraPhase.ApplyAfterPackage(source.Selection.SkylightParameters,
            source.Controller,currentOwnerMode288,ecB0,nativePhase,owner);
        if(mainPostSelection is{}post)
        {
            post.ValidateSameMain(currentMain.Camera);
            post.ValidateSameSelection(source.Selection);
            if(post.MainMode288!=currentOwnerMode288)
                throw new InvalidDataException("Main post-selection and phase use different current modes.");
            if(post.QualityPatch.AltitudeOffset is { } altitude)
                phase=phase with{Parameters=phase.Parameters with{Altitude=altitude}};
        }
        byte[] retained=source.CameraPrefixScalars.ApplyToPrefix(currentMain.Camera.Prefix.Span);
        SkyLightingState lighting;
        if(previousAcceptedLightingState is not null)
        {
            previousAcceptedLightingState.RequirePreceding(currentMain);
            lighting=previousAcceptedLightingState.Lighting;
            SkyLightingEncoding.Write(retained,lighting);
        }
        else lighting=SkyLightingEncoding.Decode(retained);
        byte[] prefix=PreparedCameraLightingSource.ApplyResolvedInputsToPrefix(phase.LightingController,
            phase.Parameters,retained);
        // Main17E19 / conditional17E11: adaptation and Cloudy, not clock fields.
        BinaryPrimitives.WriteUInt32LittleEndian(prefix.AsSpan(0x1C4,4),0);
        if(currentOwnerMode288>=3)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(prefix.AsSpan(0x1C8,4),0x3DCCCCCD);
            BinaryPrimitives.WriteUInt32LittleEndian(prefix.AsSpan(0x1D0,4),0);
        }
        if(mainPostSelection is not null)prefix=mainPostSelection.ApplyToPrefix(prefix);
        float kelvin=explicitlyEditedColorTemperature21C??source.SourceColorTemperature21C;
        // Original property939/type4 is an integer Kelvin word. C3F880
        // loads EDX directly from21C before native1147EE0's uint/100 index.
        // The optional public numeric input must have an exact integer value;
        // a float6500 bit pattern cannot stand in for uint6500 storage.
        if(!float.IsFinite(kelvin)||kelvin<0||(double)kelvin>uint.MaxValue||MathF.Truncate(kelvin)!=kelvin)
            throw new InvalidDataException("Current Camera color temperature must be finite integral uint32 Kelvin.");
        BinaryPrimitives.WriteUInt32LittleEndian(prefix.AsSpan(0x21C,4),checked((uint)kelvin));
        var climate=source.CameraPrefixScalars.Climate;
        float cloudiness=currentOwnerMode288>=3?0:climate.Cloudiness;
        if(mainPostSelection?.QualityPatch.Cloudiness is { } finalCloudiness) cloudiness=finalCloudiness;
        var weather=new SkyWeatherFrameInputs(climate with{Cloudiness=cloudiness},
            currentMain.Camera.Geometry.Eye.Y,phase.Parameters.Altitude);
        return new(source,currentMain,phase,prefix,kelvin,mainPostSelection,lighting,weather);
    }

    public static SkySeasonMainCameraRgbFrame PrepareSunMoonAndSky(
        SkySeasonMainCameraInputs input,float frameDelta,NativeCameraTangentDispatch dispatch,
        SkySceneControllerSourceProfile completeRetainedSkyController,
        SkySceneSky2Parameters completeRetainedSky2)
    {
        ArgumentNullException.ThrowIfNull(input);
        var lighting=SkySeasonPreparedCameraRgb.PrepareLighting(input.Phase.LightingController,
            input.Phase.Parameters,input.Lighting,frameDelta,
            input.Source.Transmittance,dispatch,input.CurrentColorTemperature21C);
        var rgb=new PreparedCameraLightingPacket(input.BeforeFinalPreparationPrefix.Span,lighting,
            clearAbsentTableFog:input.Source.Transmittance.Tables is null);
        var current=SkySceneCurrentCameraProducer.PrepareSelectedView(input.FrameNumber,rgb.Prefix.Span,
            input.MainFrame.Camera.FrameProvenance+";"+input.Phase.SourceProvenance+
                ";resource-selected-SunMoon-RGB;Fog/weather-separate-producer",
            completeRetainedSkyController,completeRetainedSky2);
        return new(input,rgb,current) { PreparationFrameDelta=frameDelta, PreparationDispatch=dispatch };
    }
}
