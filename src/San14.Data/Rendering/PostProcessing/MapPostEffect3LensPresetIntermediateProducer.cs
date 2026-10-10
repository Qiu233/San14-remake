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
    private readonly byte[] bytes;
    private MapPostEffect3LensPresetCallerInputs(MapCameraConstructionPacket camera,float[] words,string evidence)
    {
        Camera=camera.CopyAcceptedSnapshot();SourceEvidence=evidence;bytes=new byte[32];
        for(int i=0;i<words.Length;i++)BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i*4,4),words[i]);
    }
    public MapCameraConstructionPacket Camera{get;}
    public long FrameNumber=>Camera.FrameNumber;
    public string SourceEvidence{get;}
    public ReadOnlyMemory<byte> Bytes=>bytes.ToArray();
    internal static MapPostEffect3LensPresetCallerInputs CapturePreparedNativeInputs(
        MapCameraConstructionPacket camera,float[] nativeWords,string sourceEvidence)
    {
        if(nativeWords.Length!=8||nativeWords.Any(v=>!float.IsFinite(v)))
            throw new NotSupportedException("Native Lens scalar caller left the finite eight-word domain.");
        return new(camera,nativeWords.ToArray(),sourceEvidence);
    }
    public static MapPostEffect3LensPresetCallerInputs CaptureCurrentFields(
        MapCameraConstructionPacket actualCamera,float cpu1048ObjectDistance,float cpu104CInfiniteFocalLength,
        float cpu1050FStop,float cpu1054ExposureDial,float cpu12ECAspect,float cpu74LightAdaptedScale,
        float cpu78DarkAdaptedScale,string actualCallerEvidence)
    {
        ArgumentNullException.ThrowIfNull(actualCamera);ArgumentException.ThrowIfNullOrWhiteSpace(actualCallerEvidence);
        float[] fields=[cpu1048ObjectDistance,cpu104CInfiniteFocalLength,cpu1050FStop,cpu1054ExposureDial,
            cpu12ECAspect,cpu74LightAdaptedScale,cpu78DarkAdaptedScale];
        if(actualCamera.FrameNumber<=0||fields.Any(v=>!float.IsFinite(v))||!Sse.IsSupported)
            throw new InvalidDataException("Lens caller requires same-frame finite CPU fields and the native SSE sqrt instruction.");
        float diagonal=Sse.SqrtScalar(Vector128.CreateScalar(NativeFloat.Add(
            NativeFloat.Multiply(cpu12ECAspect,cpu12ECAspect),1))).GetElement(0);
        return new(actualCamera,[cpu1048ObjectDistance,cpu104CInfiniteFocalLength,diagonal,cpu12ECAspect,
            cpu1050FStop,cpu1054ExposureDial,cpu74LightAdaptedScale,cpu78DarkAdaptedScale],
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
        byte[] input=caller.Bytes.ToArray(),payload=preset.PrimitiveBytes.ToArray(),output=new byte[92];
        float I(int at)=>BinaryPrimitives.ReadSingleLittleEndian(input.AsSpan(at,4));
        float P(int at)=>BinaryPrimitives.ReadSingleLittleEndian(payload.AsSpan(at,4));
        void W(int at,float value)=>BinaryPrimitives.WriteSingleLittleEndian(output.AsSpan(at,4),value);
        float Pow(float x,float y)=>SkyWeatherNativePower.PowSse(x,y,sources.Power);
        float Atan(float x)=>NativeSkyAtan.Atan1032034(x,sources.Atan);
        float magnification=D(I(0),S(I(0),I(4)));W(0,magnification);
        float power=Pow(magnification,P(0x40));W(4,power);
        float focal=M(power,I(4));W(8,focal);
        float twiceFocal=A(focal,focal);
        W(0x18,M(Atan(D(D(P(4),I(8)),twiceFocal)),2));
        float ratio=D(focal,magnification);W(0xC,ratio);
        float fieldOfView=M(Atan(D(P(4),twiceFocal)),2);W(0x10,fieldOfView);
        W(0x14,M(Atan(D(M(D(P(4),I(8)),I(0xC)),twiceFocal)),2));
        float range=D(S(ratio,P(0xC)),S(P(0x10),P(0xC)));W(0x1C,range);
        float multiplier=S(1,M(S(1,P(0x48)),S(1,range)));W(0x20,multiplier);
        W(0x24,M(multiplier,fieldOfView));
        float tangentInput=M(fieldOfView,.5f);
        float aperture=D(M(I(0),I(0x10)),S(I(0),ratio));W(0x28,aperture);
        float apertureRatio=D(P(0x18),aperture);W(0x2C,apertureRatio);
        float tangent=NativeCameraTangent.Tangent1020A90(tangentInput,sources.Tangent,explicitDispatch);
        W(0x30,D(P(4),M(tangent,A(aperture,aperture))));
        float correction=A(D(S(focal,ratio),ratio),1);
        float correctedStop=M(correction,P(0x4C));W(0x34,correctedStop);
        W(0x38,M(S(P(0x4C),P(0x50)),apertureRatio));W(0x3C,M(correctedStop,.5f));
        W(0x40,A(M(D(S(I(0x10),P(0x18)),S(P(0x1C),P(0x18))),S(P(0x24),P(0x20))),P(0x20)));
        W(0x44,M(P(0x84),I(0x18)));W(0x48,M(P(0x88),I(0x1C)));
        W(0x54,M(Pow(2,I(0x14)),sources.ExposureMultiplier));
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(0x58,4),0);
        W(0x4C,M(correction,correctedStop));W(0x50,M(M(S(correctedStop,P(0x50)),apertureRatio),.5f));
        return MapPostEffect3LensPresetIntermediatePacket.FromProducedMath(caller.Camera,output,
            preset.SourceEvidence+";"+caller.SourceEvidence+";"+sources.SourceEvidence+
            "explicit-native-SSE/nearest-even;all23-original-writes;accepted-camera-prefix-retained");
    }
    [MethodImpl(MethodImplOptions.NoInlining)]private static float A(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float S(float a,float b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float M(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float D(float a,float b)=>a/b;
}
