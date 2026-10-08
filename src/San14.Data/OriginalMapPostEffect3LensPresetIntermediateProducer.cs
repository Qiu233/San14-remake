using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography;

namespace San14.Data;

public sealed class OriginalMapPostEffect3LensPresetIntermediateMathSources
{
    private OriginalMapPostEffect3LensPresetIntermediateMathSources(OriginalSkyWeatherNativePowerSource power,
        LoadedOriginalNativeSkyAtanSource atan,LoadedOriginalNativeCameraTangentSource tangent,float exposureMultiplier,float distanceUnitScale)
        =>(Power,Atan,Tangent,ExposureMultiplier,DistanceUnitScale)=(power,atan,tangent,exposureMultiplier,distanceUnitScale);
    public OriginalSkyWeatherNativePowerSource Power{get;}
    public LoadedOriginalNativeSkyAtanSource Atan{get;}
    public LoadedOriginalNativeCameraTangentSource Tangent{get;}
    public float ExposureMultiplier{get;}
    public float DistanceUnitScale{get;}
    public string Locale=>Power.Locale;
    public string SourceEvidence=>Power.SourceProvenance+";"+Atan.SourceProvenance+";"+Tangent.ResourceUri+
        "@"+Tangent.ExecutableSha256+";TC1148F00/SC1149F80-native-multiplier16DCBAC/16DD86C=3E3851EC;"+
        "TC1148CB0/SC1149D30-distance-scale13948B8/13958B8=3C23D70A";
    public static OriginalMapPostEffect3LensPresetIntermediateMathSources Load(OriginalGameRepository repository,string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var power=OriginalSkyWeatherNativePowerSource.Load(repository,locale);
        var atan=OriginalNativeSkyAtanSource.Load(repository.SourceDirectory,locale);
        var tangent=OriginalNativeCameraTangentSource.Load(repository,locale);
        byte[] exe=File.ReadAllBytes(tangent.PhysicalPath);
        if(Convert.ToHexString(SHA256.HashData(exe))!=tangent.ExecutableSha256)
            throw new InvalidDataException("Lens intermediate native source changed while reading its multiplier.");
        using var pe=new PEReader(new MemoryStream(exe,false));
        int rva=locale=="TC"?0x16DCBAC:0x16DD86C;
        var section=pe.PEHeaders.SectionHeaders.Single(s=>s.Name==".rdata"&&rva>=s.VirtualAddress&&
            rva+4<=s.VirtualAddress+s.SizeOfRawData);
        uint bits=BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(section.PointerToRawData+rva-section.VirtualAddress,4));
        if(bits!=0x3E3851EC)throw new InvalidDataException("Lens intermediate's original exposure multiplier differs.");
        int distanceRva=locale=="TC"?0x13948B8:0x13958B8;
        var distanceSection=pe.PEHeaders.SectionHeaders.Single(s=>s.Name==".rdata"&&distanceRva>=s.VirtualAddress&&
            distanceRva+4<=s.VirtualAddress+s.SizeOfRawData);
        uint distanceBits=BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(distanceSection.PointerToRawData+distanceRva-distanceSection.VirtualAddress,4));
        if(distanceBits!=0x3C23D70A)throw new InvalidDataException("Lens PrepareBase's source distance unit scale differs.");
        return new(power,atan,tangent,BitConverter.UInt32BitsToSingle(bits),BitConverter.UInt32BitsToSingle(distanceBits));
    }
}

/// <summary>Actual C4429C caller fields frozen with the same-frame Camera.
/// The8 native float inputs do not come from the current pose's target distance.</summary>
public sealed class OriginalMapPostEffect3LensPresetCallerInputs
{
    private readonly byte[] bytes;
    private OriginalMapPostEffect3LensPresetCallerInputs(OriginalMapCameraConstructionPacket camera,float[] words,string evidence)
    {
        Camera=camera.CopyAcceptedSnapshot();SourceEvidence=evidence;bytes=new byte[32];
        for(int i=0;i<words.Length;i++)BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i*4,4),words[i]);
    }
    public OriginalMapCameraConstructionPacket Camera{get;}
    public long FrameNumber=>Camera.FrameNumber;
    public string SourceEvidence{get;}
    public ReadOnlyMemory<byte> Bytes=>bytes.ToArray();
    internal static OriginalMapPostEffect3LensPresetCallerInputs CapturePreparedNativeInputs(
        OriginalMapCameraConstructionPacket camera,float[] nativeWords,string sourceEvidence)
    {
        if(nativeWords.Length!=8||nativeWords.Any(v=>!float.IsFinite(v)))
            throw new NotSupportedException("Native Lens scalar caller left the finite eight-word domain.");
        return new(camera,nativeWords.ToArray(),sourceEvidence);
    }
    public static OriginalMapPostEffect3LensPresetCallerInputs CaptureCurrentFields(
        OriginalMapCameraConstructionPacket actualCamera,float cpu1048ObjectDistance,float cpu104CInfiniteFocalLength,
        float cpu1050FStop,float cpu1054ExposureDial,float cpu12ECAspect,float cpu74LightAdaptedScale,
        float cpu78DarkAdaptedScale,string actualCallerEvidence)
    {
        ArgumentNullException.ThrowIfNull(actualCamera);ArgumentException.ThrowIfNullOrWhiteSpace(actualCallerEvidence);
        float[] fields=[cpu1048ObjectDistance,cpu104CInfiniteFocalLength,cpu1050FStop,cpu1054ExposureDial,
            cpu12ECAspect,cpu74LightAdaptedScale,cpu78DarkAdaptedScale];
        if(actualCamera.FrameNumber<=0||fields.Any(v=>!float.IsFinite(v))||!Sse.IsSupported)
            throw new InvalidDataException("Lens caller requires same-frame finite CPU fields and the native SSE sqrt instruction.");
        float diagonal=Sse.SqrtScalar(Vector128.CreateScalar(OriginalNativeFloat.Add(
            OriginalNativeFloat.Multiply(cpu12ECAspect,cpu12ECAspect),1))).GetElement(0);
        return new(actualCamera,[cpu1048ObjectDistance,cpu104CInfiniteFocalLength,diagonal,cpu12ECAspect,
            cpu1050FStop,cpu1054ExposureDial,cpu74LightAdaptedScale,cpu78DarkAdaptedScale],
            actualCallerEvidence+";C44206..C4429C/32B;aspect-sqrt101FAD8=finiteSQRTSS;no-pose-distance-substitution");
    }
    public static OriginalMapPostEffect3LensPresetCallerInputs CaptureFreshSource(OriginalMapCameraConstructionPacket camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        var source=OriginalMapCameraConstructionSource.ReadPerspective(camera.Source.Resource,camera.Source.Camera.ObjectId);
        camera.CurrentSeasonPostEffectScalars?.ValidateForCamera(camera);
        float Field(int ordinal,uint hash)
        {
            var p=source.Camera.Properties[ordinal];
            if(p.Index!=ordinal||p.NameHash!=hash||p.TypeCode!=8||p.ElementCount!=1||p.RawValue.Length!=4)
                throw new InvalidDataException("Lens caller original scalar declaration differs.");
            //C4429C consumes the actual CPU74/78 as well as1048..1054.
            //Main17E19/17E11 post-selection writes are transported to1C4/1C8;
            //retain those current words before LensPreset's later23word writer.
            int offset=ordinal switch{274=>0x12FC,275=>0x1300,276=>0x1304,277=>0x1308,
                54=>0x1C4,55=>0x1C8,_=>-1};
            if(camera.CurrentSeasonPostEffectScalars is{}current&&current.TryReadWord(offset,out uint written))
                return BitConverter.UInt32BitsToSingle(written);
            return BinaryPrimitives.ReadSingleLittleEndian(p.RawValue);
        }
        return CaptureCurrentFields(camera,Field(274,0xEE9D93F9),Field(275,0x995A7B0E),Field(276,0x04CFDD5D),
            Field(277,0x6934EE7C),Field(6,0xA70685F8),Field(54,0x907B67C1),Field(55,0x800DB8FF),
            source.ConstructorOrigin+";"+(camera.CurrentSeasonPostEffectScalars?.SourceEvidence??
                "explicit-owned-source-Camera-caller-fields/no-later-lens-scalar-writer"));
    }
}

/// <summary>Complete23 native writes of TC1148F00/SC1149F80. The finite SSE
/// pow/atan/tangent ports retain their existing source domain gates. No
/// framework power, angle approximation or default Intermediate is used.</summary>
public static class OriginalMapPostEffect3LensPresetIntermediateProducer
{
    public static OriginalMapPostEffect3LensPresetIntermediatePacket PrepareFreshSource(
        OriginalMapCameraConstructionPacket actualCamera,LoadedOriginalMapPostEffect3LensPresetSource preset,
        OriginalMapPostEffect3LensPresetIntermediateMathSources sources,OriginalNativeCameraTangentDispatch explicitDispatch)
        =>PrepareCurrent(OriginalMapPostEffect3LensPresetCallerInputs.CaptureFreshSource(actualCamera),preset,sources,explicitDispatch);

    public static OriginalMapPostEffect3LensPresetIntermediatePacket PrepareCurrent(
        OriginalMapPostEffect3LensPresetCallerInputs caller,LoadedOriginalMapPostEffect3LensPresetSource preset,
        OriginalMapPostEffect3LensPresetIntermediateMathSources sources,OriginalNativeCameraTangentDispatch explicitDispatch)
    {
        ArgumentNullException.ThrowIfNull(caller);ArgumentNullException.ThrowIfNull(preset);ArgumentNullException.ThrowIfNull(sources);
        preset.ValidateForCamera(caller.Camera.Source);
        if(caller.FrameNumber<=0||caller.Camera.ExecutableLocale!=preset.ExecutableLocale||sources.Locale!=preset.ExecutableLocale||
            sources.Atan.Locale!=sources.Locale||sources.Tangent.Language!=sources.Locale)
            throw new InvalidDataException("Lens intermediate requires one same-frame Camera/preset/native math locale.");
        if(explicitDispatch!=OriginalNativeCameraTangentDispatch.Sse)
            throw new NotSupportedException("Lens intermediate uses the recovered native SSE power branch; AVX/FMA power needs its own producer.");
        byte[] input=caller.Bytes.ToArray(),payload=preset.PrimitiveBytes.ToArray(),output=new byte[92];
        float I(int at)=>BinaryPrimitives.ReadSingleLittleEndian(input.AsSpan(at,4));
        float P(int at)=>BinaryPrimitives.ReadSingleLittleEndian(payload.AsSpan(at,4));
        void W(int at,float value)=>BinaryPrimitives.WriteSingleLittleEndian(output.AsSpan(at,4),value);
        float Pow(float x,float y)=>OriginalSkyWeatherNativePower.PowSse(x,y,sources.Power);
        float Atan(float x)=>OriginalNativeSkyAtan.Atan1032034(x,sources.Atan);
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
        float tangent=OriginalNativeCameraTangent.Tangent1020A90(tangentInput,sources.Tangent,explicitDispatch);
        W(0x30,D(P(4),M(tangent,A(aperture,aperture))));
        float correction=A(D(S(focal,ratio),ratio),1);
        float correctedStop=M(correction,P(0x4C));W(0x34,correctedStop);
        W(0x38,M(S(P(0x4C),P(0x50)),apertureRatio));W(0x3C,M(correctedStop,.5f));
        W(0x40,A(M(D(S(I(0x10),P(0x18)),S(P(0x1C),P(0x18))),S(P(0x24),P(0x20))),P(0x20)));
        W(0x44,M(P(0x84),I(0x18)));W(0x48,M(P(0x88),I(0x1C)));
        W(0x54,M(Pow(2,I(0x14)),sources.ExposureMultiplier));
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(0x58,4),0);
        W(0x4C,M(correction,correctedStop));W(0x50,M(M(S(correctedStop,P(0x50)),apertureRatio),.5f));
        return OriginalMapPostEffect3LensPresetIntermediatePacket.FromProducedMath(caller.Camera,output,
            preset.SourceEvidence+";"+caller.SourceEvidence+";"+sources.SourceEvidence+
            ";explicit-native-SSE/nearest-even;all23-original1148F00-writes;accepted-camera-prefix-retained");
    }
    [MethodImpl(MethodImplOptions.NoInlining)]private static float A(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float S(float a,float b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float M(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float D(float a,float b)=>a/b;
}
