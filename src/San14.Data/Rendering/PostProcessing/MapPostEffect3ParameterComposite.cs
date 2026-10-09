using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace San14.Data;

public sealed record MapPostEffect3CompositeBeforeSamplerRequirement(
    int NativeStageOrdinal,int RegisterIndex,string NativeManagerSource);

public sealed record MapPostEffect3ParameterCompositeUpdate(long FrameNumber,string OwnerIdentity,
    MapPostEffect3ParameterConstantBuffer ConstantBuffer,
    IReadOnlyList<MapPostEffect3CompositeBeforeSamplerRequirement> BeforeSamplerRequirements)
{
    public bool NativeInitializeReturn=>true;
    public bool RequiresBeforeSamplerProducer=>true;
    public bool RequiresSharedPrepareBaseProducer=>true;
}

/// <summary>Actual factory5 Composite init complete160B writer;
/// before1153340 binds native manager samplers7/8. Does not substitute shaders,
/// execute a GPU draw or infer those implicit sampler handles.</summary>
public static class MapPostEffect3ParameterComposite
{
    /// <summary>Actual callable Init identity in the loaded source locale.
    /// Uses the compiled paired source directory; performs no source I/O.</summary>
    public static MapPostEffect3ConstantBufferWriter InitializationWriter=>MapPostEffect3ConstantBufferWriter.CompositeLegacy;
    public static MapPostEffect3ParameterCompositeUpdate Prepare(
        MapPostEffect3ParameterState state,MapPostEffect3AllocationPlan allocation,
        MapPostEffect3LensPresetParameterOwner actualLens,
        MapPostEffect3ParameterCompositeMathSources math,
        int actualContextWidth40,int actualContextHeight44,NativeCameraTangentDispatch actualDispatch)
    {
        ArgumentNullException.ThrowIfNull(state);ArgumentNullException.ThrowIfNull(allocation);
        ArgumentNullException.ThrowIfNull(actualLens);ArgumentNullException.ThrowIfNull(math);
        MapPostEffect3ParameterState.ValidateSource(allocation.Source);
        if(state.SourceSha256!=allocation.Source.Sha256||state.OwnerIdentity!=allocation.ContextOwnerIdentity||
            state.FrameNumber<=0||actualContextWidth40<=0||actualContextHeight44<=0||
            (uint)actualContextWidth40!=allocation.ContextWidth||(uint)actualContextHeight44!=allocation.ContextHeight||
            actualLens.FrameNumber!=state.FrameNumber||actualLens.Source.ExecutableLocale!=math.Locale||
            math.Tangent.Language!=math.Locale||actualDispatch!=NativeCameraTangentDispatch.Sse)
            throw new InvalidDataException("Composite scalar/context/Lens/native math owners, extents or dispatch differ.");
        actualLens.ValidatePointerWrites(state);
        if(BinaryPrimitives.ReadUInt64LittleEndian(Value("LensPreset",7,1).Span)!=0)
            throw new NotSupportedException("Composite typed Split route requires actual unified LensPreset null.");
        // performs its own named pointer lookups: Split -> RSI at
        // /, Intermediate -> R14 at /. Its
        // unified-preset branch likewise uses +10/+18 in that same order.
        ReadOnlySpan<byte> split=actualLens.SplitBytes.Span,intermediate=actualLens.IntermediateBytes.Span;
        if(split.Length!=656||intermediate.Length is not (92 or 96))
            throw new InvalidDataException("Composite Lens builtin0/intermediate source extent differs.");
        //Native first reads AnamorphicFlag, then AnamorphicEnable overrides it.
        bool anamorphic=OptionalInt("AnamorphicFlag",1)!=0;
        if(state.FindScalarIndex("AnamorphicEnable") is not null)anamorphic=Int("AnamorphicEnable")!=0;
        foreach(string name in new[]{"InfiniteFocalLength","FStop","CameraFov","CameraAperture"})
            Declaration(name,1,1); //required lookup-only fields; actual Lens intermediate owns their upstream math
        float exposureDial=Float("ExposureDial"),whitePoint=Float("WhitePoint"),deltaTime=Float("DeltaTime"),
            exposureValue=Float("ExposureValue"),gammaValue=Float("GammaValue"),saturation=Float("ColorSaturation"),
            edgeThreshold=Float("FXAAEdgeThreshold"),edgeMinimum=Float("FXAAEdgeThresholdMin"),
            edgeSharpness=Float("FXAAEdgeSharpness"),fade=Float("VignettingFade");
        var sunProjection=Value("SunProjPos",3,1);var sunScale=Value("SunScale",2,1);
        int reverse=Int("ReverseZ"),automatic=Int("AutoExposureEnable"),gamma=Int("GammaEnable"),
            fxaa=Int("FXAAEnable"),kelvin=Int("ColorTemperature"),lowFix=Int("LowLuminanceBloomingFix");
        bool useSky2=OptionalInt("UseSky2",0)!=0;
        int index=state.FindResourceIndex(MapPostEffect3ParameterResourceTable.ConstantBuffer,"plCompositeConstants")
            ??throw new NotSupportedException("Actual Composite CB declaration is absent.");
        var declaration=allocation.ConstantBufferParameters[index];
        if(index!=16||declaration.NameHash!=0x8EF18D14||declaration.ByteCount!=160||
            declaration.Ownership!=MapPostEffect3AllocationOwnership.OwnedDeclaration)
            throw new InvalidDataException("Actual Composite CB16/160B source declaration differs.");
        float width=actualContextWidth40,height=actualContextHeight44;
        float aspectScale=anamorphic?BitConverter.UInt32BitsToSingle(0x3ED9DF52):1;
        float diagonalSquared=Pow(Sqrt(Add(Pow(width,2),Pow(height,2))),2);
        float angle=Multiply(Multiply(F(split,0x48),F(intermediate,0x10)),.5f);
        float tangent=NativeCameraTangent.Tangent1020A90(angle,math.Tangent,actualDispatch);
        float vignette=Divide(diagonalSquared,Multiply(Pow(tangent,2),4));
        float scaledWidth=Divide(width,aspectScale);
        float radius=Multiply(Sqrt(Add(Pow(height,2),Pow(scaledWidth,2))),.5f);
        float range=Multiply(Subtract(F(intermediate,0x4C),F(intermediate,0x50)),radius);
        float tail=Add(Multiply(radius,F(intermediate,0x50)),Multiply(radius,F(intermediate,0x50)));
        byte[] colorTemperature=math.LookupColorTemperature(unchecked((uint)kelvin)).ToArray();
        float whiteExposure=Multiply(whitePoint,BitConverter.UInt32BitsToSingle(0x3F59999A));
        if(lowFix==0&&split[0x8C]==0&&automatic!=0)whiteExposure=Divide(whiteExposure,Pow(2,exposureDial));
        byte[] bytes=new byte[160];
        Put(0,width);Put(4,height);Put(8,Divide(Sqrt(Add(Multiply(height,height),Multiply(width,width))),height));Put(12,Divide(width,height));
        Copy(16,intermediate,0x10);Copy(20,split,8);Copy(24,split,4);Copy(28,split,0x44);
        Copy(32,intermediate,0x54);Put(36,whiteExposure);Put(40,deltaTime);Put(44,whitePoint);
        Copy(48,sunProjection.Span,0);Copy(52,sunProjection.Span,4);Copy(56,sunScale.Span,0);Copy(60,sunScale.Span,4);
        Put(64,aspectScale);Put(68,1);Copy(72,intermediate,0x28);Put(76,reverse!=0?1:0);
        Put(80,edgeThreshold);Put(84,edgeMinimum);Put(88,edgeSharpness);Put(92,2);
        Put(96,vignette);Put(100,1);Put(104,range);Put(108,tail);
        colorTemperature.CopyTo(bytes,112);Put(124,saturation);
        Put(128,gamma!=0?1:0);Put(132,gammaValue);Put(136,automatic);Put(140,exposureValue);
        Put(144,fxaa!=0?1:0);Put(148,fade);Put(152,useSky2?6.5f:1);Put(156,0);
        return new(state.FrameNumber,state.OwnerIdentity,new(state,declaration,InitializationWriter,bytes,math.Locale),
            Array.AsReadOnly(new[]{new MapPostEffect3CompositeBeforeSamplerRequirement(2,7,"ShaderManager30/source-linear"),
                new MapPostEffect3CompositeBeforeSamplerRequirement(2,8,"ShaderManager38/source-point")}));

        void Declaration(string name,uint type,int count)
        {int i=state.FindScalarIndex(name)??throw new NotSupportedException("Composite required scalar lookup is absent: "+name);
         var d=state.Declarations[i];if(d.NativeValueType!=type||d.ElementCount!=count)
            throw new InvalidDataException("Composite native scalar declaration differs: "+name);}
        ReadOnlyMemory<byte> Value(string name,uint type,int count)
        {Declaration(name,type,count);return state.RequireValue(name);}
        float Float(string name)
        {float v=F(Value(name,1,1).Span,0);return float.IsFinite(v)?v:throw new NotSupportedException("Composite nonfinite scalar: "+name);}
        int Int(string name)=>BinaryPrimitives.ReadInt32LittleEndian(Value(name,6,1).Span);
        int OptionalInt(string name,int absent)=>state.FindScalarIndex(name) is null?absent:Int(name);
        float Pow(float x,float y)=>SkyWeatherNativePower.PowSse(x,y,math.Power);
        void Put(int at,float value)
        {if(!float.IsFinite(value))throw new NotSupportedException("Composite shader output is outside the admitted finite native domain.");
         BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(at),value);}
        void Copy(int at,ReadOnlySpan<byte> value,int offset)=>value.Slice(offset,4).CopyTo(bytes.AsSpan(at));
    }
    private static float F(ReadOnlySpan<byte> b,int offset)=>BinaryPrimitives.ReadSingleLittleEndian(b[offset..]);
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Add(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Subtract(float a,float b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Multiply(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Divide(float a,float b)=>a/b;
    private static float Sqrt(float value)
    {if(!Sse.IsSupported||value<0||!float.IsFinite(value))throw new NotSupportedException("Composite source finite SQRTSS domain is unresolved.");
     return Sse.SqrtScalar(Vector128.CreateScalar(value)).ToScalar();}
}
