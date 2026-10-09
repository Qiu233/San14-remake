using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace San14.Data;

/// <summary>Whole96B native Intermediate: the23 mathematical words plus the
/// independent native context comparison tag at5C. The application frame number
/// is an ownership key and is never substituted for that native counter.</summary>
public sealed class MapPostEffect3LensPresetPreparedIntermediatePacket
{
    private readonly byte[] bytes;
    private MapPostEffect3LensPresetPreparedIntermediatePacket(
        MapPostEffect3LensPresetIntermediatePacket math,uint nativeTag,string evidence)
    {
        Camera=math.Camera.CopyAcceptedSnapshot();FrameNumber=math.FrameNumber;bytes=new byte[96];
        math.Bytes.Span.CopyTo(bytes);BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x5C,4),nativeTag);
        SourceEvidence=math.SourceEvidence+";"+evidence;
    }
    public MapCameraConstructionPacket Camera{get;}
    public long FrameNumber{get;}
    public uint NativeComparisonTag5C=>BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0x5C,4));
    public ReadOnlyMemory<byte> Bytes=>bytes.ToArray();
    public string SourceEvidence{get;}

    /// <summary>Fresh Camera construction clears embedded1330..2BEF,
    /// including relativeIntermediate15CC+5C. The intermediate writer updates
    /// exactly92B and preserves that separate constructor-zero tag.</summary>
    public static MapPostEffect3LensPresetPreparedIntermediatePacket ConstructFreshSource(
        MapPostEffect3LensPresetIntermediatePacket math)
    {
        ArgumentNullException.ThrowIfNull(math);
        if(math.FrameNumber<=0)throw new InvalidDataException("Fresh96B Lens ownership requires an accepted Camera frame.");
        return new(math,0,"explicit-fresh-Camera;TC-/SC-memset-embedded1330/18C0"+
            "Intermediate-relative15CC+5C=wholeCPU2958;C4429C92B-math-preserves-marker");
    }
    public static MapPostEffect3LensPresetPreparedIntermediatePacket FromExplicitOwnedPreparedCamera(
        MapCameraConstructionPacket camera,ReadOnlySpan<byte> actualPreparedCamera,string producerEvidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(producerEvidence);
        if(actualPreparedCamera.Length<0x162C)
            throw new InvalidDataException("Prepared Camera must include all96B at its relative15CC.");
        var math=MapPostEffect3LensPresetIntermediatePacket.FromExplicitOwnedPreparedCamera(camera,actualPreparedCamera,producerEvidence);
        return new(math,BinaryPrimitives.ReadUInt32LittleEndian(actualPreparedCamera.Slice(0x1628,4)),
            producerEvidence+";explicit-owned-prepared-Camera-relative1628-native-marker");
    }
}

public sealed record MapPostEffect3LensPresetPrepareBaseInputs(long FrameNumber,
    uint ActualContextCounter1BF0,int DescriptorWidth,int DescriptorHeight,string ActualContextEvidence);
public sealed record MapPostEffect3LensPresetPrepareBaseResult(long FrameNumber,
    uint PreviousNativeComparisonTag,uint CurrentNativeComparisonTag,bool RegeneratedMath,
    bool NativeReturnValue,string SourceEvidence);

/// <summary> split-route preparation. Supported actual declarations are
/// all present: their unknown values cannot be treated as failed name lookups.
/// Marker-equal skip reads no current scalar values. Native lookup-missing or
/// nonfinite mathematical branches require a separate retained-data producer.</summary>
public static class MapPostEffect3LensPresetPrepareBase
{
    public static MapPostEffect3LensPresetPrepareBaseResult Apply(
        MapPostEffect3LensPresetParameterOwner pins,MapPostEffect3ParameterState state,
        MapPostEffect3LensPresetPrepareBaseInputs inputs,
        MapPostEffect3LensPresetIntermediateMathSources sources,
        NativeCameraTangentDispatch explicitDispatch)
    {
        ArgumentNullException.ThrowIfNull(pins);ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(inputs);ArgumentNullException.ThrowIfNull(sources);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputs.ActualContextEvidence);
        pins.ValidatePointerWrites(state);pins.RequirePrepareBaseStorage();
        if(inputs.FrameNumber!=pins.FrameNumber||sources.Locale!=pins.Source.ExecutableLocale)
            throw new InvalidDataException("Lens PrepareBase inputs differ from the actual pinned Camera frame/locale.");
        if(BinaryPrimitives.ReadUInt64LittleEndian(state.RequireValue("LensPreset").Span)!=0)
            throw new NotSupportedException("Lens PrepareBase unified pointer route needs its own actual native owner.");
        uint previous=pins.NativeComparisonTag5C;
        string evidence=pins.SourceEvidence+";"+inputs.ActualContextEvidence+";/";
        if(previous==inputs.ActualContextCounter1BF0)
            return new(pins.FrameNumber,previous,previous,false,true,evidence+";native-marker-equal-no-scalar-read");
        if(inputs.DescriptorWidth<=0||inputs.DescriptorHeight<=0||!Sse.IsSupported)
            throw new NotSupportedException("Native Lens PrepareBase extent must stay in its recovered positive finite SSE domain.");
        float Scalar(string name)
        {
            var value=state.RequireValue(name);
            if(value.Length!=4)throw new InvalidDataException("Native Lens PrepareBase scalar extent differs: "+name);
            float result=BinaryPrimitives.ReadSingleLittleEndian(value.Span);
            if(!float.IsFinite(result))throw new NotSupportedException("Native Lens PrepareBase scalar is nonfinite.");
            return result;
        }
        float distance=NativeFloat.Multiply(Scalar("ObjectDistance"),sources.DistanceUnitScale);
        float focal=NativeFloat.Multiply(Scalar("InfiniteFocalLength"),sources.DistanceUnitScale);
        float width=inputs.DescriptorWidth,height=inputs.DescriptorHeight;
        float square=NativeFloat.Add(SkyWeatherNativePower.PowSse(width,2,sources.Power),
            SkyWeatherNativePower.PowSse(height,2,sources.Power));
        float diagonal=Divide(Sse.SqrtScalar(Vector128.CreateScalar(square)).GetElement(0),height);
        var caller=MapPostEffect3LensPresetCallerInputs.CapturePreparedNativeInputs(pins.Camera,
            [distance,focal,diagonal,Divide(width,height),Scalar("FStop"),Scalar("ExposureDial"),
                Scalar("AdaptedScaleLight"),Scalar("AdaptedScaleDark")],evidence+";/-six-native-scalars-and-descriptor40-extent");
        var math=MapPostEffect3LensPresetIntermediateProducer.PrepareCurrent(caller,pins.Source,sources,explicitDispatch);
        pins.CommitPrepareBase(math,inputs.ActualContextCounter1BF0,evidence+"-exact-current-native-counter-write");
        return new(pins.FrameNumber,previous,inputs.ActualContextCounter1BF0,true,true,evidence+";all92B-math-before5C-counter-write");
    }
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Divide(float a,float b)=>a/b;
}
