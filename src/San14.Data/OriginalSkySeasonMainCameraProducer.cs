using System.Buffers.Binary;

namespace San14.Data;

/// <summary>An owned source season selection. World third-date selection,
/// pending SceneDatabase clock/weather seed and selected Camera Skylight
/// fields retain independent identities. No global renderer clock is advanced.</summary>
public sealed class OriginalSkySeasonMainCameraSource
{
    internal OriginalSkySeasonMainCameraSource(OriginalMapSeasonResolvedSourceFrame season,
        OriginalMapSeasonCameraSelection selection,OriginalMapSeasonSceneClockSeed seed,
        OriginalCameraLightingController controller,float kelvin,
        OriginalSkySeasonTransmittanceSource transmittance,bool sourceEmptyAxes,
        LoadedOriginalMapSeasonCameraPostEffectScalarSource postEffectScalarSource)
    {Season=season;Selection=selection;SceneClockSeed=seed;Controller=controller;
        SourceColorTemperature21C=kelvin;Transmittance=transmittance;UsesSourceCreatedEmptyAxes=sourceEmptyAxes;
        CameraPrefixScalars=OriginalMapSeasonCameraPrefixScalars.ReadDirectSelection(selection);
        PostEffectScalarSource=postEffectScalarSource;}
    private OriginalSkySeasonMainCameraSource(OriginalMapSeasonRuntimeFrame calendar,
        OriginalSkySeasonMainCameraSource loaded)
    {
        Season=new(calendar,loaded.Season.Resources);Selection=loaded.Selection;
        SceneClockSeed=loaded.SceneClockSeed;Controller=loaded.Controller;
        SourceColorTemperature21C=loaded.SourceColorTemperature21C;Transmittance=loaded.Transmittance;
        UsesSourceCreatedEmptyAxes=loaded.UsesSourceCreatedEmptyAxes;
        CameraPrefixScalars=loaded.CameraPrefixScalars;PostEffectScalarSource=loaded.PostEffectScalarSource;
    }
    /// <summary>Bind this request's calendar while retaining the parsed source
    /// package. A different season requires its actual resource generation.</summary>
    public OriginalSkySeasonMainCameraSource WithCalendar(OriginalMapSeasonRuntimeFrame calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        if(calendar.Selection!=Season.Resources.Selection)
            throw new NotSupportedException("A season transition requires a new Main Camera source package generation.");
        return new(calendar,this);
    }
    public OriginalMapSeasonResolvedSourceFrame Season{get;}
    public OriginalMapSeasonCameraSelection Selection{get;}
    public OriginalMapSeasonSceneClockSeed SceneClockSeed{get;}
    public OriginalCameraLightingController Controller{get;}
    public float SourceColorTemperature21C{get;}
    public OriginalSkySeasonTransmittanceSource Transmittance{get;}
    public bool UsesSourceCreatedEmptyAxes{get;}
    public OriginalMapSeasonCameraPrefixScalars CameraPrefixScalars{get;}
    public LoadedOriginalMapSeasonCameraPostEffectScalarSource PostEffectScalarSource{get;}
    public long FrameNumber=>Season.Calendar.FrameNumber;
    public string SourceProvenance=>Season.Calendar.SourceProvenance+";"+Selection.SourceEvidence+
        ";source-selected-V468-controller-fields;"+CameraPrefixScalars.SourceEvidence+
        ";source-Kelvin6500;"+Transmittance.SourceProvenance;
}

public sealed class OriginalSkySeasonMainCameraInputs
{
    private readonly byte[] prefix;
    internal OriginalSkySeasonMainCameraInputs(OriginalSkySeasonMainCameraSource source,
        OriginalMainMapCameraFrame main,OriginalMapSeasonMainCameraPhaseResult phase,byte[] prefix,float kelvin,
        OriginalMapMainCameraPostSelectionPacket? mainPostSelection)
    {Source=source;MainFrame=main with{Camera=main.Camera.CopyAcceptedSnapshot()};
        Phase=phase;this.prefix=prefix;CurrentColorTemperature21C=kelvin;MainPostSelection=mainPostSelection;}
    public OriginalSkySeasonMainCameraSource Source{get;}
    public OriginalMainMapCameraFrame MainFrame{get;}
    public OriginalMapSeasonMainCameraPhaseResult Phase{get;}
    public OriginalMapMainCameraPostSelectionPacket? MainPostSelection{get;}
    public ReadOnlyMemory<byte> BeforeFinalPreparationPrefix=>prefix;
    public float CurrentColorTemperature21C{get;}
    public long FrameNumber=>Source.FrameNumber;
}

public sealed record OriginalSkySeasonMainCameraRgbFrame(OriginalSkySeasonMainCameraInputs Inputs,
    OriginalPreparedCameraLightingPacket SunMoonRgb,
    OriginalSkySceneCurrentCameraParameters CurrentSkyCamera)
{
    public float? PreparationFrameDelta { get; internal init; }
    public OriginalNativeCameraTangentDispatch? PreparationDispatch { get; internal init; }
    public bool CompleteWeatherFogPrepared=>false;
    public string SourceProvenance=>Inputs.Source.SourceProvenance+
        ";Main-V468-source-package ->17BD0-post-phase ->present-table-SunMoon-RGB ->selected-View-D7-Sky-setters";
}

/// <summary>Callable owned World calendar→season resources→actual external
/// Camera package→same Main current zoom phase→present-table Sun/Moon→Sky
/// phase producer. Full weather/Fog and selected View light-record ownership
/// are separate producers, never inferred from this Camera's color rows.</summary>
public static class OriginalSkySeasonMainCameraProducer
{
    public static OriginalSkySeasonMainCameraSource ResolveOwnedSource(OriginalGameRepository repository,
        OriginalMapSeasonResolvedSourceFrame ownedSeason,string locale,
        OriginalMapSeasonCameraSelection? explicitlySelectedPackage=null)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(ownedSeason);
        var canonical=OriginalMapSeasonRuntime.PrepareFrame(ownedSeason.Calendar.State,ownedSeason.Calendar.FrameNumber);
        if(canonical!=ownedSeason.Calendar||ownedSeason.Resources.Selection!=canonical.Selection)
            throw new InvalidDataException("Sky season source requires its canonical owned World-third-date frame.");
        var provider=OriginalMapSeasonCameraApplication.ReadProvider(ownedSeason.Resources.Action);
        var seed=OriginalMapSeasonCameraApplication.ReadSourceSceneClockSeed(provider);
        // This factory creates the source selector with its source0/0 axes and
        // absent external selector. A later current selector is explicit.
        var selection=explicitlySelectedPackage??OriginalMapSeasonCameraApplication.SelectSourceEmptyAxes(
            provider,seed.Controls.SelectorScalar1A0,seed.Controls.SelectorScalar1A4);
        selection.Validate();
        if(selection.Provider.Database.Action.Selection!=canonical.Selection||
            selection.Provider.Database.Objects.Source.ResourceUri!=provider.Database.Objects.Source.ResourceUri||
            selection.Provider.Database.Objects.Source.DecodedSha256!=provider.Database.Objects.Source.DecodedSha256)
            throw new InvalidDataException("Current selected Camera package belongs to another owned source season generation.");
        if(selection.SecondPackage is not null)
            throw new NotSupportedException("The seven Skylight pair interpolation is available separately; this full source-controller producer requires its exact direct selected package until other controller blend instructions are closed.");
        var sampling=OriginalSkySeasonPackageTextures.Read(selection.FirstPackage);
        var controller=OriginalPreparedCameraLightingSource.ReadController(sampling.Package.Camera);
        float kelvin=OriginalSkySeasonMoonTemperature.ReadKelvin(selection);
        return new(ownedSeason,selection,seed,controller,kelvin,
            OriginalSkySeasonTransmittanceSource.Load(repository,selection.FirstPackage,locale),explicitlySelectedPackage is null,
            OriginalMapSeasonCameraPostEffectScalarSource.Load(repository,locale));
    }

    /// <summary>V468 source fields precede the same Main17BD0 owner phase.
    /// Current mode288 remains explicit. Primary ECb0 reads the proved embedded
    /// Main+50 controller230/234 fields of this immutable same-frame packet.
    /// A later Kelvin edit is explicit rather than silently reset to source.</summary>
    public static OriginalSkySeasonMainCameraInputs PrepareBeforeFinalPreparation(
        OriginalSkySeasonMainCameraSource source,OriginalMainMapCameraFrame currentMain,
        int currentOwnerMode288,OriginalMapSeasonMainCameraPhaseSource nativePhase,
        float? explicitlyEditedColorTemperature21C=null,
        OriginalSkySeasonMainCameraRgbFrame? previousAcceptedRgbFrame=null,
        OriginalMapMainCameraPostSelectionPacket? mainPostSelection=null)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(currentMain);
        ArgumentNullException.ThrowIfNull(nativePhase);
        if(source.FrameNumber!=currentMain.Camera.FrameNumber||source.FrameNumber<1||
            currentMain.Camera.Source.Camera.ObjectId!=OriginalMainMapCameraAdapter.MainCameraObjectId||
            currentMain.Camera.Source.Resource.Source.OverrideSource is not null||
            nativePhase.Locale!=currentMain.Camera.ExecutableLocale||
            source.Transmittance.Atan.Locale!=currentMain.Camera.ExecutableLocale)
            throw new InvalidDataException("Applied season Camera requires this same actual source Main38D frame and executable generation.");
        string owner=currentMain.Camera.FrameProvenance+";primary-Main+50-controller=Main230/234";
        float ecB0=OriginalMapSeasonMainCameraPhase.FromOwnerZoomFields(
            currentMain.ZoomSelection.Band1E0,currentMain.ZoomSelection.Fraction1E4);
        var phase=OriginalMapSeasonMainCameraPhase.ApplyAfterPackage(source.Selection.SkylightParameters,
            source.Controller,currentOwnerMode288,ecB0,nativePhase,owner);
        if(mainPostSelection is{}post)
        {
            post.ValidateSameMain(currentMain.Camera);
            post.ValidateSameSelection(source.Selection);
            if(post.MainMode288!=currentOwnerMode288)
                throw new InvalidDataException("Main post-selection and phase use different current modes.");
            if(post.TryReadPreparedWord(0x210,out uint altitude))
                phase=phase with{Parameters=phase.Parameters with{Altitude=BitConverter.UInt32BitsToSingle(altitude)}};
        }
        byte[] retained=source.CameraPrefixScalars.ApplyToPrefix(currentMain.Camera.Prefix.Span);
        if(previousAcceptedRgbFrame is not null)
        {
            var previous=previousAcceptedRgbFrame.Inputs.MainFrame;
            if(previous.Camera.FrameNumber>=currentMain.Camera.FrameNumber||
                previous.Camera.Source.Resource.Source.ResourceUri!=currentMain.Camera.Source.Resource.Source.ResourceUri||
                previous.Camera.Source.Resource.Source.DecodedSha256!=currentMain.Camera.Source.Resource.Source.DecodedSha256||
                previous.InitialFocus!=currentMain.InitialFocus)
                throw new InvalidDataException("Retained source Camera RGB transition belongs to another or nonpreceding Main owner frame.");
            // The current pose/lens/package and shared Fog keep their own
            // writers. Only this producer's accepted RGB/transition storage
            // follows the native embedded Camera across subsequent frames.
            previousAcceptedRgbFrame.SunMoonRgb.Prefix.Span.Slice(0x2AC,0x88).CopyTo(retained.AsSpan(0x2AC,0x88));
            previousAcceptedRgbFrame.SunMoonRgb.Prefix.Span.Slice(0x3B0,0x10).CopyTo(retained.AsSpan(0x3B0,0x10));
        }
        byte[] prefix=OriginalPreparedCameraLightingSource.ApplyResolvedInputsToPrefix(phase.LightingController,
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
        return new(source,currentMain,phase,prefix,kelvin,mainPostSelection);
    }

    public static OriginalSkySeasonMainCameraRgbFrame PrepareSunMoonAndSky(
        OriginalSkySeasonMainCameraInputs input,float frameDelta,OriginalNativeCameraTangentDispatch dispatch,
        OriginalSkySceneControllerSourceProfile completeRetainedSkyController,
        OriginalSkySceneSky2Parameters completeRetainedSky2)
    {
        ArgumentNullException.ThrowIfNull(input);
        var rgb=OriginalSkySeasonPreparedCameraRgb.PrepareWithSourceTransmittanceRgb(input.Phase.LightingController,
            input.Phase.Parameters,input.BeforeFinalPreparationPrefix.Span,frameDelta,
            input.Source.Transmittance,dispatch,input.CurrentColorTemperature21C);
        var current=OriginalSkySceneCurrentCameraProducer.PrepareSelectedView(input.FrameNumber,rgb.Prefix.Span,
            input.MainFrame.Camera.FrameProvenance+";"+input.Phase.SourceProvenance+
                ";present-source-table-SunMoon-RGB-only;Fog/weather-separate-producer",
            completeRetainedSkyController,completeRetainedSky2);
        return new(input,rgb,current) { PreparationFrameDelta=frameDelta, PreparationDispatch=dispatch };
    }
}
