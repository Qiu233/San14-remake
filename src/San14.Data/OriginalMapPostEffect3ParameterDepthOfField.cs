using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace San14.Data;

/// <summary>1150DF9/1150E4F aliases a source texture into DofOutputColor before
/// any pass executes. This is a producer requirement, not a completed binding.</summary>
public sealed record OriginalMapPostEffect3DepthOfFieldTextureAlias(
    string SourceName,string DestinationName,int SourceParameterIndex,int DestinationParameterIndex);

public sealed class OriginalMapPostEffect3ParameterDepthOfFieldUpdate
{
    private readonly byte[] extension;
    internal OriginalMapPostEffect3ParameterDepthOfFieldUpdate(OriginalMapPostEffect3ParameterState state,
        bool enabled,OriginalMapPostEffect3DepthOfFieldTextureAlias alias,
        OriginalMapPostEffect3ParameterConstantBuffer? packet,byte[] extensionBytes)
    {FrameNumber=state.FrameNumber;OwnerIdentity=state.OwnerIdentity;Enabled10=enabled;
     TextureAlias=alias;ConstantBuffer=packet;extension=extensionBytes;}
    public long FrameNumber{get;}
    public string OwnerIdentity{get;}
    public bool Enabled10{get;}
    public bool NativeInitializeReturn=>Enabled10;
    /// <summary>1153F90's complete shared before-hook returns handler byte10.</summary>
    public bool BeforeHookReturn=>Enabled10;
    public OriginalMapPostEffect3DepthOfFieldTextureAlias TextureAlias{get;}
    public bool RequiresTextureAliasProducer=>true;
    public OriginalMapPostEffect3ParameterConstantBuffer? ConstantBuffer{get;}
    /// <summary>1151256 compares nativeCB.self10 with global27F3D88. The
    /// conditional16B at offset96 are outside the selected shader's96B RDEF.
    /// These bytes are not silently applied to the declared96B allocation.</summary>
    public bool RequiresActualBackendExtensionPredicate=>Enabled10;
    public string BackendExtensionPredicate=>"TC:1151256;nativeCB.self10==global27F3D88";
    public int ConditionalBackendExtensionOffset=>96;
    public ReadOnlyMemory<byte> ConditionalBackendExtensionBytes=>extension.ToArray();
}

/// <summary>Actual factory2 plDepthOfField init1150D90 and before1153F90.
/// Prepares the complete selected plDoFConstants96B image. Parameter values
/// remain unknown until an actual scalar writer or owned LensPreset binds them.</summary>
public static class OriginalMapPostEffect3ParameterDepthOfField
{
    public static OriginalMapPostEffect3ParameterDepthOfFieldUpdate Prepare(
        OriginalMapPostEffect3ParameterState state,OriginalMapPostEffect3AllocationPlan allocation,
        OriginalMapPostEffect3LensPresetParameterOwner? actualLensParameters,
        int actualContextWidth40,int actualContextHeight44,
        OriginalMapPostEffect3ParameterAliasMode actualAliasMode=OriginalMapPostEffect3ParameterAliasMode.UnknownNativeLocale)
    {
        ArgumentNullException.ThrowIfNull(state);ArgumentNullException.ThrowIfNull(allocation);
        OriginalMapPostEffect3ParameterState.ValidateSource(allocation.Source);
        if(state.SourceSha256!=allocation.Source.Sha256||state.OwnerIdentity!=allocation.ContextOwnerIdentity||
            state.FrameNumber<=0||actualContextWidth40<=0||actualContextHeight44<=0||
            (uint)actualContextWidth40!=allocation.ContextWidth||(uint)actualContextHeight44!=allocation.ContextHeight)
            throw new InvalidDataException("DOF scalar/allocation/context descriptor owners differ or are stale.");
        //1150D60 starts enabled. Present DofEnable is an int !=0 test, not byte
        //or strictly positive. Unknown present values cannot become defaults.
        bool enabled=OptionalInt(state,"DofEnable",1)!=0;
        var alias=Alias(state,enabled?"DofOutputColorInternal":"DofInputColor","DofOutputColor",actualAliasMode);
        if(!enabled)return new(state,false,alias,null,Array.Empty<byte>());

        ArgumentNullException.ThrowIfNull(actualLensParameters);
        actualLensParameters.ValidatePointerWrites(state);
        if(actualLensParameters.FrameNumber!=state.FrameNumber)
            throw new InvalidDataException("DOF LensPreset pointers belong to another frame.");
        ReadOnlySpan<byte> split=actualLensParameters.SplitBytes.Span;
        ReadOnlySpan<byte> intermediate=actualLensParameters.IntermediateBytes.Span;
        if(split.Length!=656||intermediate.Length is not (92 or 96))
            throw new InvalidDataException("DOF actual LensPreset builtin0/intermediate byte extent differs.");
        //1151AA0 prefers LensPreset when nonnull, then only falls back to the
        //Split/Intermediate bank. This owned implementation closes the latter.
        if(state.FindScalarIndex("LensPreset") is not null&&
            BinaryPrimitives.ReadUInt64LittleEndian(state.RequireValue("LensPreset").Span)!=0)
            throw new NotSupportedException("DOF nonnull unified LensPreset producer has not been selected by this typed Split route.");
        bool anamorphic=OptionalInt(state,"AnamorphicEnable",1)!=0;
        if(state.FindScalarIndex("AnamorphicFlag") is not null)
            anamorphic=OptionalInt(state,"AnamorphicFlag",1)!=0;
        //Native requires the FStop lookup to succeed, but its scalar value is
        //not read by this writer. Prepared Intermediate already owns that math.
        RequireFloatDeclaration(state,"FStop");
        float distance=RequiredFloat(state,"ObjectDistance"),near=RequiredFloat(state,"CameraNear"),
            far=RequiredFloat(state,"CameraFar");
        bool reverse=RequiredInt(state,"ReverseZ")!=0;
        bool compareBrightness=OptionalInt(state,"CompareBrightness",0)!=0;
        int? index=state.FindResourceIndex(OriginalMapPostEffect3ParameterResourceTable.ConstantBuffer,"plDoFConstants");
        if(index is not int i)throw new NotSupportedException("Actual DOF constant buffer declaration is absent.");
        var declaration=allocation.ConstantBufferParameters[i];
        if(declaration.Index!=2||declaration.NameHash!=0x6746FE40||declaration.ByteCount!=96||
            declaration.Ownership!=OriginalMapPostEffect3AllocationOwnership.OwnedDeclaration)
            throw new InvalidDataException("Actual Functor0 DOF CB2/96B declaration differs.");

        float width=actualContextWidth40,height=actualContextHeight44;
        float diagonal=Sqrt(Add(Multiply(height,height),Multiply(width,width)));
        float scale=BitConverter.UInt32BitsToSingle(0x3C23D70A);
        byte[] bytes=new byte[96];
        Put(0,width);Put(4,height);Put(8,Divide(diagonal,height));Put(12,Divide(width,height));
        //1150E98 supplies R8=&[RBP-69] (Intermediate out),1150EA0 supplies
        //RDX=&[RBP-61] (Split out) to1151AA0. Later115113E loads RDX from
        //[RBP-69], so the CB store stream's RDX is Intermediate and R8 is
        //Split. In particular115121B reads the integer polygon stop count
        //from Split14, not the float field-of-view bits at Intermediate14.
        Put(16,Multiply(distance,scale));Copy(20,intermediate,8);Put(24,Multiply(near,scale));Put(28,Multiply(far,scale));
        Copy(32,intermediate,16);Copy(36,split,8);Copy(40,split,4);Copy(44,split,0x44);
        Put(48,anamorphic?BitConverter.UInt32BitsToSingle(0x3ED9DF52):1);Put(52,1);Copy(56,intermediate,0x28);
        Put(60,reverse?1:0);Put(64,Divide(1,height));Copy(68,intermediate,0x30);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(72),0);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(76),0);
        Put(80,BinaryPrimitives.ReadInt32LittleEndian(split[0x14..]));
        Put(84,Divide(F(intermediate,0x28),F(split,0x1C)));Copy(88,intermediate,0x40);Put(92,1024);
        byte[] extension=new byte[16];BinaryPrimitives.WriteUInt32LittleEndian(extension,compareBrightness?1u:0u);
        return new(state,true,alias,new(state,declaration,0x1150D90,bytes),extension);

        void Put(int at,float value)
        {
            if(!float.IsFinite(value))throw new NotSupportedException("DOF native shader output is outside the admitted finite source domain.");
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(at),value);
        }
        void Copy(int at,ReadOnlySpan<byte> source,int offset)=>source.Slice(offset,4).CopyTo(bytes.AsSpan(at));
    }
    private static OriginalMapPostEffect3DepthOfFieldTextureAlias Alias(OriginalMapPostEffect3ParameterState state,
        string source,string destination,OriginalMapPostEffect3ParameterAliasMode actualAliasMode)
    {
        int? s=state.FindResourceIndex(OriginalMapPostEffect3ParameterResourceTable.Texture,source,actualAliasMode);
        int? d=state.FindResourceIndex(OriginalMapPostEffect3ParameterResourceTable.Texture,destination,actualAliasMode);
        if(s is not int si||d is not int di)
            throw new NotSupportedException("DOF actual texture alias requires its source resource/lookup producer.");
        return new(source,destination,si,di);
    }
    private static void RequireFloatDeclaration(OriginalMapPostEffect3ParameterState state,string name)
    {
        int i=state.FindScalarIndex(name)??throw new NotSupportedException("DOF required scalar lookup is absent: "+name);
        var declaration=state.Declarations[i];
        if(declaration.NativeValueType!=1||declaration.ElementCount!=1||declaration.ValueByteCount!=4)
            throw new InvalidDataException("DOF scalar float declaration differs: "+name);
    }
    private static float RequiredFloat(OriginalMapPostEffect3ParameterState state,string name)
    {RequireFloatDeclaration(state,name);float f=F(state.RequireValue(name).Span,0);
     return float.IsFinite(f)?f:throw new NotSupportedException("DOF scalar float is nonfinite: "+name);}
    private static int RequiredInt(OriginalMapPostEffect3ParameterState state,string name)
    {
        int i=state.FindScalarIndex(name)??throw new NotSupportedException("DOF required scalar lookup is absent: "+name);
        var d=state.Declarations[i];if(d.NativeValueType!=6||d.ElementCount!=1)
            throw new InvalidDataException("DOF scalar integer declaration differs: "+name);
        return BinaryPrimitives.ReadInt32LittleEndian(state.RequireValue(name).Span);
    }
    private static int OptionalInt(OriginalMapPostEffect3ParameterState state,string name,int fallback)
        =>state.FindScalarIndex(name) is null?fallback:RequiredInt(state,name);
    private static float F(ReadOnlySpan<byte> bytes,int offset)=>BinaryPrimitives.ReadSingleLittleEndian(bytes[offset..]);
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Multiply(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Add(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Divide(float a,float b)=>a/b;
    private static float Sqrt(float value)
    {
        if(!Sse.IsSupported)throw new PlatformNotSupportedException("DOF source101FAD8 finite positive route requires x64 SSE sqrtss.");
        return Sse.SqrtScalar(Vector128.CreateScalar(value)).ToScalar();
    }
}
