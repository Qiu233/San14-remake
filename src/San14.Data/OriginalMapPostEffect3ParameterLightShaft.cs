using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace San14.Data;

public sealed record OriginalMapPostEffect3LightShaftTextureAlias(int TargetParameterIndex,int SourceParameterIndex,
    string TargetName,string SourceName,string SourceEvidence);

/// <summary>Actual Init and shared Before1153F90 outputs. An alias is
/// required on both branches; CB production alone does not apply it or execute
/// the seven source passes.</summary>
public sealed record OriginalMapPostEffect3ParameterLightShaftInitialization(long FrameNumber,string OwnerIdentity,
    string SourceSha256,bool InitializationReturnedTrue,bool BeforeHookAllowsPasses,
    OriginalMapPostEffect3ParameterConstantBuffer? ConstantBuffer,
    OriginalMapPostEffect3LightShaftTextureAlias OutputTextureAlias,string SourceEvidence,string Locale="TC")
{
    public bool RequiresOutputTextureAliasProducer=>true;
}

public static class OriginalMapPostEffect3ParameterLightShaft
{
    public static OriginalMapPostEffect3ParameterLightShaftInitialization PrepareInit(
        OriginalMapPostEffect3ParameterState state,OriginalMapPostEffect3AllocationPlan allocation,
        OriginalMapPostEffect3LightShaftExponential exponential,
        OriginalMapPostEffect3ParameterAliasMode actualAliasMode=OriginalMapPostEffect3ParameterAliasMode.UnknownNativeLocale)
    {
        ArgumentNullException.ThrowIfNull(state);ArgumentNullException.ThrowIfNull(allocation);ArgumentNullException.ThrowIfNull(exponential);
        OriginalMapPostEffect3ParameterState.ValidateSource(allocation.Source);
        if(state.SourceSha256!=allocation.Source.Sha256||state.OwnerIdentity!=allocation.ContextOwnerIdentity||state.FrameNumber<=0||
            allocation.ContextWidth<=0||allocation.ContextHeight<=0)
            throw new InvalidDataException("LightShaft scalar/allocation owners or frames differ.");
        bool enabled=I("LightShaftEnable")!=0&&I("IntoSunLight")!=0;
        string src=enabled?"LSOutputColorInternal":"LSInputColor",dst="LSOutputColor";
        int Resource(string name)=>state.FindResourceIndex(OriginalMapPostEffect3ParameterResourceTable.Texture,name,actualAliasMode)
            ??throw new InvalidDataException("LightShaft texture declaration is absent: "+name);
        string evidence=";freshEnabled10=1;-init;-LSEnable-dword"+
            "-IntoSunLight-dword;-before-returnsEnabled10"+exponential.SourceEvidence;
        var alias=new OriginalMapPostEffect3LightShaftTextureAlias(Resource(dst),Resource(src),dst,src,
            evidence+(enabled?"-internal-output-alias":"-input-output-alias"));
        if(!enabled)return new(state.FrameNumber,state.OwnerIdentity,state.SourceSha256,false,false,null,alias,evidence,exponential.Locale);
        int index=state.FindResourceIndex(OriginalMapPostEffect3ParameterResourceTable.ConstantBuffer,"plLightShaftConstants")
            ??throw new InvalidDataException("LightShaft CB is absent.");
        var cb=allocation.ConstantBufferParameters[index];
        if(cb.ByteCount!=144||cb.Ownership!=OriginalMapPostEffect3AllocationOwnership.OwnedDeclaration)
            throw new InvalidDataException("LightShaft native CB extent/ownership differs.");
        byte[] proj=V3("SunProjPos"),color=V3("SunColor");
        float large=F("LSLarge"),frequency=F("LSFrequency"),strong=F("LSStrong"),white=F("WhitePoint");
        int blur=I("LSGaussBlur"),step=I("LSStepLength"),reverse=I("ReverseZ");
        if(blur==0)throw new NotSupportedException("Native zero LightShaft Gaussian variance needs its separate nonfinite branch.");
        float size=blur,inverseVariance=D(-1,M(A(size,size),size));
        float[] weights=new float[16];float sum=0;
        for(int i=0;i<weights.Length;i++)
        {float sample=exponential.EvaluateSse(M(M(i,i),inverseVariance));weights[i]=sample;sum=A(sum,sample);}
        float inverseSum=D(1,sum);byte[] bytes=new byte[144];
        Put(0,0);Put(4,0);Put(8,0);Put(12,reverse!=0?1:0);
        Copy3(16,proj);Put(28,0);Copy3(32,color);Put(44,step);
        Put(48,large>BitConverter.UInt32BitsToSingle(0x3A83126F)?large:BitConverter.UInt32BitsToSingle(0x3A83126F));
        Put(52,frequency);Put(56,strong);Put(60,0);
        Put(64,M(allocation.ContextWidth,.5f));Put(68,M(allocation.ContextHeight,.5f));Put(72,white);
        Put(76,D(allocation.ContextWidth,allocation.ContextHeight));
        for(int i=0;i<weights.Length;i++)Put(80+i*4,M(weights[i],inverseSum));
        return new(state.FrameNumber,state.OwnerIdentity,state.SourceSha256,true,true,
            new(state,cb,OriginalMapPostEffect3ConstantBufferWriter.LightShaft,bytes,exponential.Locale),
            alias,evidence+"-complete144B-map-writes;source-context40/44-dimensions",exponential.Locale);
        void Put(int at,float v)=>BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(at),v);
        void Copy3(int at,byte[] values)=>values.CopyTo(bytes,at);
        int I(string name)
        {
            _=Decl(name,6,1,4);return BinaryPrimitives.ReadInt32LittleEndian(state.RequireValue(name).Span);
        }
        float F(string name)
        {
            _=Decl(name,1,1,4);float v=BinaryPrimitives.ReadSingleLittleEndian(state.RequireValue(name).Span);
            if(!float.IsFinite(v))throw new InvalidDataException("LightShaft scalar is nonfinite: "+name);return v;
        }
        byte[] V3(string name)
        {_=Decl(name,3,1,12);return state.RequireValue(name).ToArray();}
        OriginalMapPostEffect3ParameterDeclaration Decl(string name,uint type,byte count,int bytesCount)
        {
            int i=state.FindScalarIndex(name)??throw new InvalidDataException("LightShaft source scalar is absent: "+name);
            var d=state.Declarations[i];if(d.NativeValueType!=type||d.ElementCount!=count||d.ValueByteCount!=bytesCount)
                throw new InvalidDataException("LightShaft scalar type/extent differs: "+name);return d;
        }
    }
    public static IReadOnlyList<OriginalMapPostEffect3ParameterConstantBufferBinding> PreparePassBindings(
        OriginalMapPostEffect3AllocationPlan allocation,int passIndex,OriginalMapPostEffect3ParameterLightShaftInitialization init)
    {
        ArgumentNullException.ThrowIfNull(init);ArgumentNullException.ThrowIfNull(allocation);
        var state=OriginalMapPostEffect3PassStateReader.Read(allocation.Source);
        if(passIndex is <1 or >7||state[passIndex].Factory.Index!=1||
            init.OwnerIdentity!=allocation.ContextOwnerIdentity||init.SourceSha256!=allocation.Source.Sha256)
            throw new InvalidDataException("LightShaft pass/source owner differs.");
        if(!init.BeforeHookAllowsPasses)return Array.Empty<OriginalMapPostEffect3ParameterConstantBufferBinding>();
        if(init.ConstantBuffer is not{}cb)throw new InvalidDataException("Enabled LightShaft has no complete source CB.");
        return OriginalMapPostEffect3ParameterConstantBuffers.PreparePassBindings(allocation,passIndex,init.FrameNumber,[cb]);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]private static float A(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float M(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float D(float a,float b)=>a/b;
}
