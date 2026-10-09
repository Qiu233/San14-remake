using System.Buffers.Binary;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace San14.Data;

/// <summary> clones wrapper/view records of the existing counted
/// physical texture. It does not allocate unrelated pixel images per level.</summary>
public sealed record OriginalMapPostEffect3EyeBloomViewPlan(int PhysicalTextureParameterIndex,
    int NativeMipCount,IReadOnlyList<int> ClonedWrapperMipLevels,ushort NativeWidth40,ushort NativeHeight42,
    ushort NativeSamplerFilterMask,string SourceEvidence,string Locale="TC")
{
    public bool ClonesExistingPhysicalPixels=>true;
    public bool PreservesFullWrapperDimensions=>true;
    public int ShaderResourceMipCount=>1;
}

/// <summary>A counted pointer setter whose source is either another current
/// table slot or a cloned single-mip wrapper of the specified physical owner.
/// The device consumer creates the view and supplies its real pointer.</summary>
public sealed record OriginalMapPostEffect3EyeTextureAliasRecipe(int TargetParameterIndex,
    int? SourceParameterIndex,int? PhysicalTextureParameterIndex,int? NativeMipLevel,
    string SourceEvidence)
{
    public bool UsesSingleMipWrapper=>PhysicalTextureParameterIndex is not null;
}

public sealed record OriginalMapPostEffect3EyePassIteration(int SourcePassIndex,int NativeCachedPassOrdinal,
    int NativeLoopIndex,int NativeLoopCount)
{
    public bool IsBloomGraphics=>NativeCachedPassOrdinal is 0 or 1 or 2 or 3 or 4 or 8 or 9 or 10 or 11;
    public bool IsCompute=>NativeCachedPassOrdinal is 5 or 6 or 7;
}

public sealed record OriginalMapPostEffect3EyeBeforeBindings(OriginalMapPostEffect3EyePassIteration Iteration,
    int? SourceTextureParameterIndex,int? SourceMipLevel,int? TargetTextureParameterIndex,int? TargetMipLevel,
    int ActualSourceWidth40,int ActualSourceHeight42,int ActualLoopLevel,
    OriginalMapPostEffect3ParameterConstantBuffer? BloomLoopConstants,string SourceEvidence,string Locale="TC")
{
    public int NativePixelTextureRegister=>0;
    public OriginalMapPostEffect3ResourceSetter TextureSetter=>OriginalMapPostEffect3ResourceSetter.ShaderResource;
    public int NativeTargetRegister=>0;
    public OriginalMapPostEffect3ResourceSetter TargetSetter=>OriginalMapPostEffect3ResourceSetter.RenderTarget;
    public int NativeSamplerStage=>2;
    public IReadOnlyList<int> NativeBeforeSamplerRegisters=>Array.AsReadOnly(new[]{7,8});
    public bool RequiresDynamicPixelTextureBinding=>Iteration.NativeCachedPassOrdinal is 3 or 4 or 10 or 11;
    public bool RequiresDynamicTargetBinding=>Iteration.NativeCachedPassOrdinal is 2 or 3 or 4 or 9 or 10 or 11;
    public OriginalMapPostEffect3EyeTextureAliasRecipe? PrefilterAliasChange{get;init;}
    public bool RequiresTemporaryBlendOverride=>Iteration.NativeCachedPassOrdinal is 4 or 11;
    public uint? TemporaryNativeColorStateF4=>RequiresTemporaryBlendOverride?0x111100u:null;
    public byte? TemporaryNativeRt0BlendEnable1C0=>RequiresTemporaryBlendOverride?(byte)1:null;
    public bool RestoreActualPreviousBlendState=>RequiresTemporaryBlendOverride;
    public bool RequiresNativeScissorOverride=>Iteration.NativeCachedPassOrdinal is 0 or 2 or 3 or 4 or 9 or 10 or 11;
    public bool RestoreActualPreviousScissor=>RequiresNativeScissorOverride;
    public bool RequiresGenericComputeDispatch=>Iteration.IsCompute;
    public ReadOnlyMemory<byte> NativeDrawCommand=>Iteration.IsCompute?ReadOnlyMemory<byte>.Empty:
        Convert.FromHexString("2D0003000000000003000000");
}

public sealed class OriginalMapPostEffect3EyeCurrentInputs
{
    private readonly OriginalMapPostEffect3TextureParameterState table;
    private readonly OriginalMapPostEffect3ResolvedTextureParameter color;
    internal OriginalMapPostEffect3EyeCurrentInputs(OriginalMapPostEffect3EyeAdaptation2Frame frame,
        OriginalMapPostEffect3TextureParameterState table,OriginalMapPostEffect3ResolvedTextureParameter color,
        ushort width,ushort height,uint contextWord,string evidence)
        =>(Frame,this.table,this.color,ColorWidth40,ColorHeight42,ActualContext1C48Word,SourceEvidence)=
            (frame,table,color,width,height,contextWord,evidence);
    public OriginalMapPostEffect3EyeAdaptation2Frame Frame{get;}
    public int ColorWidth40{get;}
    public int ColorHeight42{get;}
    public uint ActualContext1C48Word{get;}
    public string SourceEvidence{get;}
    public OriginalMapPostEffect3ResolvedTextureParameter ActualColorTexture=>color;
    public void RequireCurrent()
    {Frame.RequireCurrent();table.ValidateCurrent(color);}
}

public sealed class OriginalMapPostEffect3EyeAdaptation2Frame
{
    private readonly OriginalMapPostEffect3EyeAdaptation2Owner owner;
    internal OriginalMapPostEffect3EyeAdaptation2Frame(OriginalMapPostEffect3EyeAdaptation2Owner owner,
        OriginalMapPostEffect3ParameterState state,OriginalMapPostEffect3AllocationPlan allocation,
        IReadOnlyDictionary<string,byte[]> values,int levels,uint loopRate,uint mask,bool cropped,
        int activePhysicalAtInitialize,
        IReadOnlyList<OriginalMapPostEffect3EyePassIteration> iterations,
        OriginalMapPostEffect3ParameterConstantBuffer bloomMain)
        =>(this.owner,State,Allocation,Values,LevelCount,NativeLoopRate1A8Word,NativeMask1E8,
            InitialCroppedViewport294,ActivePhysicalTextureAtInitialize,OrderedIterations,BloomMainConstants)=
          (owner,state,allocation,values,levels,loopRate,mask,cropped,activePhysicalAtInitialize,iterations,bloomMain);
    public OriginalMapPostEffect3ParameterState State{get;}
    public OriginalMapPostEffect3AllocationPlan Allocation{get;}
    internal IReadOnlyDictionary<string,byte[]> Values{get;}
    public long FrameNumber=>State.FrameNumber;
    public string OwnerIdentity=>State.OwnerIdentity;
    public string Locale=>owner.Locale;
    public int LevelCount{get;}
    public uint NativeLoopRate1A8Word{get;}
    public uint NativeMask1E8{get;}
    public bool InitialCroppedViewport294{get;}
    public int ActivePhysicalTextureAtInitialize{get;}
    public IReadOnlyList<OriginalMapPostEffect3EyePassIteration> OrderedIterations{get;}
    public OriginalMapPostEffect3ParameterConstantBuffer BloomMainConstants{get;}
    public bool ProvesGpuExecution=>false;
    public bool RequiresExposureConstantProducer=>true;
    public string OutputAliasTargetName=>"BL2OutputColor";
    public bool BloomEnabled=>BinaryPrimitives.ReadInt32LittleEndian(Values["EnableBloom"])!=0;
    public string OutputAliasSourceName=>BloomEnabled?"BL2BloomPrefiltered":"BL2InputColor";
    public int? OutputAliasRequiredNativeMipLevel=>BloomEnabled?0:null;
    public OriginalMapPostEffect3EyeTextureAliasRecipe InitializeOutputAlias=>BloomEnabled?
        new(74,null,ActivePhysicalTextureAtInitialize,0,owner.SourceEvidence+";Init-enabled-current-controller28-wrapper"):
        new(74,68,null,null,owner.SourceEvidence+";Init-disabled-copy-current-InputColor68");
    public void RequireCurrent()
    {
        owner.RequireCurrent(this);
        foreach(var v in Values)if(!State.RequireValue(v.Key).Span.SequenceEqual(v.Value))
            throw new InvalidDataException("EyeAdaptation2 scalar changed after original preparation: "+v.Key);
    }
}

public sealed class LoadedOriginalMapPostEffect3EyeAdaptation2Source
{
    internal LoadedOriginalMapPostEffect3EyeAdaptation2Source(OriginalNativeLogTables log,string evidence,string locale="TC")
        =>(FloatLogTables,SourceEvidence,Locale)=(log,evidence,locale);
    internal OriginalNativeLogTables FloatLogTables{get;}
    public string Locale{get;}
    public string SourceEvidence{get;}
    /// <summary>Actual complete64B cbExposure2. The middle-gray log
    /// executes in binary64, while its denominator is the separately executed
    /// binary32 log(2) promoted to binary64; only the final ratio rounds32.</summary>
    public OriginalMapPostEffect3ParameterConstantBuffer PrepareExposure(
        OriginalMapPostEffect3EyeAdaptation2Frame frame,LoadedOriginalNativeDoubleLogSource actualDoubleLog,
        OriginalNativeLogDispatch actualDispatch)
    {
        ArgumentNullException.ThrowIfNull(frame);ArgumentNullException.ThrowIfNull(actualDoubleLog);frame.RequireCurrent();
        if(actualDoubleLog.Locale!=Locale||frame.Locale!=Locale||
            actualDispatch!=OriginalNativeLogDispatch.Sse)
            throw new InvalidDataException("Eye binary64/binary32 math source or actual owned dispatch differs.");
        var state=frame.State;var allocation=frame.Allocation;
        int index=state.FindResourceIndex(OriginalMapPostEffect3ParameterResourceTable.ConstantBuffer,"cbExposure2")
            ??throw new NotSupportedException("Eye cbExposure2 declaration missing.");
        var declaration=allocation.ConstantBufferParameters[index];
        if(index!=24||declaration.ByteCount!=64||declaration.Ownership!=OriginalMapPostEffect3AllocationOwnership.OwnedDeclaration)
            throw new InvalidDataException("Actual Eye Exposure CB24/64B declaration differs.");
        float F(string name,int word=0)=>BinaryPrimitives.ReadSingleLittleEndian(frame.Values[name].AsSpan(word*4,4));
        float rangeScale=OriginalNativeLogarithm.Divide(1,OriginalNativeFloat.Subtract(F("EvRangeMinMax",1),F("EvRangeMinMax")));
        float gray=(float)(actualDoubleLog.LogPositiveFloat(F("AdjustMiddleGray"),actualDispatch)/
            (double)OriginalNativeLogarithm.Log(2,FloatLogTables,actualDispatch));
        byte[] bytes=new byte[64];
        Copy(0,"AdaptedScaleLight");Copy(4,"AdaptedScaleDark");Copy(8,"EvRangeMinMax");Put(12,0);
        Put(16,gray);Copy(20,"DeltaTime");Put(24,0);Put(28,0);
        Put(32,rangeScale);
        float negativeRange=BitConverter.UInt32BitsToSingle(BitConverter.SingleToUInt32Bits(
            OriginalNativeFloat.Multiply(F("EvRangeMinMax"),rangeScale))^0x80000000u);
        Put(36,negativeRange);
        Put(40,OriginalNativeFloat.Multiply(F("ScreenWidth"),F("Viewport",2)));
        Put(44,OriginalNativeFloat.Multiply(F("ScreenHeight"),F("Viewport",3)));
        const uint percentWord=0x3C23D70A;
        float percent=BitConverter.UInt32BitsToSingle(percentWord);
        Put(48,OriginalNativeFloat.Multiply(F("OmittedLuminancePercentsLowHigh"),percent));
        Put(52,OriginalNativeFloat.Multiply(F("OmittedLuminancePercentsLowHigh",1),percent));
        Copy(56,"LuminanceRangeMinMax");Copy(60,"LuminanceRangeMinMax",1);
        return new(state,declaration,OriginalMapPostEffect3ConstantBufferWriter.Exposure,bytes,Locale);
        void Put(int at,float value)=>BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(at),value);
        void Copy(int at,string name,int word=0)=>frame.Values[name].AsSpan(word*4,4).CopyTo(bytes.AsSpan(at));
    }
    public OriginalMapPostEffect3EyeAdaptation2Owner ConstructFreshController(
        OriginalMapPostEffect3Container actualModule,string actualOwnerIdentity)
    {
        ArgumentNullException.ThrowIfNull(actualModule);ArgumentException.ThrowIfNullOrWhiteSpace(actualOwnerIdentity);
        OriginalMapPostEffect3ParameterState.ValidateSource(actualModule);
        return new(this,actualModule,actualOwnerIdentity);
    }
    public OriginalMapPostEffect3EyeCurrentInputs CaptureCurrentInputs(OriginalMapPostEffect3EyeAdaptation2Frame frame,
        OriginalMapPostEffect3TextureParameterState table,uint actualContext1C48Word,
        IReadOnlyList<OriginalMapPostEffect3Composite2ExternalExtent> actualExternalExtents,
        OriginalMapPostEffect3ParameterAliasMode actualNativeAliasMode,string sourceEvidence)
    {
        ArgumentNullException.ThrowIfNull(frame);ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(actualExternalExtents);ArgumentException.ThrowIfNullOrWhiteSpace(sourceEvidence);
        frame.RequireCurrent();
        if(!ReferenceEquals(table.Allocation,frame.Allocation)||table.FrameNumber!=frame.FrameNumber)
            throw new InvalidDataException("Eye current InputColor table belongs to another invocation.");
        int index=frame.State.FindResourceIndex(OriginalMapPostEffect3ParameterResourceTable.Texture,"BL2InputColor",actualNativeAliasMode)
            ??throw new NotSupportedException("Eye source InputColor declaration missing.");
        var color=table.Require(index);ushort width,height;
        if(color.Reference.PhysicalAllocationParameterIndex is int physical)
        {var a=frame.Allocation.TextureAllocations.Single(a=>a.Parameter.Index==physical);width=a.Width16;height=a.Height16;}
        else
        {
            var candidates=actualExternalExtents.Where(e=>e.ActualTexture.ParameterIndex==index).ToArray();
            if(candidates.Length!=1||string.IsNullOrWhiteSpace(candidates[0].SourceEvidence))
                throw new NotSupportedException("Eye actual InputColor wrapper has no current native dimensions.");
            table.ValidateCurrent(candidates[0].ActualTexture);
            if(candidates[0].ActualTexture!=color)throw new InvalidDataException("Eye wrapper dimensions belong to another pointer.");
            width=candidates[0].Width40;height=candidates[0].Height42;
        }
        // Resource v38 takes counted source wrappers before aliases. Native
        // cloning cannot be inferred if a mod replaced those owned pointers.
        foreach(int owned in new[]{85,86})
        {
            var pointer=table.Require(owned);
            if(pointer.Reference.PhysicalAllocationParameterIndex!=owned)
                throw new NotSupportedException("Eye resource initializer needs its actual counted Bloom physical wrappers.");
        }
        return new(frame,table,color,width,height,actualContext1C48Word,SourceEvidence+";"+sourceEvidence);
    }
    public OriginalMapPostEffect3EyeBloomViewPlan ReadOwnedBloomViewPlan(OriginalMapPostEffect3AllocationPlan allocation,bool fourK)
    {
        OriginalMapPostEffect3ParameterState.ValidateSource(allocation.Source);
        int index=fourK?86:85;
        var a=allocation.TextureAllocations.Single(t=>t.Parameter.Index==index);
        var shape=OriginalMapPostEffect3AllocationShapeReader.Read(a);
        if(shape.Dimension!=OriginalMapPostEffect3TextureDimension.Texture2D||!shape.HasRenderTargetPath||shape.MipLevels is <2 or >17)
            throw new NotSupportedException("Original EyeAdaptation2 bloom wrapper mip extent cannot initialize its sixteen-view array.");
        return new(index,shape.MipLevels,Array.AsReadOnly(Enumerable.Range(0,shape.MipLevels-1).ToArray()),
            a.Width16,a.Height16,0x5400,SourceEvidence+"-BL2Bloom/BL2Bloom4KCB"+
            "-native-wrapper-copy;(i,i)-exact-single-mip-SRV/RTV;sampler48=(48&3FF)|5400;not-new-pixels",Locale);
    }
    public OriginalMapPostEffect3EyeTextureAliasRecipe ReadResourceInitializationAlias(OriginalMapPostEffect3AllocationPlan allocation)
    {
        ReadOwnedBloomViewPlan(allocation,false);ReadOwnedBloomViewPlan(allocation,true);
        return new(69,null,85,0,SourceEvidence+";v38-initial-controller28-BL2Bloom85-single-mip0");
    }
}

/// <summary>Native factory8 constructor member294 begins0. Each native loop
/// writer later replaces it, so the next invocation's Init mask must retain
/// that value instead of reinitializing it from current Viewport prematurely.</summary>
public sealed class OriginalMapPostEffect3EyeAdaptation2Owner
{
    private readonly LoadedOriginalMapPostEffect3EyeAdaptation2Source source;
    private readonly OriginalMapPostEffect3Container module;
    private readonly string ownerIdentity;
    private bool croppedViewport294;
    private bool activeFourKWrapper;
    private OriginalMapPostEffect3EyeAdaptation2Frame? current;
    internal OriginalMapPostEffect3EyeAdaptation2Owner(LoadedOriginalMapPostEffect3EyeAdaptation2Source source,
        OriginalMapPostEffect3Container module,string owner)
        =>(this.source,this.module,ownerIdentity)=(source,module,owner);
    public string SourceEvidence=>source.SourceEvidence;
    public string Locale=>source.Locale;
    public OriginalMapPostEffect3EyeRetainedControllerSnapshot CaptureRetainedControllerSnapshot()=>
        new(ownerIdentity,module.Sha256,current?.FrameNumber??0,croppedViewport294,activeFourKWrapper);
    internal void RequireOwnedModuleForRetention(OriginalMapPostEffect3AllocationPlan allocation)
    {
        if(!ReferenceEquals(module,allocation.Source)||ownerIdentity!=allocation.ContextOwnerIdentity)
            throw new InvalidDataException("Retained Eye controller belongs to another module generation.");
    }
    internal void RequireCurrentPreparedStateForRetention(OriginalMapPostEffect3ParameterState state)
    {
        if(current is null||!ReferenceEquals(current.State,state))
            throw new InvalidDataException("Retained Eye Init requires its actual current prepared scalar state.");
        current.RequireCurrent();
    }
    public OriginalMapPostEffect3EyeAdaptation2Frame PrepareFrame(OriginalMapPostEffect3ParameterState state,
        OriginalMapPostEffect3AllocationPlan allocation,OriginalNativeLogDispatch actualFloatLogDispatch)
    {
        if(!ReferenceEquals(module,allocation.Source)||state.SourceSha256!=module.Sha256||state.OwnerIdentity!=ownerIdentity||
            allocation.ContextOwnerIdentity!=ownerIdentity||state.FrameNumber<=0||current is not null&&state.FrameNumber<=current.FrameNumber)
            throw new InvalidDataException("EyeAdaptation2 source controller needs its actual module/current later invocation.");
        if(actualFloatLogDispatch!=OriginalNativeLogDispatch.Sse||!Sse.IsSupported)
            throw new NotSupportedException("EyeAdaptation2 float-log route is limited to the inspected SSE source branch.");
        state.BindOwnedAllocation(allocation);
        var values=new Dictionary<string,byte[]>(StringComparer.Ordinal);
        foreach(var d in OriginalMapPostEffect3ApplyInitialScalars.ExposureDefaults)
        {
            int index=state.FindScalarIndex(d.Name)??throw new NotSupportedException("Eye capability4 scalar absent: "+d.Name);
            var declaration=state.Declarations[index];
            if(declaration.NativeValueType!=d.NativeType||declaration.ElementCount!=d.ElementCount)
                throw new InvalidDataException("Eye capability4 scalar type/count differs: "+d.Name);
            values.Add(d.Name,state.RequireValue(d.Name).ToArray());
        }
        float F(string n)=>BinaryPrimitives.ReadSingleLittleEndian(values[n]);
        int I(string n)=>BinaryPrimitives.ReadInt32LittleEndian(values[n]);
        float L(float v)=>OriginalNativeLogarithm.Log(v,source.FloatLogTables,actualFloatLogDispatch);
        float argument=OriginalNativeFloat.Multiply(F("ScreenHeight"),I("Enable4KCBFix")==1?.25f:.5f);
        float untruncated=OriginalNativeFloat.Subtract(OriginalNativeFloat.Add(
            OriginalNativeLogarithm.Divide(L(argument),L(2)),F("BloomRadius")),8);
        int rawLevel=Sse.ConvertToInt32WithTruncation(Vector128.CreateScalar(untruncated));
        float rate=OriginalNativeFloat.Subtract(OriginalNativeFloat.Add(untruncated,.5f),rawLevel);
        int levels=Math.Clamp(rawLevel,1,16);
        uint mask=0;
        if(I("EnableBloom")!=0)
        {
            mask=I("EnableDebugBloomSoftKneeCurve")!=0?3u:1u;
            if(levels>1)
                mask|=croppedViewport294?(levels>2?0xE00u:0xA00u):(levels>2?0x1Cu:0x14u);
        }
        mask|=0xE0;
        if(I("EnableDebugHistgram")!=0)mask|=0x100;
        string[] names=["DrawBloomPrefilterPS","DebugBloomSoftKneeCurvePS","DownBloomSample0PS","DownBloomSample1PS",
            "UpSampleBloomPS","ClearHistogramCS","CalcKEyeHistogramCS","CalcExposureScaleCS","DebugViewHistogramPS",
            "DownBloomSample0_ForViewportPS","DownBloomSample1_ForViewportPS","UpSampleBloom_ForViewportPS"];
        var selected=new Dictionary<int,int>();
        for(int ordinal=0;ordinal<names.Length;ordinal++)
        {
            var matches=module.ReadFunctorPasses(1).Where(p=>p.NameHash==OriginalMapPostEffect3ParameterState.PrimaryNameHash(names[ordinal])).ToArray();
            if(matches.Length!=1)throw new InvalidDataException("Eye native cached pass name differs: "+names[ordinal]);
            if((mask&(1u<<ordinal))!=0)selected.Add(matches[0].Index,ordinal);
        }
        var iterations=new List<OriginalMapPostEffect3EyePassIteration>();
        foreach(var pass in module.ReadFunctorPasses(1))if(selected.TryGetValue(pass.Index,out int ordinal))
        {
            int count=ordinal is 3 or 10?Math.Max(0,levels-2):ordinal is 4 or 11?Math.Max(0,levels-1):1;
            for(int i=0;i<count;i++)iterations.Add(new(pass.Index,ordinal,i,count));
        }
        var main=OriginalMapPostEffect3ParameterConstantBuffers.PrepareBloom2Main(state,allocation,source.Locale);
        return current=new(this,state,allocation,values,levels,BitConverter.SingleToUInt32Bits(rate),mask,croppedViewport294,
            activeFourKWrapper?86:85,
            Array.AsReadOnly(iterations.ToArray()),main);
    }

    /// <summary>Actual dynamic t0/RT0 and complete32B loop constants.
    /// source width/height for prefilter come from its selected InputColor
    /// wrapper; all other mip-wrapper dimensions derive from source allocation.
    /// This neither binds a GPU sampler nor publishes texture readiness.</summary>
    public OriginalMapPostEffect3EyeBeforeBindings PrepareBefore(OriginalMapPostEffect3EyeAdaptation2Frame frame,
        OriginalMapPostEffect3EyePassIteration iteration,OriginalMapPostEffect3EyeCurrentInputs inputs)
    {
        frame.RequireCurrent();ArgumentNullException.ThrowIfNull(inputs);inputs.RequireCurrent();
        if(!ReferenceEquals(inputs.Frame,frame))throw new InvalidDataException("Eye Before inputs belong to another actual frame.");
        if(!frame.OrderedIterations.Contains(iteration))throw new InvalidDataException("Eye iteration was not selected by current native Init mask.");
        bool fourK=BinaryPrimitives.ReadInt32LittleEndian(frame.Values["Enable4KCBFix"])==1;
        var view=source.ReadOwnedBloomViewPlan(frame.Allocation,fourK);
        OriginalMapPostEffect3EyeTextureAliasRecipe? aliasChange=null;
        if(activeFourKWrapper!=fourK)
        {
            activeFourKWrapper=fourK;
            aliasChange=new(69,null,view.PhysicalTextureParameterIndex,0,
                source.SourceEvidence+"Before-mode-change-copy-active-array-to-controller28/alias69");
        }
        int o=iteration.NativeCachedPassOrdinal,i=iteration.NativeLoopIndex;
        int? read=null,readMip=null,target=null,targetMip=null;int width=0,height=0,level=0;
        switch(o)
        {
            case 0:width=inputs.ColorWidth40;height=inputs.ColorHeight42;target=view.PhysicalTextureParameterIndex;targetMip=0;break;
            case 2:case 9:
                read=target=view.PhysicalTextureParameterIndex;readMip=0;targetMip=1;width=view.NativeWidth40;height=view.NativeHeight42;break;
            case 3:case 10:
                read=target=view.PhysicalTextureParameterIndex;readMip=i+1;targetMip=i+2;level=i+1;width=view.NativeWidth40;height=view.NativeHeight42;break;
            case 4:case 11:
                read=target=view.PhysicalTextureParameterIndex;readMip=frame.LevelCount-i-1;targetMip=frame.LevelCount-i-2;
                level=frame.LevelCount-i-1;width=view.NativeWidth40;height=view.NativeHeight42;break;
        }
        OriginalMapPostEffect3ParameterConstantBuffer? loop=null;
        if(width!=0||height!=0)
        {
            if(readMip is int rm&&(rm<0||rm>=view.ClonedWrapperMipLevels.Count)||
                targetMip is int tm&&(tm<0||tm>=view.NativeMipCount))
                throw new NotSupportedException("Actual Eye level count exceeds initialized original Bloom mip wrappers.");
            loop=OriginalMapPostEffect3ParameterConstantBuffers.PrepareBloom2Loop(frame.State,frame.Allocation,
                new(width,height,level,BitConverter.UInt32BitsToSingle(inputs.ActualContext1C48Word),frame.NativeLoopRate1A8Word),source.Locale);
            var vp=frame.Values["Viewport"];
            croppedViewport294=BinaryPrimitives.ReadSingleLittleEndian(vp.AsSpan(8))!=1||
                BinaryPrimitives.ReadSingleLittleEndian(vp.AsSpan(12))!=1;
        }
        return new(iteration,read,readMip,target,targetMip,width,height,level,loop,source.SourceEvidence+
            "-ordered-Before;-PS-t0/samplers7,8;-native-RT0-mip;-current-loop-CB;requires-owned-source-cache/view-producer",source.Locale)
            {PrefilterAliasChange=aliasChange};
    }
    internal void RequireCurrent(OriginalMapPostEffect3EyeAdaptation2Frame frame)
    {if(!ReferenceEquals(current,frame))throw new InvalidDataException("Eye source preparation was superseded by a later invocation.");}
}

/// <summary>CPU cache snapshot only. It neither restores a GPU owner nor
/// claims exposure/bloom contents, wrapper creation or source execution.</summary>
public sealed record OriginalMapPostEffect3EyeRetainedControllerSnapshot(string ContextOwnerIdentity,
    string ModuleSha256,long PreparedFrameNumber,bool CroppedViewport294,bool ActiveFourKWrapper);

public static class OriginalMapPostEffect3ParameterEyeAdaptation2
{
    public static LoadedOriginalMapPostEffect3EyeAdaptation2Source Load(OriginalGameRepository repository,string locale="TC")
    {
        ArgumentNullException.ThrowIfNull(repository);
        return OriginalDeferredSamplerSources.ReadPostEffect3EyeAdaptation2Source(
            OriginalDeferredSamplerSources.Load(repository.SourceDirectory,locale),OriginalNativeLogTables.Load(repository.SourceDirectory,locale));
    }
}

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalMapPostEffect3EyeAdaptation2Source ReadPostEffect3EyeAdaptation2Source(
        LoadedOriginalDeferredSamplerNativeSource native,OriginalNativeLogTables log)
    {
        if(native.Locale is not ("TC" or "SC")||log.Locale!=native.Locale)
            throw new NotSupportedException("EyeAdaptation2 source code and log tables need the inspected same SC/TC source.");

        return new(log,native.ResourceUri+$";{native.Locale}:paired-source-profile;"+";capability4-all20typedscalars;-logSSE-layercount"+
            "-original-mask;-counted-repetitions;-existingphysical85/86-mipviews"+
            "-Before;-main/-loop;requires-separate-doublelog-Exposure64B/source-mode-cache;no-GPU-claim",native.Locale);

    }
}
