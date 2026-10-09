using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Owned successful season table/RGB + actual Main weather72 →
/// current prepared3C0 Camera and clock tail. This source capability cannot be
/// obtained from a nonzero declaration key, an observed prefix or an absent
/// dependency. It remains distinct from the class66 complete LightData owner.</summary>
public sealed class MapSourceCreatedSeasonLightingFrame
{
    private readonly byte[] prior, current;
    private MapSourceCreatedSeasonLightingFrame(SkySeasonMainCameraRgbFrame camera,
        SkySeasonMainWeatherFogFrame weather, float delta)
    {
        AcceptedSeasonCamera=camera; AcceptedWeather=weather; FrameDelta=delta;
        prior=camera.Inputs.BeforeFinalPreparationPrefix.ToArray();
        Clock=SkylightClock.Advance(Parameters,Controller.AutoDateTimeStepType,Controller.TimeSpanPerDay,delta);
        current=SkylightClock.ApplyToPreparedPrefix(weather.ApplyKnownWritesToCurrentPreparedPrefix(camera),Clock);
        SourceProvenance=weather.SourceProvenance+
            ";owned-successful-season-table-runtime=Present;Main-prepared3C0;actual-clock-tail;"+
            "selected-LightData-owner-independent;Main72-owned-by-Camera";
        CurrentSkyCamera=SkySceneCurrentCameraProducer.PrepareSelectedView(FrameNumber,current,
            SourceProvenance,SkySceneSourceDefaults.ApplyPreparedMainControllerInputs(
                camera.CurrentSkyCamera.Controller,current),camera.CurrentSkyCamera.Parameters);
    }
    public SkySeasonMainCameraRgbFrame AcceptedSeasonCamera { get; }
    public SkySeasonMainWeatherFogFrame AcceptedWeather { get; }
    public long FrameNumber=>AcceptedSeasonCamera.Inputs.FrameNumber;
    public float FrameDelta { get; }
    public CameraLightingController Controller=>AcceptedSeasonCamera.Inputs.Phase.LightingController;
    public SkylightParameters Parameters=>AcceptedSeasonCamera.Inputs.Phase.Parameters;
    public SkylightClockResult Clock { get; }
    public SkySceneCurrentCameraParameters CurrentSkyCamera { get; }
    public ReadOnlyMemory<byte> PriorPreparedCameraPrefix=>prior.ToArray();
    public ReadOnlyMemory<byte> PreparedCameraPrefix=>current.ToArray();
    public string SourceProvenance { get; }
    public MapLightingRuntimeTable RuntimeTable=>MapLightingRuntimeTable.Present;

    /// <summary>The Camera after this same frame's completed owned seasonal
    /// prefix and post-scalar writers. Geometry still owns its earlier stage.</summary>
    public MapCameraConstructionPacket CapturePreparedCamera()
    {
        var inputs=AcceptedSeasonCamera.Inputs;
        var main=inputs.MainFrame.Camera;
        var optics=inputs.Source.PostEffectScalarSource.CaptureActualMainPreparation(inputs,main);
        return new(current.ToArray(),main.Source,main.CurrentPose,main.Lens,main.Matrices,
            main.ExecutableLocale,FrameNumber,
            main.FrameProvenance+";"+SourceProvenance+";owned-current-season-post-Camera",
            optics.Scalars,optics);
    }

    /// <summary>The two actual source producers must share the same accepted
    /// Main frame. Their private successful raw table owner supplies Present;
    /// no lookup-key or retained-native-wrapper inference is used.</summary>
    public static MapSourceCreatedSeasonLightingFrame CapturePreparedSeason(
        SkySeasonMainCameraRgbFrame camera, SkySeasonMainWeatherFogFrame weather)
    {
        ArgumentNullException.ThrowIfNull(camera);ArgumentNullException.ThrowIfNull(weather);
        weather.RequireCurrent(camera);
        if(camera.PreparationFrameDelta is not float delta || !float.IsFinite(delta) ||
            camera.PreparationDispatch!=NativeCameraTangentDispatch.Sse || camera.Inputs.FrameNumber<1 ||
            camera.Inputs.MainFrame.Camera.FrameNumber!=camera.Inputs.FrameNumber ||
            camera.Inputs.MainFrame.Camera.Source.Camera.ObjectId!=MainMapCameraAdapter.MainCameraObjectId ||
            camera.Inputs.BeforeFinalPreparationPrefix.Length!=0x3C0 || camera.SunMoonRgb.Prefix.Length!=0x3C0)
            throw new InvalidDataException("Present lighting requires the actual same-frame source Main RGB/weather producers and their explicit SSE delta.");
        if(camera.Inputs.Phase.LightingController.DeclaredTransmittanceTable!=SkyAtmoTableResources.ObjectId ||
            camera.Inputs.Source.Transmittance.Atan.Locale!=camera.Inputs.MainFrame.Camera.ExecutableLocale ||
            BinaryPrimitives.ReadUInt32LittleEndian(camera.SunMoonRgb.Prefix.Span.Slice(0x3B8,4))!=0x3F800000)
            throw new InvalidDataException("Present lighting differs from its successfully loaded source table or the actual Camera3B8=1 continuation.");
        return new(camera,weather,delta);
    }

    internal void ValidateInputs(MapSourceCreatedLightingFrameInputs input)
    {
        if(input.RuntimeTable!=MapLightingRuntimeTable.Present || !ReferenceEquals(input.PresentSeasonCamera,this) ||
            input.FrameNumber!=FrameNumber || input.LightingSkylightParameters!=Parameters ||
            BitConverter.SingleToUInt32Bits(input.FrameDelta)!=BitConverter.SingleToUInt32Bits(FrameDelta) ||
            !input.PriorPreparedCameraPrefix.Span.SequenceEqual(prior) ||
            (input.CurrentController is not null && input.CurrentController!=Controller))
            throw new InvalidDataException("Present lighting inputs differ from their actual owned season/table/Camera frame.");
    }

    internal void ValidatePreparedLighting(MapSourceCreatedLightingPacket lighting)
    {
        ValidateInputs(lighting.AcceptedInputs);
        if(lighting.SelectedController!=Controller || lighting.Clock!=Clock ||
            !lighting.PreparedCameraPrefix.Span.SequenceEqual(current))
            throw new InvalidDataException("Accepted Present lighting differs from its prepared same-frame season Camera/clock writer.");
    }
}
