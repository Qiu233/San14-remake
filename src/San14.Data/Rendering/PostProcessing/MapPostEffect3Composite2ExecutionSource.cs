using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace San14.Data;

/// <summary>The ShaderManager module+30 linear and +38 point 8x8 M1
/// sampler carriers. Original/ create both and114AAD0 installs
/// them. They are independent of Camera LUT assets and named texture slots.
/// The positional legacy names remain compatible with existing clients.</summary>
public sealed record MapPostEffect3Composite2ModuleLuts(long FrameNumber,string ContextOwnerIdentity,
    MapPostEffect3ApplyView ActualHdrWrapper30,MapPostEffect3ApplyView? ActualLdrWrapper38,
    ushort ActualHdrSampler48,string SourceEvidence)
{
    public MapPostEffect3ApplyView ActualLinearWrapper30=>ActualHdrWrapper30;
    public MapPostEffect3ApplyView ActualPointWrapper38=>ActualLdrWrapper38??
        throw new InvalidDataException("The actual ShaderManager point carrier38 must exist.");
    public ushort ActualLinearSampler48=>ActualHdrSampler48;
}

public sealed class MapPostEffect3Composite2TextureAlias
{
    private readonly MapPostEffect3TextureParameterState table;
    private readonly MapPostEffect3ResolvedTextureParameter? source;
    private readonly long? nullRevision;
    internal MapPostEffect3Composite2TextureAlias(MapPostEffect3TextureParameterState table,int from,int to,
        bool clearsAlternate,string evidence,string locale="TC")
    {
        this.table=table;SourceParameterIndex=from;DestinationParameterIndex=to;ClearsAlternateRuntimePointer=clearsAlternate;SourceEvidence=evidence;Locale=locale;
        if(table.ReadKnownNullablePointer(from) is null)nullRevision=table.CaptureKnownNullRevision(from);
        else source=table.Require(from);
    }
    public int SourceParameterIndex{get;}
    public int DestinationParameterIndex{get;}
    public string Locale{get;}
    public string SourceEvidence{get;}
    public MapPostEffect3ResolvedTextureParameter? ActualCopiedSource=>source;
    public bool CopiesKnownNull=>source is null;
    public bool CopiesActualSelectedSide=>true;
    public bool ClearsAlternateRuntimePointer{get;}
    internal void ValidateSource(MapPostEffect3TextureParameterState actualTable)
    {
        if(!ReferenceEquals(table,actualTable))throw new InvalidDataException("Composite2 alias uses another actual texture table.");
        if(source is null)table.ValidateKnownNullRevision(SourceParameterIndex,nullRevision!.Value);else table.ValidateCurrent(source);
    }
    internal void ValidateCopiedDestination(MapPostEffect3TextureParameterState actualTable)
    {
        if(!ReferenceEquals(table,actualTable)||!ReferenceEquals(table.ReadKnownNullablePointer(DestinationParameterIndex),source?.Reference))
            throw new InvalidDataException("Composite2 Init must copy the accepted native wrapper and side, including null.");
    }
}

/// <summary> writes bytes0..147 of the160B CB23. Its twelve padding
/// bytes receive no native write. A consumer supplies actual mapped/staging
/// bytes to preserve them; zero padding is never inferred from this writer.</summary>
public sealed class MapPostEffect3Composite2CrossFilterPatch
{
    private readonly MapPostEffect3Composite2ExecutionFrame frame;
    private readonly byte[] written;
    internal MapPostEffect3Composite2CrossFilterPatch(MapPostEffect3Composite2ExecutionFrame frame,int phase,byte[] bytes)
        =>(this.frame,NativePhase, written)=(frame,phase,bytes.ToArray());
    public int NativePhase{get;}
    public MapPostEffect3ConstantBufferWriter Writer=>MapPostEffect3ConstantBufferWriter.ToneInfo;
    public int ConstantBufferParameterIndex=>23;
    public int NativeDeclaredByteCount=>160;
    public int ActualWrittenByteCount=>148;
    public ReadOnlyMemory<byte> WrittenBytes=>written.ToArray();
    public MapPostEffect3ParameterConstantBuffer CompleteWithActualMappedBytes(
        ReadOnlyMemory<byte> actualMapped160,string actualMappedProducerEvidence)
    {
        frame.RequireCurrent();ArgumentException.ThrowIfNullOrWhiteSpace(actualMappedProducerEvidence);
        if(actualMapped160.Length!=160)throw new InvalidDataException("CrossFilter needs its actual160B mapped/staging allocation.");
        byte[] bytes=actualMapped160.ToArray();written.CopyTo(bytes,0);
        return new(frame.State,frame.Allocation.ConstantBufferParameters[23],Writer,bytes,frame.Locale);
    }
}

public sealed class MapPostEffect3Composite2ExecutionFrame
{
    private readonly LoadedMapPostEffect3Composite2ExecutionSource source;
    private readonly MapPostEffect3TextureParameterState table;
    private readonly IReadOnlyDictionary<string,byte[]> values;
    private MapPostEffect3ResolvedTextureParameter? initialized79,initialized78;
    private long? null79,null78;
    private bool initialized;
    private readonly MapPostEffect3ResolvedTextureParameter? preparedBloom74;
    private readonly long? preparedNull74;
    private ushort savedHdrSamplerLow4;
    internal MapPostEffect3Composite2ExecutionFrame(LoadedMapPostEffect3Composite2ExecutionSource source,
        MapPostEffect3ParameterState state,MapPostEffect3AllocationPlan allocation,
        MapPostEffect3TextureParameterState table,MapPostEffect3ParameterComposite2Update update,
        MapPostEffect3Composite2ModuleLuts luts,IReadOnlyDictionary<string,byte[]> values,
        IReadOnlyList<MapPostEffect3Composite2TextureAlias> aliases,
        MapPostEffect3ParameterConstantBuffer? hdr)
    {this.source=source;State=state;Allocation=allocation;this.table=table;Prepared=update;ActualInitialModuleLuts=luts;
        this.values=values;InitializationAliases=aliases;HdrRemakeConstants=hdr;savedHdrSamplerLow4=(ushort)(luts.ActualLinearSampler48&15);
        if(table.ReadKnownNullablePointer(74) is null)preparedNull74=table.CaptureKnownNullRevision(74);else preparedBloom74=table.Require(74);}
    public MapPostEffect3ParameterState State{get;}
    public MapPostEffect3AllocationPlan Allocation{get;}
    public MapPostEffect3ParameterComposite2Update Prepared{get;}
    public MapPostEffect3ParameterComposite2Update PreparedUpdate=>Prepared;
    public object ActualFrameIdentity=>this;
    internal MapPostEffect3TextureParameterState CurrentTextureTable=>table;
    internal ushort SavedHdrSamplerLow4=>savedHdrSamplerLow4;
    internal void CaptureCross0Sampler(ushort actualSampler48)=>savedHdrSamplerLow4=(ushort)(actualSampler48&15);
    public MapPostEffect3Composite2ModuleLuts ActualInitialModuleLuts{get;}
    public MapPostEffect3Composite2ModuleLuts InitialModuleSamplerWrappers=>ActualInitialModuleLuts;
    public long FrameNumber=>State.FrameNumber;
    public string OwnerIdentity=>State.OwnerIdentity;
    public string SourceSha256=>State.SourceSha256;
    public uint NativeCapability1F0=>Prepared.NativeCapability1F0;
    public IReadOnlyList<MapPostEffect3Composite2TextureAlias> InitializationAliases{get;}
    public MapPostEffect3ParameterConstantBuffer? HdrRemakeConstants{get;}
    public MapPostEffect3Composite2CrossFilterPatch? InitialCrossFilterConstants{get;private set;}
    internal ushort ActualBloomWidth40{get;private set;}
    internal ushort ActualBloomHeight42{get;private set;}
    internal MapPostEffect3ResolvedTextureParameter ActualInputBloom74=>preparedBloom74??
        throw new InvalidDataException("Positive native CrossFilter dereferences its nonnull InputBloom74 wrapper.");
    internal void AcceptInitialCrossFilter(MapPostEffect3Composite2CrossFilterPatch? patch,ushort width,ushort height)
    {InitialCrossFilterConstants=patch;ActualBloomWidth40=width;ActualBloomHeight42=height;}
    public bool HasAppliedInitialization=>initialized;
    public bool RequiresCrossFilterConstants=>Float("CrossFilterWeight")>0;
    public bool RequiresHdrRemakeConstants=>Word("EnableSeparateHdrLut")!=0;
    public byte NativeInitializeReturnAl=>1;
    public byte NativeAfterReturnAl=>1;
    public byte NativeFinishReturnAl=>1;
    public bool AfterWritesState=>false;
    public bool ProvesGpuExecution=>false;
    public string SourceEvidence=>source.SourceEvidence;
    public string Locale=>source.Locale;
    internal float Float(string name,int word=0)=>BinaryPrimitives.ReadSingleLittleEndian(values[name].AsSpan(word*4));
    internal int Word(string name)=>BinaryPrimitives.ReadInt32LittleEndian(values[name]);
    internal ReadOnlyMemory<byte> Value(string name)=>values[name].ToArray();
    public void ValidateInitializationFor(MapPostEffect3TextureParameterState actualTable)
    {
        if(initialized)throw new InvalidOperationException("Composite2 current Init aliases were already applied.");
        RequireTable(actualTable);Prepared.RequireCurrent();ValidateScalars();
        foreach(var alias in InitializationAliases)alias.ValidateSource(actualTable);
    }
    /// <summary>Called by the typed table immediately after its ordered native
    /// pointer setters. Prepare's CB22 remains the pre-Init source snapshot.</summary>
    internal void AcceptInitialization(MapPostEffect3TextureParameterState actualTable)
    {
        RequireTable(actualTable);
        if(initialized)throw new InvalidOperationException("Composite2 Init was already accepted.");
        foreach(var alias in InitializationAliases)alias.ValidateCopiedDestination(actualTable);
        Capture(79,out initialized79,out null79);
        if(InitializationAliases.Any(a=>a.DestinationParameterIndex==78))Capture(78,out initialized78,out null78);
        initialized=true;
        void Capture(int index,out MapPostEffect3ResolvedTextureParameter? pointer,out long? revision)
        {pointer=table.ReadKnownNullablePointer(index) is null?null:table.Require(index);revision=pointer is null?table.CaptureKnownNullRevision(index):null;}
    }
    public MapPostEffect3ResolvedTextureParameter? ValidateInitializationOutput79(MapPostEffect3TextureParameterState actualTable)
    {
        RequireTable(actualTable);RequireInitialized();return initialized79;
    }
    internal long InitializedNull79Revision=>null79??throw new InvalidDataException("Composite2 output79 is not null.");
    public void RequireCurrent()
    {
        ValidateScalars();
        if(preparedBloom74 is null)table.ValidateKnownNullRevision(74,preparedNull74!.Value);else table.ValidateCurrent(preparedBloom74);
        if(!initialized){Prepared.RequireCurrent();return;}
        if(initialized79 is null)table.ValidateKnownNullRevision(79,null79!.Value);else table.ValidateCurrent(initialized79);
        if(InitializationAliases.Any(a=>a.DestinationParameterIndex==78))
        {if(initialized78 is null)table.ValidateKnownNullRevision(78,null78!.Value);else table.ValidateCurrent(initialized78);}
    }
    internal void RequireInitialized(){RequireCurrent();if(!initialized)throw new InvalidDataException("Composite2 needs its actual ordered Init pointer setters first.");}
    internal void RequireTable(MapPostEffect3TextureParameterState actualTable)
    {if(!ReferenceEquals(actualTable,table))throw new InvalidDataException("Composite2 execution uses another current texture table.");}
    private void ValidateScalars()
    {foreach(var v in values)if(!State.RequireValue(v.Key).Span.SequenceEqual(v.Value))throw new InvalidDataException("Composite2 scalar changed after native Prepare: "+v.Key);}
}

public sealed class MapPostEffect3Composite2BeforeBindings
{
    internal MapPostEffect3Composite2BeforeBindings(MapPostEffect3Composite2ExecutionFrame frame,
        int pass,int ordinal,MapPostEffect3Composite2ModuleLuts luts,ushort? sampler,bool clear,int? clearMask,
        MapPostEffect3Composite2CrossFilterPatch? cross,string evidence)
        =>(Frame,SourcePassIndex,NativeCachedPassOrdinal,ActualModuleLuts,NativeHdrSampler48AfterWrite,ClearsNativeTarget0,
            NativeContext1C18Write,CrossFilterConstants,SourceEvidence)=(frame,pass,ordinal,luts,sampler,clear,clearMask,cross,evidence);
    public MapPostEffect3Composite2ExecutionFrame Frame{get;}
    public int SourcePassIndex{get;}
    public int NativeCachedPassOrdinal{get;}
    public MapPostEffect3Composite2ModuleLuts ActualModuleLuts{get;}
    public MapPostEffect3Composite2ModuleLuts ModuleSamplerWrappers=>ActualModuleLuts;
    public ushort? NativeHdrSampler48AfterWrite{get;}
    public ushort? NativeLinearSampler48AfterWrite=>NativeHdrSampler48AfterWrite;
    public bool ClearsNativeTarget0{get;}
    public int? NativeContext1C18Write{get;}
    public MapPostEffect3Composite2CrossFilterPatch? CrossFilterConstants{get;}
    public string SourceEvidence{get;}
    public byte NativeBeforeReturnAl=>1;
    public int NativePixelStage=>2;
    public int NativeHdrSamplerRegister=>7;
    public int NativeLinearSamplerRegister=>7;
    public int NativeLdrSamplerRegister=>8;
    public int NativePointSamplerRegister=>8;
    public bool OverridesActualHdrAndLdrSamplers=>true;
    public bool OverridesActualModuleCarrierSamplers=>true;
    public bool IsCompute=>NativeCachedPassOrdinal==10;
    public bool ChangesScissor=>!IsCompute&&Frame.NativeCapability1F0>=4;
    public bool ChangesBlend=>NativeCachedPassOrdinal is 0 or 1 or 12 or 13;
    public bool UsesViewportScalarForScissor=>NativeCachedPassOrdinal is 0 or 1 or 7 or 8 or 9 or 12 or 13 or 14 or 15 or 16;
    public bool RequiresPriorTargetContents=>ChangesBlend;
    public uint? TemporaryNativeColorStateF4=>ChangesBlend?0x111100u:null;
    public byte? TemporaryNativeRt0BlendEnable1C0=>ChangesBlend?(byte)1:null;
    public bool RequiresActualPostBindingTarget=>!IsCompute;
    public void RequireCurrent()=>Frame.RequireInitialized();
}

public sealed class MapPostEffect3Composite2GraphicsRecipe
{
    private readonly MapPostEffect3TextureParameterState table;
    internal MapPostEffect3Composite2GraphicsRecipe(MapPostEffect3Composite2BeforeBindings before,
        MapPostEffect3TextureParameterState table,MapPostEffect3EyeGraphicsCurrentState current,
        MapPostEffect3EyeGraphicsSelectedTarget target,int targetCount,FinalEffectScissor? scissor)
    {Before=before;this.table=table;ActualCurrentState=current;ActualSelectedTarget=target;ActualEffectiveTargetCount=targetCount;DrawScissor=scissor;}
    public MapPostEffect3Composite2BeforeBindings Before{get;}
    public MapPostEffect3EyeGraphicsCurrentState ActualCurrentState{get;}
    public MapPostEffect3EyeGraphicsSelectedTarget ActualSelectedTarget{get;}
    public int ActualEffectiveTargetCount{get;}
    public MapPostEffect3EyeNativeViewport DrawViewport=>ActualCurrentState.ActualViewport;
    public FinalEffectScissor? DrawScissor{get;}
    public bool DrawScissorEnabled=>Before.ChangesScissor||ActualCurrentState.ActualScissor is not null;
    public uint DrawNativeColorStateF4=>Before.TemporaryNativeColorStateF4??ActualCurrentState.ActualNativeColorStateF4;
    public byte DrawNativeRt0BlendEnable1C0=>Before.TemporaryNativeRt0BlendEnable1C0??ActualCurrentState.ActualNativeRt0BlendEnable1C0;
    public MapPostEffect3EyeGraphicsCurrentState RestoreExecuteState=>ActualCurrentState;
    public bool ExecuteChangesViewport=>false;
    public bool PreservesActualRemainingRasterFields=>true;
    public uint NativeScissorEnableOpcode=>0x10013;
    public uint NativeScissorRectangleOpcode=>0x10014;
    public IReadOnlyList<int> NativeColorStateAffectedSlots=>Array.AsReadOnly(Enumerable.Range(0,8).ToArray());
    public uint NativeRt0BlendEnableOpcode=>0x100000B;
    public ReadOnlyMemory<byte> NativeDrawCommand=>Convert.FromHexString("2D0003000000000003000000");
    public byte NativeExecuteReturnAl=>1;
    public bool ProvesGpuExecution=>false;
    public void RequireCurrent()
    {Before.Frame.RequireCurrent();if(ActualSelectedTarget.ActualTexture is{} target)table.ValidateCurrent(target);}
}

public sealed class LoadedMapPostEffect3Composite2ExecutionSource
{
    private readonly SkyWeatherNativePowerSource power;
    private readonly float degreesToRadians;
    private readonly Vector4[] aberrationColors;
    internal LoadedMapPostEffect3Composite2ExecutionSource(string evidence,SkyWeatherNativePowerSource power,float angle,Vector4[] colors)
        =>(SourceEvidence,this.power,degreesToRadians,aberrationColors)=(evidence,power,angle,colors.ToArray());
    public string SourceEvidence{get;}
    public string Locale=>power.Locale;
    public MapPostEffect3Composite2ExecutionFrame PrepareInitialization(
        MapPostEffect3ParameterState state,MapPostEffect3AllocationPlan allocation,
        MapPostEffect3TextureParameterState table,MapPostEffect3ParameterComposite2Update actualPrepared,
        MapPostEffect3Composite2ModuleLuts actualModuleLuts,MapPostEffect3ParameterAliasMode actualAliasMode,
        MapPostEffect3Composite2ExternalExtent? actualBloomExtent=null)
    {
        ArgumentNullException.ThrowIfNull(state);ArgumentNullException.ThrowIfNull(allocation);ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(actualPrepared);ArgumentNullException.ThrowIfNull(actualModuleLuts);actualPrepared.RequireCurrent();
        actualPrepared.RequireOwnedLifecycle(state,allocation,table);
        MapPostEffect3ParameterState.ValidateSource(allocation.Source);state.BindOwnedAllocation(allocation);
        if(actualPrepared.ConstantBuffer.Locale!=Locale||!ReferenceEquals(table.Allocation,allocation)||table.FrameNumber!=state.FrameNumber||state.OwnerIdentity!=actualPrepared.OwnerIdentity||
            state.FrameNumber!=actualPrepared.FrameNumber||actualPrepared.ConstantBuffer.SourceSha256!=state.SourceSha256||actualPrepared.NativeCapability1F0!=5)
            throw new InvalidDataException("Composite2 lifecycle requires this actual capability5 controller/scalar/table invocation.");
        ValidateLuts(state.FrameNumber,state.OwnerIdentity,actualModuleLuts);
        var values=MapPostEffect3ApplyCompleteInitialScalars.Composite2Defaults.ToDictionary(d=>d.Name,d=>state.RequireValue(d.Name).ToArray(),StringComparer.Ordinal);
        int Find(string name,int expected)
        {int index=state.FindResourceIndex(MapPostEffect3ParameterResourceTable.Texture,name,actualAliasMode)
            ??throw new InvalidDataException("Composite2 native Init texture name missing: "+name);
         if(index!=expected)throw new InvalidDataException("Composite2 source name/index changed: "+name);return index;}
        var aliases=new List<MapPostEffect3Composite2TextureAlias>
        {new(table,Find("PRECMP2InputColor",68),Find("PRECMP2OutputAddBloom",79),false,SourceEvidence+"Init-current68-to79",Locale)};
        uint mask=actualPrepared.PassSelection.NativeMask64;
        if((mask&0x3003)!=0)
        {
            bool cross=(mask&0x10200)!=0;
            aliases.Add(new(table,cross?Find("PRECMP2TargetBloom",77):Find("PRECMP2InputBloom",74),
                Find("PRECMP2InputAddBloom",78),true,SourceEvidence+"Init-copy-current-wrapper/side-reset-alternate",Locale));
        }
        var hdr=PrepareHdrRemake(state,allocation,values);
        var frame=new MapPostEffect3Composite2ExecutionFrame(this,state,allocation,table,actualPrepared,actualModuleLuts,values,aliases.AsReadOnly(),hdr);
        if(frame.RequiresCrossFilterConstants)
        {
            var bloom=frame.ActualInputBloom74;ushort width,height;
            if(bloom.Reference.PhysicalAllocationParameterIndex is int physical)
            {var a=allocation.TextureAllocations.Single(t=>t.Parameter.Index==physical);width=a.Width16;height=a.Height16;}
            else
            {
                if(actualBloomExtent is null||actualBloomExtent.ActualTexture!=bloom||string.IsNullOrWhiteSpace(actualBloomExtent.SourceEvidence))
                    throw new NotSupportedException("Positive Composite2 CrossFilter needs its actual external Bloom74 wrapper40/42 extent.");
                width=actualBloomExtent.Width40;height=actualBloomExtent.Height42;
            }
            frame.AcceptInitialCrossFilter(PrepareCrossFilterWithActualBloomExtent(frame,0,bloom,width,height,"actual Prepare InputBloom wrapper40/42"),width,height);
        }
        return frame;
    }
    public MapPostEffect3Composite2BeforeBindings PrepareBefore(
        MapPostEffect3Composite2ExecutionFrame frame,int actualSourcePassIndex,
        MapPostEffect3Composite2ModuleLuts actualCurrentModuleLuts)
    {
        ArgumentNullException.ThrowIfNull(frame);frame.RequireInitialized();ValidateLuts(frame.FrameNumber,frame.OwnerIdentity,actualCurrentModuleLuts);
        if(actualCurrentModuleLuts.ActualLinearWrapper30!=frame.ActualInitialModuleLuts.ActualLinearWrapper30||
            actualCurrentModuleLuts.ActualPointWrapper38!=frame.ActualInitialModuleLuts.ActualPointWrapper38)
            throw new InvalidDataException("Composite2 current ShaderManager carrier pointers changed after Init.");
        int ordinal=Array.IndexOf(DirectoryByCachedOrdinal,actualSourcePassIndex);
        if(ordinal<0||!frame.Prepared.PassSelection.OrderedFactory9Passes.Contains(actualSourcePassIndex))
            throw new InvalidDataException("Composite2 Before rejects an unselected native cached pass.");
        bool cross0=ordinal is 7 or 14,addBloom=ordinal is 0 or 1 or 12 or 13;
        if(cross0)frame.CaptureCross0Sampler(actualCurrentModuleLuts.ActualLinearSampler48);
        ushort? sampler=cross0?(ushort)(actualCurrentModuleLuts.ActualLinearSampler48|15):addBloom?
            (ushort)((actualCurrentModuleLuts.ActualLinearSampler48&0xFFF0)|frame.SavedHdrSamplerLow4):null;
        bool clear=(ordinal is 2 or 11)&&(frame.Prepared.PassSelection.NativeMask64&0x1E0078)==0;
        MapPostEffect3Composite2CrossFilterPatch? patch=ordinal is 8 or 15?
            PrepareCrossFilterWithActualBloomExtent(frame,1,frame.ActualInputBloom74,frame.ActualBloomWidth40,frame.ActualBloomHeight42,frame.SourceEvidence):
            ordinal is 9 or 16?PrepareCrossFilterWithActualBloomExtent(frame,2,frame.ActualInputBloom74,frame.ActualBloomWidth40,frame.ActualBloomHeight42,frame.SourceEvidence):null;
        return new(frame,actualSourcePassIndex,ordinal,actualCurrentModuleLuts,sampler,clear,cross0?15:null,patch,
            SourceEvidence+";Before-t7/t8-current-ShaderManager-linear/point-carriers;Select-then-Before-then-Bind;RT0-null-precedes-effective-target-list");
    }
    public MapPostEffect3Composite2CrossFilterPatch? PrepareCrossFilter(
        MapPostEffect3Composite2ExecutionFrame frame,int nativePhase)
    {
        ArgumentNullException.ThrowIfNull(frame);frame.RequireCurrent();
        if(nativePhase is <0 or >2)throw new InvalidDataException("Composite2 CrossFilter phase is outside its actual0/1/2 calls.");
        if(!frame.RequiresCrossFilterConstants)return null;
        // dereferences the actual PRECMP2InputBloom wrapper. It is
        // independent of Init78's selected77 result in the positive branch.
        var table=GetCurrentTable(frame);var bloom=table.Require(74);ushort width,height;
        if(bloom.Reference.PhysicalAllocationParameterIndex is not int physical)
            throw new NotSupportedException("Positive CrossFilter needs its actual current Bloom wrapper40/42 extent producer.");
        var texture=frame.Allocation.TextureAllocations.Single(t=>t.Parameter.Index==physical);width=texture.Width16;height=texture.Height16;
        if(width==0||height==0)throw new InvalidDataException("Composite2 CrossFilter wrapper extent is zero.");
        return PrepareCrossFilterWithActualBloomExtent(frame,nativePhase,bloom,width,height,"actual source-owned selected wrapper40/42");
    }
    public MapPostEffect3Composite2CrossFilterPatch? PrepareCrossFilterWithActualBloomExtent(
        MapPostEffect3Composite2ExecutionFrame frame,int nativePhase,
        MapPostEffect3ResolvedTextureParameter actualBloom,ushort actualWidth40,ushort actualHeight42,string actualExtentEvidence)
    {
        frame.RequireCurrent();ArgumentException.ThrowIfNullOrWhiteSpace(actualExtentEvidence);
        if(nativePhase is <0 or >2||actualBloom.ParameterIndex!=74||actualWidth40==0||actualHeight42==0)
            throw new InvalidDataException("Composite2 CrossFilter needs its actual phase and InputBloom74 dimensions.");
        GetCurrentTable(frame).ValidateCurrent(actualBloom);
        if(!frame.RequiresCrossFilterConstants)return null;
        RequireCb(frame.Allocation,23,0xE9A98E0A,160);
        float M(float a,float b)=>NativeFloat.Multiply(a,b);
        float D(float a,float b)=>NativeLogarithm.Divide(a,b);
        float A(float a,float b)=>NativeFloat.Add(a,b);
        float S(float a,float b)=>NativeFloat.Subtract(a,b);
        var direction=NativeTrigonometry.SinCos014590(M(frame.Float("CrossFilterInclination"),degreesToRadians));
        float rate=SkyWeatherNativePower.PowSse(6,nativePhase,power);
        float dx=M(M(direction.Sin,D(frame.Float("CrossFilterLength"),actualWidth40)),rate);
        float dy=M(M(direction.Cos,D(frame.Float("CrossFilterLength"),actualHeight42)),rate);
        byte[] bytes=new byte[148];frame.Value("CrossFilterWeight").Span.CopyTo(bytes.AsSpan(144,4));
        for(int sample=0;sample<6;sample++)
        {
            float strength=SkyWeatherNativePower.PowSse(frame.Float("CrossFilterAttenuation"),M(sample,rate),power);
            float aberration=frame.Float("CrossFilterAberration");
            for(int lane=0;lane<4;lane++)
            {
                float color=aberration>0&&nativePhase==1?
                    A(M(ColorComponent(aberrationColors[sample%6],lane),aberration),S(1,aberration)):1;
                BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(sample*16+lane*4),M(strength,color));
            }
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(96+sample*8),M(sample,dx));
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(100+sample*8),M(sample,dy));
        }
        return new(frame,nativePhase,bytes);
    }
    private static float ColorComponent(Vector4 color,int lane)=>lane switch
        {0=>color.X,1=>color.Y,2=>color.Z,_=>color.W};
    public MapPostEffect3Composite2GraphicsRecipe PrepareGraphics(
        MapPostEffect3Composite2BeforeBindings before,MapPostEffect3TextureParameterState actualTable,
        MapPostEffect3EyeGraphicsCurrentState actualAfterBindingState,
        MapPostEffect3EyeGraphicsSelectedTarget actualAfterBindingTarget,int actualEffectiveTargetCount)
    {
        ArgumentNullException.ThrowIfNull(before);ArgumentNullException.ThrowIfNull(actualTable);
        ArgumentNullException.ThrowIfNull(actualAfterBindingState);ArgumentNullException.ThrowIfNull(actualAfterBindingTarget);
        var frame=before.Frame;frame.RequireInitialized();frame.RequireTable(actualTable);
        if(!Sse.IsSupported||before.IsCompute||actualEffectiveTargetCount is <0 or >4||
            actualAfterBindingState.FrameNumber!=frame.FrameNumber||actualAfterBindingState.ContextOwnerIdentity!=frame.OwnerIdentity||
            string.IsNullOrWhiteSpace(actualAfterBindingState.OwnedFrontendIdentity)||string.IsNullOrWhiteSpace(actualAfterBindingState.ViewIdentity)||
            string.IsNullOrWhiteSpace(actualAfterBindingState.SourceEvidence)||string.IsNullOrWhiteSpace(actualAfterBindingTarget.SourceEvidence)||
            string.IsNullOrWhiteSpace(actualAfterBindingTarget.ActualTargetViewIdentity)||actualAfterBindingTarget.NativeCurrentWidth50<=0||
            actualAfterBindingTarget.NativeCurrentHeight54<=0||actualAfterBindingTarget.ActualInstanceIndex<0)
            throw new InvalidDataException("Composite2 graphics requires actual post-Bind target topology and frontend state.");
        var viewport=actualAfterBindingState.ActualViewport;
        if(new[]{viewport.Left,viewport.Top,viewport.Width,viewport.Height,viewport.MinimumDepth,viewport.MaximumDepth}.Any(x=>!float.IsFinite(x))||
            viewport.Width<=0||viewport.Height<=0)
            throw new InvalidDataException("Composite2 actual target viewport/depth range is invalid.");
        if(actualAfterBindingTarget.ActualTexture is{} target)
        {
            actualTable.ValidateCurrent(target);
            if(target.Reference.InstanceIndex!=actualAfterBindingTarget.ActualInstanceIndex)
                throw new InvalidDataException("Composite2 bound target side differs from its current wrapper.");
        }
        var directoryTarget=frame.Allocation.Passes.Single(p=>p.Pass.Index==before.SourcePassIndex).ActiveBindings
            .Where(b=>b.Role==MapPostEffect3BindingRole.RenderTargetWrite&&b.RegisterIndex==0).DistinctBy(b=>b.ParameterIndex).ToArray();
        if(directoryTarget.Length>1)throw new InvalidDataException("Composite2 native directory has multiple RT0 pointers.");
        if(!before.ClearsNativeTarget0&&directoryTarget.Length==1)
        {
            var expected=actualTable.Require(directoryTarget[0].ParameterIndex);
            if(actualEffectiveTargetCount==0||!ReferenceEquals(actualAfterBindingTarget.ActualTexture?.Reference,expected.Reference))
                throw new InvalidDataException("Composite2 actual Bind did not select the source directory's current RT0.");
        }
        // A cleared RT0 is not proof that RT1..3 are also empty. The caller
        // supplies the actual effective target count and complete raster state.
        FinalEffectScissor? scissor=actualAfterBindingState.ActualScissor;
        if(before.ChangesScissor)
        {
            string scalar=before.UsesViewportScalarForScissor?"Viewport":"CompositeLastViewport";
            int X(int extent,int lane)=>Sse.ConvertToInt32WithTruncation(Vector128.CreateScalar(NativeFloor(
                NativeFloat.Multiply(extent,frame.Float(scalar,lane)))));
            scissor=new(X(actualAfterBindingTarget.NativeCurrentWidth50,0),X(actualAfterBindingTarget.NativeCurrentHeight54,1),
                X(actualAfterBindingTarget.NativeCurrentWidth50,2),X(actualAfterBindingTarget.NativeCurrentHeight54,3));
        }
        return new(before,actualTable,actualAfterBindingState,actualAfterBindingTarget,actualEffectiveTargetCount,scissor);
    }
    private static MapPostEffect3TextureParameterState GetCurrentTable(MapPostEffect3Composite2ExecutionFrame frame)=>frame.CurrentTextureTable;
    private MapPostEffect3ParameterConstantBuffer? PrepareHdrRemake(MapPostEffect3ParameterState state,
        MapPostEffect3AllocationPlan allocation,IReadOnlyDictionary<string,byte[]> values)
    {
        if(BinaryPrimitives.ReadInt32LittleEndian(values["EnableSeparateHdrLut"])==0)return null;
        RequireCb(allocation,25,0x257141D7,48);byte[] bytes=new byte[48];
        float Unsigned(string n)=>BinaryPrimitives.ReadUInt32LittleEndian(values[n]);
        BinaryPrimitives.WriteSingleLittleEndian(bytes,NativeLogarithm.Divide(Unsigned("HdrTvNit"),100));
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(4),NativeLogarithm.Divide(Unsigned("BaseTvNit"),100));
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(8),Unsigned("TonemapType"));
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(12),Unsigned("ColorGamut"));
        values["NeutralTonemapParams0"].CopyTo(bytes,16);values["NeutralTonemapParams1"].CopyTo(bytes,32);
        return new(state,allocation.ConstantBufferParameters[25],MapPostEffect3ConstantBufferWriter.LightInfo,bytes,Locale);
    }
    private static void RequireCb(MapPostEffect3AllocationPlan allocation,int index,uint hash,uint size)
    {var cb=allocation.ConstantBufferParameters[index];if(cb.Index!=index||cb.NameHash!=hash||cb.ByteCount!=size||
        cb.Ownership!=MapPostEffect3AllocationOwnership.OwnedDeclaration)throw new InvalidDataException("Composite2 native CB declaration differs.");}
    private static void ValidateLuts(long frame,string owner,MapPostEffect3Composite2ModuleLuts luts)
    {if(luts.FrameNumber!=frame||luts.ContextOwnerIdentity!=owner||luts.ActualLinearWrapper30.FrameNumber!=frame||
        string.IsNullOrWhiteSpace(luts.ActualLinearWrapper30.OwnerIdentity)||string.IsNullOrWhiteSpace(luts.SourceEvidence)||
        luts.ActualLdrWrapper38 is null||luts.ActualPointWrapper38.FrameNumber!=frame||
        string.IsNullOrWhiteSpace(luts.ActualPointWrapper38.OwnerIdentity))
        throw new InvalidDataException("Composite2 needs both actual same-frame ShaderManager linear/point sampler carriers.");}
    private static float NativeFloor(float value)
    {
        uint bits=BitConverter.SingleToUInt32Bits(value),magnitude=bits&0x7FFFFFFF;bool negative=(bits&0x80000000)!=0;
        if(magnitude>=0x4B800000){if(magnitude>0x7F800000)throw new NotSupportedException("Composite2 native NaN floor needs its actual exception producer.");return value;}
        if(magnitude<0x3F800000)return magnitude==0?value:negative?-1:0;
        int shift=(0x96-(int)(bits>>23))&31;uint truncated=bits&~((1u<<shift)-1);float result=BitConverter.UInt32BitsToSingle(truncated);
        return negative&&truncated!=bits?NativeFloat.Subtract(result,1):result;
    }
    private static readonly int[] DirectoryByCachedOrdinal=[98,100,108,109,110,111,112,92,94,96,106,107,99,101,93,95,97,113,114,115,116];
}

public static class MapPostEffect3Composite2ExecutionSource
{
    public static LoadedMapPostEffect3Composite2ExecutionSource Load(GameResourceRepository repository,string locale="TC")
    {ArgumentNullException.ThrowIfNull(repository);return RenderSamplerSourceAdapters.ReadComposite2ExecutionSource(
        RenderSamplerSourceAdapters.Load(repository.SourceDirectory,locale),SkyWeatherNativePowerSource.Load(repository,locale));}
}

public static partial class RenderSamplerSourceAdapters
{
    internal static LoadedMapPostEffect3Composite2ExecutionSource ReadComposite2ExecutionSource(
        LoadedDeferredSamplerNativeSource native,SkyWeatherNativePowerSource power)
    {
        if(power.Locale!=native.Locale)throw new NotSupportedException("Composite2 lifecycle and power require the same locale.");
        const float degreesToRadians=.017453292f;
        Vector4[] aberrationColors=[new(1,0,0,0),new(.5f,0,.5f,0),new(0,0,1,0),
            new(0,.5f,.5f,0),new(0,1,0,0),new(.5f,.5f,0,0)];
        return new(RenderRuleIdentity.For("composite2-execution",native.Locale)+
            ";current-wrapper-and-initialization-order;cross-filter-six-colors;scissor-not-viewport",
            power,degreesToRadians,aberrationColors);
    }
}
