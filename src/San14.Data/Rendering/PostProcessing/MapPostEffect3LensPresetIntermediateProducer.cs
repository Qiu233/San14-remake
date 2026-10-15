using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace San14.Data;

public sealed class MapPostEffect3LensPresetIntermediateMathSources
{
    public const string ResourceUri="san14://rules/post/lens-preset-intermediate";
    public const string RuleVersion="lens-preset-intermediate-v1";
    private const float StandardExposureMultiplier=.18f;
    private const float CentimetersToMeters=.01f;

    private MapPostEffect3LensPresetIntermediateMathSources(SkyWeatherNativePowerSource power,
        LoadedNativeSkyAtanSource atan,LoadedNativeCameraTangentSource tangent,float exposureMultiplier,float distanceUnitScale)
        =>(Power,Atan,Tangent,ExposureMultiplier,DistanceUnitScale)=(power,atan,tangent,exposureMultiplier,distanceUnitScale);
    public SkyWeatherNativePowerSource Power{get;}
    public LoadedNativeSkyAtanSource Atan{get;}
    public LoadedNativeCameraTangentSource Tangent{get;}
    public float ExposureMultiplier{get;}
    public float DistanceUnitScale{get;}
    public string Locale=>Power.Locale;
    public string SourceEvidence=>ResourceUri+"@"+RuleVersion+";"+Power.SourceProvenance+";"+
        Atan.SourceProvenance+";"+Tangent.ResourceUri+"@"+Tangent.RuleVersion;
    public static MapPostEffect3LensPresetIntermediateMathSources Load(GameResourceRepository repository,string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var power=SkyWeatherNativePowerSource.Load(repository,locale);
        var atan=NativeSkyAtanSource.Load(repository.SourceDirectory,locale);
        var tangent=NativeCameraTangentSource.Load(repository,locale);
        return new(power,atan,tangent,StandardExposureMultiplier,CentimetersToMeters);
    }
}

/// <summary>Actual caller fields frozen with the same-frame Camera.
/// The8 native float inputs do not come from the current pose's target distance.</summary>
public sealed class MapPostEffect3LensPresetCallerInputs
{
    private MapPostEffect3LensPresetCallerInputs(MapCameraConstructionPacket camera,
        float objectDistance,float infiniteFocalLength,float sensorDiagonalRatio,float sensorAspect,
        float fStop,float exposureDial,float lightAdaptationScale,float darkAdaptationScale,string evidence)
    {
        Camera=camera.CopyAcceptedSnapshot();SourceEvidence=evidence;
        (ObjectDistance,InfiniteFocalLength,SensorDiagonalRatio,SensorAspect,FStop,ExposureDial,LightAdaptationScale,DarkAdaptationScale)=
            (objectDistance,infiniteFocalLength,sensorDiagonalRatio,sensorAspect,fStop,exposureDial,lightAdaptationScale,darkAdaptationScale);
    }
    public MapCameraConstructionPacket Camera{get;}
    public long FrameNumber=>Camera.FrameNumber;
    public string SourceEvidence{get;}
    public float ObjectDistance{get;}
    public float InfiniteFocalLength{get;}
    public float SensorDiagonalRatio{get;}
    public float SensorAspect{get;}
    public float FStop{get;}
    public float ExposureDial{get;}
    public float LightAdaptationScale{get;}
    public float DarkAdaptationScale{get;}
    internal static MapPostEffect3LensPresetCallerInputs CapturePreparedNativeInputs(
        MapCameraConstructionPacket camera,float objectDistance,float infiniteFocalLength,
        float sensorDiagonalRatio,float sensorAspect,float fStop,float exposureDial,
        float lightAdaptationScale,float darkAdaptationScale,string sourceEvidence)
    {
        if(!float.IsFinite(objectDistance)||!float.IsFinite(infiniteFocalLength)||
            !float.IsFinite(sensorDiagonalRatio)||!float.IsFinite(sensorAspect)||
            !float.IsFinite(fStop)||!float.IsFinite(exposureDial)||
            !float.IsFinite(lightAdaptationScale)||!float.IsFinite(darkAdaptationScale))
            throw new NotSupportedException("Native Lens scalar caller left the finite eight-word domain.");
        return new(camera,objectDistance,infiniteFocalLength,sensorDiagonalRatio,sensorAspect,
            fStop,exposureDial,lightAdaptationScale,darkAdaptationScale,sourceEvidence);
    }
    public static MapPostEffect3LensPresetCallerInputs CaptureCurrentFields(
        MapCameraConstructionPacket actualCamera,float cpu1048ObjectDistance,float cpu104CInfiniteFocalLength,
        float cpu1050FStop,float cpu1054ExposureDial,float cpu12ECAspect,float cpu74LightAdaptedScale,
        float cpu78DarkAdaptedScale,string actualCallerEvidence)
    {
        ArgumentNullException.ThrowIfNull(actualCamera);ArgumentException.ThrowIfNullOrWhiteSpace(actualCallerEvidence);
        if(actualCamera.FrameNumber<=0||!float.IsFinite(cpu1048ObjectDistance)||!float.IsFinite(cpu104CInfiniteFocalLength)||
            !float.IsFinite(cpu1050FStop)||!float.IsFinite(cpu1054ExposureDial)||!float.IsFinite(cpu12ECAspect)||
            !float.IsFinite(cpu74LightAdaptedScale)||!float.IsFinite(cpu78DarkAdaptedScale)||!Sse.IsSupported)
            throw new InvalidDataException("Lens caller requires same-frame finite CPU fields and the native SSE sqrt instruction.");
        float diagonal=Sse.SqrtScalar(Vector128.CreateScalar(NativeFloat.Add(
            NativeFloat.Multiply(cpu12ECAspect,cpu12ECAspect),1))).GetElement(0);
        return new(actualCamera,cpu1048ObjectDistance,cpu104CInfiniteFocalLength,diagonal,cpu12ECAspect,
            cpu1050FStop,cpu1054ExposureDial,cpu74LightAdaptedScale,cpu78DarkAdaptedScale,
            actualCallerEvidence+"32B;aspect-sqrt=finiteSQRTSS;no-pose-distance-substitution");
    }
    public static MapPostEffect3LensPresetCallerInputs CaptureFreshSource(MapCameraConstructionPacket camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        var optics = camera.Optics;
        var caller = camera.CurrentSeasonPostEffectScalars?.QualityPatch;
        return CaptureCurrentFields(camera, optics.LensSimulation.ObjectDistance,
            optics.LensSimulation.InfiniteFocalLength, optics.LensSimulation.FStop,
            optics.LensSimulation.ExposureDial, optics.LensSimulation.SensorAspect,
            caller?.LightAdaptationScale ?? optics.Exposure.LightAdaptationScale,
            caller?.DarkAdaptationScale ?? optics.Exposure.DarkAdaptationScale,
            "camera-optics/lens-preset-caller");
    }
}

/// <summary>Complete23 native writes of /. The finite SSE
/// pow/atan/tangent ports retain their existing source domain gates. No
/// framework power, angle approximation or default Intermediate is used.</summary>
public static class MapPostEffect3LensPresetIntermediateProducer
{
    public static MapPostEffect3LensPresetIntermediatePacket PrepareFreshSource(
        MapCameraConstructionPacket actualCamera,LoadedMapPostEffect3LensPresetSource preset,
        MapPostEffect3LensPresetIntermediateMathSources sources,NativeCameraTangentDispatch explicitDispatch)
        =>PrepareCurrent(MapPostEffect3LensPresetCallerInputs.CaptureFreshSource(actualCamera),preset,sources,explicitDispatch);

    public static MapPostEffect3LensPresetIntermediatePacket PrepareCurrent(
        MapPostEffect3LensPresetCallerInputs caller,LoadedMapPostEffect3LensPresetSource preset,
        MapPostEffect3LensPresetIntermediateMathSources sources,NativeCameraTangentDispatch explicitDispatch)
    {
        ArgumentNullException.ThrowIfNull(caller);ArgumentNullException.ThrowIfNull(preset);ArgumentNullException.ThrowIfNull(sources);
        preset.ValidateForCamera(caller.Camera.Source);
        if(caller.FrameNumber<=0||caller.Camera.ExecutableLocale!=preset.ExecutableLocale||sources.Locale!=preset.ExecutableLocale||
            sources.Atan.Locale!=sources.Locale||sources.Tangent.Language!=sources.Locale)
            throw new InvalidDataException("Lens intermediate requires one same-frame Camera/preset/native math locale.");
        if(explicitDispatch!=NativeCameraTangentDispatch.Sse)
            throw new NotSupportedException("Lens intermediate uses the recovered native SSE power branch; AVX/FMA power needs its own producer.");
        byte[] output=new byte[92];
        var optics=preset.Settings.Optics;
        void W(int at,float value)=>BinaryPrimitives.WriteSingleLittleEndian(output.AsSpan(at,4),value);
        float Pow(float x,float y)=>SkyWeatherNativePower.PowSse(x,y,sources.Power);
        float Atan(float x)=>NativeSkyAtan.Atan1032034(x,sources.Atan);
        float magnification=D(caller.ObjectDistance,S(caller.ObjectDistance,caller.InfiniteFocalLength));W(0,magnification);
        float power=Pow(magnification,optics.FocusingPower);W(4,power);
        float focal=M(power,caller.InfiniteFocalLength);W(8,focal);
        float twiceFocal=A(focal,focal);
        W(0x18,M(Atan(D(D(optics.ImageDiagonal,caller.SensorDiagonalRatio),twiceFocal)),2));
        float ratio=D(focal,magnification);W(0xC,ratio);
        float fieldOfView=M(Atan(D(optics.ImageDiagonal,twiceFocal)),2);W(0x10,fieldOfView);
        W(0x14,M(Atan(D(M(D(optics.ImageDiagonal,caller.SensorDiagonalRatio),caller.SensorAspect),twiceFocal)),2));
        float range=D(S(ratio,optics.InfiniteFocalLengthRange.X),S(optics.InfiniteFocalLengthRange.Y,optics.InfiniteFocalLengthRange.X));W(0x1C,range);
        float multiplier=S(1,M(S(1,optics.MaximumImageFieldOfViewScale),S(1,range)));W(0x20,multiplier);
        W(0x24,M(multiplier,fieldOfView));
        float tangentInput=M(fieldOfView,.5f);
        float aperture=D(M(caller.ObjectDistance,caller.FStop),S(caller.ObjectDistance,ratio));W(0x28,aperture);
        float apertureRatio=D(optics.FStopRange.X,aperture);W(0x2C,apertureRatio);
        float tangent=NativeCameraTangent.Tangent1020A90(tangentInput,sources.Tangent,explicitDispatch);
        W(0x30,D(optics.ImageDiagonal,M(tangent,A(aperture,aperture))));
        float correction=A(D(S(focal,ratio),ratio),1);
        float correctedStop=M(correction,optics.InfiniteImageCircleRate);W(0x34,correctedStop);
        W(0x38,M(S(optics.InfiniteImageCircleRate,optics.InfiniteVignettingStartRate),apertureRatio));W(0x3C,M(correctedStop,.5f));
        W(0x40,A(M(D(S(caller.FStop,optics.FStopRange.X),S(optics.FStopRange.Y,optics.FStopRange.X)),S(optics.FStopShapeRange.Y,optics.FStopShapeRange.X)),optics.FStopShapeRange.X));
        W(0x44,M(optics.AdaptedTime.X,caller.LightAdaptationScale));W(0x48,M(optics.AdaptedTime.Y,caller.DarkAdaptationScale));
        W(0x54,M(Pow(2,caller.ExposureDial),sources.ExposureMultiplier));
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(0x58,4),0);
        W(0x4C,M(correction,correctedStop));W(0x50,M(M(S(correctedStop,optics.InfiniteVignettingStartRate),apertureRatio),.5f));
        return MapPostEffect3LensPresetIntermediatePacket.FromProducedMath(caller.Camera,output,
            preset.SourceEvidence+";"+caller.SourceEvidence+";"+sources.SourceEvidence+
            "explicit-native-SSE/nearest-even;all23-original-writes;accepted-camera-prefix-retained");
    }
    [MethodImpl(MethodImplOptions.NoInlining)]private static float A(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float S(float a,float b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float M(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float D(float a,float b)=>a/b;
}
