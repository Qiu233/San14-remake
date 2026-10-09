using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace San14.Data;

public sealed record MapPostEffect3LensFlareTextureAlias(int DestinationParameterIndex,int SourceParameterIndex,
    string DestinationName,string SourceName,string SourceEvidence);
public sealed record MapPostEffect3LensFlareBufferWrite(int ParameterIndex,ReadOnlyMemory<byte> Bytes,string SourceEvidence);

/// <summary>Retained native plLensFlare.self10. Fresh is +0; Init
/// compares this word with FStop before retaining the next value.</summary>
public sealed record MapPostEffect3LensFlareHistory(string SourceSha256,string OwnerIdentity,uint FStopBits)
{
    public static MapPostEffect3LensFlareHistory ConstructFresh(MapPostEffect3Container source,string owner)
    {MapPostEffect3ParameterState.ValidateSource(source);ArgumentException.ThrowIfNullOrWhiteSpace(owner);return new(source.Sha256,owner,0);}
}

public sealed record MapPostEffect3ParameterLensFlareInitialization(long FrameNumber,string OwnerIdentity,string SourceSha256,
    bool FStopChanged14,bool LensEnabled15,bool AnamorphicEnabled16,
    MapPostEffect3LensFlareHistory NextHistory,
    IReadOnlyList<MapPostEffect3ParameterConstantBuffer> ConstantBuffers,
    IReadOnlyList<MapPostEffect3LensFlareTextureAlias> TextureAliases,
    IReadOnlyList<MapPostEffect3LensFlareBufferWrite> BufferWrites,string SourceEvidence,string Locale="TC")
{
    public bool NativeInitializeReturn=>true;
    public bool RequiresTextureAliasProducer=>true;
    public bool RequiresBeforeHookProducer=>true;
}

/// <summary>Actual initialization, complete192B Lens constants and
/// complete16B down/up/bloom constants. Current source scope supports native
/// powf uses the recovered positive finite SSE kernel and its source tables.
/// Before1151B90's per-pass resource/dispatch mutations remain independent.</summary>
public static class MapPostEffect3ParameterLensFlare
{
    /// <summary>Actual callable Init identity in the loaded source locale.
    /// Uses the compiled paired source directory; performs no source I/O.</summary>
    public static MapPostEffect3ConstantBufferWriter InitializationWriter=>MapPostEffect3ConstantBufferWriter.LensLegacy;
    public static MapPostEffect3ParameterLensFlareInitialization PrepareInit(
        MapPostEffect3ParameterState state,MapPostEffect3AllocationPlan allocation,
        MapPostEffect3LensPresetParameterOwner actualLens,
        MapPostEffect3LensFlareHistory history,
        SkyWeatherNativePowerSource? actualNativePower=null,
        MapPostEffect3ParameterAliasMode aliasMode=MapPostEffect3ParameterAliasMode.UnknownNativeLocale)
    {
        ArgumentNullException.ThrowIfNull(state);ArgumentNullException.ThrowIfNull(allocation);
        ArgumentNullException.ThrowIfNull(actualLens);ArgumentNullException.ThrowIfNull(history);
        MapPostEffect3ParameterState.ValidateSource(allocation.Source);
        if(state.SourceSha256!=allocation.Source.Sha256||state.OwnerIdentity!=allocation.ContextOwnerIdentity||
            history.SourceSha256!=state.SourceSha256||history.OwnerIdentity!=state.OwnerIdentity||state.FrameNumber<=0||
            allocation.ContextWidth==0||allocation.ContextHeight==0||allocation.ContextWidth>int.MaxValue||allocation.ContextHeight>int.MaxValue)
            throw new InvalidDataException("LensFlare parameter/allocation/history scope differs.");
        actualLens.ValidatePointerWrites(state);
        if(actualLens.FrameNumber!=state.FrameNumber)throw new InvalidDataException("LensFlare pinned Lens parameters belong to another frame.");
        if(state.FindScalarIndex("LensPreset") is not null&&BinaryPrimitives.ReadUInt64LittleEndian(state.RequireValue("LensPreset").Span)!=0)
            throw new NotSupportedException("LensFlare nonnull unified LensPreset has no selected typed Split producer.");
        // performs independent named lookups: Split -> R15 at
        // /, Intermediate -> R12 at /. The
        // unified-preset branch uses +10 -> R15 and +18 -> R12 as well.
        byte[] split=actualLens.SplitBytes.ToArray(),intermediate=actualLens.IntermediateBytes.ToArray();
        if(split.Length!=656||intermediate.Length<92||actualLens.Source.GhostCount!=4||actualLens.Source.FlareCount!=16)
            throw new InvalidDataException("LensFlare current builtin0 parameter extent differs.");
        // Init validates lookup existence, even for values not subsequently read.
        foreach(string name in RequiredLookups)if(state.FindScalarIndex(name) is null)
            throw new InvalidDataException("LensFlare required native scalar lookup is absent: "+name);
        float objectDistance=F("ObjectDistance"),infinite=F("InfiniteFocalLength"),fstop=F("FStop"),
            near=F("CameraNear"),far=F("CameraFar"),white=F("WhitePoint");
        uint fstopBits=BitConverter.SingleToUInt32Bits(fstop);
        bool changed=BitConverter.UInt32BitsToSingle(history.FStopBits)!=fstop;
        bool lens=I("LensFlareEnable")!=0&&I("IntoSunLight")!=0&&split[0x54]==0;
        // repeats lookup into the SAME output pointer without clearing
        // it. If AnamorphicFlag is absent (the real payload), failed lookup
        // retains the AnamorphicEnable pointer and reads that scalar value.
        bool anamorphic=state.FindScalarIndex("AnamorphicEnable") is null||I("AnamorphicEnable")!=0;
        if(state.FindScalarIndex("AnamorphicFlag") is not null)anamorphic=I("AnamorphicFlag")!=0;
        float anamorphicScale=anamorphic?Bits(0x3ED9DF52):1f;
        float sky=state.FindScalarIndex("UseSky2") is not null&&I("UseSky2")!=0?6.5f:1f;
        float threshold=M(white,Bits(0x3F59999A));
        bool lowFix=state.FindScalarIndex("LowLuminanceBloomingFix") is not null&&I("LowLuminanceBloomingFix")!=0;
        if(!lowFix&&split[0x8C]==0&&I("AutoExposureEnable")!=0)
        {
            float dial=F("ExposureDial");
            float power=1;
            if(dial!=0)
            {
                if(actualNativePower is null||actualNativePower.Locale!=actualLens.Source.ExecutableLocale)
                    throw new NotSupportedException("LensFlare powf requires the matching original positive-finite SSE source kernel.");
                power=SkyWeatherNativePower.PowSse(2,dial,actualNativePower);
            }
            // zero-exponent source branch returns exactly binary32 one.
            threshold=D(threshold,power);
        }
        float width=allocation.ContextWidth,height=allocation.ContextHeight;
        byte[] main=new byte[192];
        Put(main,0,width);Put(main,4,height);Put(main,8,D(MathF.Sqrt(A(M(height,height),M(width,width))),height));Put(main,12,D(width,height));
        Put(main,16,M(objectDistance,Bits(0x3C23D70A)));Copy(main,20,intermediate,8);Put(main,24,M(near,Bits(0x3C23D70A)));Put(main,28,M(far,Bits(0x3C23D70A)));
        Copy(main,32,intermediate,0x10);Copy(main,36,split,8);Copy(main,40,split,4);Copy(main,44,split,0x44);
        Copy(main,48,intermediate,0x54);Put(main,52,threshold);CopyScalar(main,56,"DeltaTime");CopyScalar(main,60,"WhitePoint");
        CopyScalar(main,64,"SunProjPos");CopyScalar(main,68,"SunProjPos",4);CopyScalar(main,72,"SunScale");CopyScalar(main,76,"SunScale",4);
        Put(main,80,anamorphicScale);Put(main,84,1);Copy(main,88,intermediate,0x28);Put(main,92,I("ReverseZ")!=0?1:0);
        Copy(main,96,intermediate,0x44);Copy(main,100,intermediate,0x48);Put(main,104,0);Put(main,108,0);
        Put(main,112,D(1,height));Copy(main,116,intermediate,0x30);Put(main,120,0);Put(main,124,0);
        Put(main,128,BinaryPrimitives.ReadInt32LittleEndian(split.AsSpan(0x14)));Put(main,132,D(ReadF(intermediate,0x28),ReadF(split,0x1C)));
        Copy(main,136,intermediate,0x40);Put(main,140,1024);Put(main,144,U(split,0x58));Put(main,148,U(split,0x6C));Put(main,152,U(split,0x68));Put(main,156,U(split,0x78));
        Put(main,160,M(infinite,Bits(0x3C23D70A)));Copy(main,164,intermediate,4);Copy(main,168,split,0x7C);Copy(main,172,split,0x80);
        CopyScalar(main,176,"GhostScale");CopyScalar(main,180,"HaloScale");Put(main,184,sky);Put(main,188,0);
        var cbs=new List<MapPostEffect3ParameterConstantBuffer>{Packet("plLensFlareConstants",main)};
        uint w=allocation.ContextWidth>>1,h=allocation.ContextHeight>>1;
        for(int i=0;i<4;i++)
        {cbs.Add(Scale("plDownScaleConstants"+i,w,h,i==0||i==1?0:i-1));w>>=1;h>>=1;}
        uint upW=checked((uint)(int)M(allocation.ContextWidth>>1,anamorphicScale))&0xFFFFFFFC,
            upH=(allocation.ContextHeight>>1)&0xFFFFFFFC;
        for(int i=2;i>=0;i--)cbs.Add(Scale("plUpScaleConstants"+i,upW,upH,i));
        cbs.Add(Scale("plUpScaleConstantsAnamorphicBlur",upW>>1,upH,0));
        byte[] bloom=new byte[16];CopyScalar(bloom,0,"BloomScale");cbs.Add(Packet("plBloomConstants",bloom));
        var aliases=new[]{Alias("LFOutputToneMapInfo","ToneMapInfo"),Alias("LFOutputStarBurst","StarBurst"),
            Alias("LFOutputBloom",anamorphic?"AnamorphicBloom0":"Bloom1")};
        // Native map receives byte capacity, zeros ALL bytes, then copies only
        // when complete declared capacity fits actual builtin arrays.
        var buffers=new[]{Buffer("LensGroup",0x90,4,16),Buffer("SimpleGhost",0xD0,16,28)};
        return new(state.FrameNumber,state.OwnerIdentity,state.SourceSha256,changed,lens,anamorphic,
            new(state.SourceSha256,state.OwnerIdentity,fstopBits),cbs.AsReadOnly(),Array.AsReadOnly(aliases),Array.AsReadOnly(buffers),
            actualLens.SourceEvidence+";fresh-FStop0;-init;-complete192B;-scales;-bufferwrites",
            actualLens.Source.ExecutableLocale);

        int Resource(MapPostEffect3ParameterResourceTable table,string name)=>state.FindResourceIndex(table,name,aliasMode)
            ??throw new InvalidDataException("LensFlare source resource declaration absent: "+name);
        MapPostEffect3LensFlareTextureAlias Alias(string dst,string src)=>new(Resource(MapPostEffect3ParameterResourceTable.Texture,dst),
            Resource(MapPostEffect3ParameterResourceTable.Texture,src),dst,src,";source-alias");
        MapPostEffect3ParameterConstantBuffer Packet(string name,byte[] bytes)
        {
            var d=allocation.ConstantBufferParameters[Resource(MapPostEffect3ParameterResourceTable.ConstantBuffer,name)];
            if(d.ByteCount!=bytes.Length||d.Ownership!=MapPostEffect3AllocationOwnership.OwnedDeclaration)
                throw new InvalidDataException("LensFlare source CB ownership/extent differs: "+name);
            return new(state,d,InitializationWriter,bytes,
                actualLens.Source.ExecutableLocale);
        }
        MapPostEffect3ParameterConstantBuffer Scale(string name,uint x,uint y,int level)
        {byte[] b=new byte[16];Put(b,0,x);Put(b,4,y);Put(b,8,level);return Packet(name,b);}
        MapPostEffect3LensFlareBufferWrite Buffer(string name,int sourceAt,int count,int stride)
        {
            int index=Resource(MapPostEffect3ParameterResourceTable.Buffer,name);
            var d=allocation.BufferAllocations.Single(a=>a.Parameter.Index==index);
            if(d.Parameter.Ownership!=MapPostEffect3AllocationOwnership.OwnedDeclaration||d.ByteCount>int.MaxValue)
                throw new InvalidDataException("LensFlare source buffer ownership/extent differs.");
            byte[] bytes=new byte[(int)d.ByteCount];int n=checked(count*stride);
            if(bytes.Length>=n)split.AsSpan(sourceAt,n).CopyTo(bytes);
            return new(index,bytes,";zero-entire-capacity;-LensGroup;-SimpleGhost");
        }
        int I(string name)
        {Decl(name,6,4);return BinaryPrimitives.ReadInt32LittleEndian(state.RequireValue(name).Span);}
        float F(string name)
        {Decl(name,1,4);float f=BinaryPrimitives.ReadSingleLittleEndian(state.RequireValue(name).Span);if(!float.IsFinite(f))throw new InvalidDataException("LensFlare scalar nonfinite: "+name);return f;}
        void Decl(string name,uint type,int count)
        {int i=state.FindScalarIndex(name)??throw new InvalidDataException(name);var d=state.Declarations[i];
         if(d.NativeValueType!=type||d.ElementCount!=1||d.ValueByteCount!=count)throw new InvalidDataException("LensFlare scalar type/extent differs: "+name);}
        void CopyScalar(byte[] b,int target,string name,int sourceOffset=0)=>state.RequireValue(name).Span.Slice(sourceOffset,4).CopyTo(b.AsSpan(target,4));
    }

    public static IReadOnlyList<MapPostEffect3ParameterConstantBufferBinding> PreparePassBindings(
        MapPostEffect3AllocationPlan allocation,int passIndex,MapPostEffect3ParameterLensFlareInitialization init)
    {
        ArgumentNullException.ThrowIfNull(allocation);ArgumentNullException.ThrowIfNull(init);
        if(passIndex is <20 or >43||init.OwnerIdentity!=allocation.ContextOwnerIdentity||init.SourceSha256!=allocation.Source.Sha256||
            MapPostEffect3PassStateReader.Read(allocation.Source)[passIndex].Factory.Index!=4)
            throw new InvalidDataException("LensFlare pass/source owner differs.");
        return MapPostEffect3ParameterConstantBuffers.PreparePassBindings(allocation,passIndex,init.FrameNumber,init.ConstantBuffers);
    }
    private static readonly string[] RequiredLookups=["ObjectDistance","InfiniteFocalLength","FStop","ExposureDial","CameraNear","CameraFar","WhitePoint","SunProjPos","SunScale","ReverseZ","AutoExposureEnable","ExposureValue","DeltaTime","GhostScale","HaloScale","BloomScale","IntoSunLight"];
    private static float Bits(uint b)=>BitConverter.UInt32BitsToSingle(b);
    private static float ReadF(byte[] b,int at)=>BinaryPrimitives.ReadSingleLittleEndian(b.AsSpan(at));
    private static uint U(byte[] b,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(at));
    private static void Copy(byte[] d,int at,byte[] s,int from)=>s.AsSpan(from,4).CopyTo(d.AsSpan(at,4));
    private static void Put(byte[] b,int at,float f)=>BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(at),f);
    [MethodImpl(MethodImplOptions.NoInlining)]private static float M(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float A(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float D(float a,float b)=>a/b;
}
