using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace San14.Data;

public sealed record MapPostEffect3DepthOfField2ExternalExtent(
    MapPostEffect3ResolvedTextureParameter ActualTexture,ushort Width40,ushort Height42,string SourceEvidence);

public sealed class MapPostEffect3DepthOfField2Inputs
{
    internal readonly MapPostEffect3ParameterState State;
    internal readonly MapPostEffect3TextureParameterState Table;
    internal readonly IReadOnlyList<(int Index,MapPostEffect3ResolvedTextureParameter? Pointer,long? NullRevision)> Captured;
    internal readonly IReadOnlyDictionary<string,(bool Present,ushort Width,ushort Height)> Extents;
    internal MapPostEffect3DepthOfField2Inputs(MapPostEffect3ParameterState state,
        MapPostEffect3TextureParameterState table,
        IReadOnlyList<(int,MapPostEffect3ResolvedTextureParameter?,long?)> captured,
        IReadOnlyDictionary<string,(bool,ushort,ushort)> extents,uint context,uint mode,string evidence)
        =>(State,Table,Captured,Extents,ActualContext1C48Word,ActualDescriptorMode58,SourceEvidence)=
            (state,table,captured,extents,context,mode,evidence);
    public long FrameNumber=>State.FrameNumber;
    public uint ActualContext1C48Word{get;}
    public uint ActualDescriptorMode58{get;}
    public string SourceEvidence{get;}
    internal void RequireCurrent(MapPostEffect3ParameterState state,MapPostEffect3AllocationPlan allocation,
        bool sourceShapeWriterMayHaveReplacedPointer=false)
    {
        if(!ReferenceEquals(State,state)||!ReferenceEquals(Table.Allocation,allocation)||Table.FrameNumber!=state.FrameNumber)
            throw new InvalidDataException("DOF2 wrapper inputs belong to another current invocation.");
        foreach(var c in Captured)
        {
            if(sourceShapeWriterMayHaveReplacedPointer&&c.Index==58)continue;
            if(c.Pointer is null)Table.ValidateKnownNullRevision(c.Index,c.NullRevision!.Value);else Table.ValidateCurrent(c.Pointer);
        }
    }
}

/// <summary>Owned factory7 fields plus its two distinct source global caches.
/// These are fresh construction or prior recovered local invocations; no
/// running original controller/global observation is implied.</summary>
public sealed class MapPostEffect3DepthOfField2Controller
{
    internal MapPostEffect3DepthOfField2Controller(MapPostEffect3AllocationPlan allocation,long frame,
        uint count,bool flip,uint spriteSide,bool shape4k,bool shapeMulti,uint shapeHeight,
        bool init4k,bool initMulti,uint initHeight,bool cropped,string evidence,string locale="TC")
        =>(Allocation,FrameNumber,Count10,HistoryFlip16,SpriteSide28,ShapeFix4K,ShapeMultiResolution,ShapeHeight,
           InitializeFix4K,InitializeMultiResolution,InitializeHeight,UsesCroppedViewport168,SourceEvidence,Locale)=
            (allocation,frame,count,flip,spriteSide,shape4k,shapeMulti,shapeHeight,init4k,initMulti,initHeight,cropped,evidence,locale);
    internal MapPostEffect3AllocationPlan Allocation{get;}
    public long FrameNumber{get;}
    public uint Count10{get;}
    public bool HistoryFlip16{get;}
    public uint SpriteSide28{get;}
    public bool ShapeFix4K{get;}
    public bool ShapeMultiResolution{get;}
    public uint ShapeHeight{get;}
    public bool InitializeFix4K{get;}
    public bool InitializeMultiResolution{get;}
    public uint InitializeHeight{get;}
    public bool UsesCroppedViewport168{get;}
    public bool RearScatterEnabled14{get;internal set;}
    public bool ForeScatterEnabled15{get;internal set;}
    public string SourceEvidence{get;}
    public string Locale{get;}
    public uint NativeCapability160=>2;
    /// <summary>Only native's decrement. The caller must have reached
    /// the actual finish hook in its owned source protocol. No pass success or
    /// GPU completion is inferred by advancing this numeric field.</summary>
    public MapPostEffect3DepthOfField2Controller ApplySourceFinish(bool actualFinishHookReached)
    {
        if(!actualFinishHookReached)throw new InvalidDataException("DOF2 source finish hook was not reached.");
        return new(Allocation,FrameNumber,Count10==0?0:Count10-1,HistoryFlip16,SpriteSide28,
            ShapeFix4K,ShapeMultiResolution,ShapeHeight,InitializeFix4K,InitializeMultiResolution,InitializeHeight,
            UsesCroppedViewport168,SourceEvidence+";-owned-source-finish-count10",Locale)
            {RearScatterEnabled14=RearScatterEnabled14,ForeScatterEnabled15=ForeScatterEnabled15};
    }
}

public sealed record MapPostEffect3DepthOfField2ShapeRequirement(bool SourceShapeChanged,
    uint RequestedSide,int ActualShapeTextureParameterIndex)
{
    public bool RequiresShapeTextureValidationOrAllocation=>SourceShapeChanged;
    public uint RequiredFlags46Mask=>0x7C00;
    public uint RequiredFlags46Value=>0x400;
    public uint RequiredFlags46Low7=>3;
    public bool WritesCb19=>false; //Actual capability2 branch uses DOF2ShapeInfo.
    public uint RequiredHeight=>RequestedSide;
    public uint RequiredWidth=>RequestedSide;
    public int RequiredMipCount=>1;
    public uint RequiredNativeFormat=>3;
}

public sealed record MapPostEffect3DepthOfField2PassSelection(uint NativeMaskA8,
    IReadOnlyList<int> OrderedFactory7Passes)
{
    // The actual capability2 VS15..18/PS18..21 neither declares nor reads b3.
    // Their counted CB19 setter remains present; its unwritten bytes cannot
    // be manufactured into a complete recovered constant-buffer packet.
    public bool HasNativeUnusedCb19Setter=>OrderedFactory7Passes.Any(p=>p is >=68 and <=71);
    public bool RequiresSeparateCb19Producer=>false;
}

public sealed record MapPostEffect3DepthOfField2SpriteCountReadback(long FrameNumber,
    string ContextOwnerIdentity,uint ActualStagingSide28,bool NativeMapReturnedNull,
    uint RearCountAt04,uint ForeCountAt14,string SourceEvidence);

public sealed record MapPostEffect3DepthOfField2PixelTextureSetter(int NativeRegisterIndex,
    MapPostEffect3ResolvedTextureParameter ActualTexture,string Locale="TC")
{
    public uint NativeStageCode=>2;
    public MapPostEffect3ResourceSetter TextureSetter=>MapPostEffect3ResourceSetter.ShaderResource;
}

public enum MapPostEffect3DepthOfField2DynamicBindingKind { PixelTexture,RenderTarget }
public sealed record MapPostEffect3DepthOfField2DynamicBinding(
    MapPostEffect3DepthOfField2DynamicBindingKind Kind,int NativeRegisterIndex,
    MapPostEffect3ResolvedTextureParameter ActualTexture,string Locale="TC")
{
    public MapPostEffect3ResourceSetter TextureSetter=>MapPostEffect3ResourceSetter.ShaderResource;
}

public sealed class MapPostEffect3DepthOfField2BeforeBindings
{
    private readonly MapPostEffect3ParameterDepthOfField2Update update;
    private readonly MapPostEffect3TextureParameterState table;
    internal MapPostEffect3DepthOfField2BeforeBindings(MapPostEffect3ParameterDepthOfField2Update update,
        MapPostEffect3TextureParameterState table,int pass,
        IReadOnlyList<MapPostEffect3DepthOfField2PixelTextureSetter> textures,
        MapPostEffect3ResolvedTextureParameter? target,bool readback,bool clearOverride)
        =>(this.update,this.table,SourcePassIndex,OrderedPixelTextureSetters,ActualTarget0,
            UsedNativeSpriteCountReadback,RequiresNativeClearMaskAndColorOverride)=
            (update,table,pass,textures,target,readback,clearOverride);
    public int SourcePassIndex{get;}
    public long FrameNumber=>update.FrameNumber;
    public string ContextOwnerIdentity=>update.ConstantBuffer.OwnerIdentity;
    public IReadOnlyList<MapPostEffect3DepthOfField2PixelTextureSetter> OrderedPixelTextureSetters{get;}
    public IReadOnlyList<MapPostEffect3DepthOfField2DynamicBinding> OrderedDynamicBindings
    {
        get
        {
            var result=new List<MapPostEffect3DepthOfField2DynamicBinding>();
            if(SourcePassIndex==67&&OrderedPixelTextureSetters.Count!=0)
                result.Add(new(MapPostEffect3DepthOfField2DynamicBindingKind.PixelTexture,1,OrderedPixelTextureSetters[0].ActualTexture,update.ConstantBuffer.Locale));
            if(ActualTarget0 is not null)result.Add(new(MapPostEffect3DepthOfField2DynamicBindingKind.RenderTarget,0,ActualTarget0,update.ConstantBuffer.Locale));
            foreach(var texture in OrderedPixelTextureSetters.Skip(SourcePassIndex==67?1:0))
                result.Add(new(MapPostEffect3DepthOfField2DynamicBindingKind.PixelTexture,texture.NativeRegisterIndex,texture.ActualTexture,update.ConstantBuffer.Locale));
            return result.AsReadOnly();
        }
    }
    public MapPostEffect3ResolvedTextureParameter? ActualTarget0{get;}
    public int NativeTargetRegister=>0;
    public int NativeTargetMip=>-1;
    public int NativeTargetArraySlice=>-1;
    public MapPostEffect3ResourceSetter TargetSetter=>MapPostEffect3ResourceSetter.RenderTarget;
    public IReadOnlyList<int> PixelSamplerRegisters=>Array.AsReadOnly(new[]{7,8});
    public bool UsedNativeSpriteCountReadback{get;}
    // writes a qword: DWORD clear mask1C18=15 and packed RGBA8
    //clear color1C1C=0. consumes them during target Bind.
    public bool RequiresNativeClearMaskAndColorOverride{get;}
    public uint? NativeClearMaskContext1C18=>RequiresNativeClearMaskAndColorOverride?15u:null;
    public uint? NativeClearColorRgba8Context1C1C=>RequiresNativeClearMaskAndColorOverride?0u:null;
    public bool RequiresGenericComputeDispatch=>SourcePassIndex is >=57 and <=61;
    public bool RequiresNativeIndirectDraw=>SourcePassIndex is >=62 and <=65;
    public uint NativeIndirectDrawOpcode=>0x40032;
    public int NativeIndirectArgumentByteOffset=>SourcePassIndex is 62 or 64?0:16;
    public int? NativeIndirectBufferParameterIndex=>RequiresNativeIndirectDraw?update.RequireSpriteInstanceBufferIndex():null;
    public bool RequiresSourceStagingBufferCopy=>RequiresNativeIndirectDraw;
    public uint NativeStagingCopyOpcode=>0x50;
    public uint ActualStagingSide28=>update.NextController.SpriteSide28;
    public bool RequiresTemporaryBlendOverride=>RequiresNativeIndirectDraw||SourcePassIndex is >=72 and <=83;
    public uint? TemporaryNativeColorStateF4=>RequiresNativeIndirectDraw?0x111100u:
        SourcePassIndex is >=72 and <=83?0x441100u:null;
    public byte? TemporaryNativeRt0BlendEnable1C0=>RequiresTemporaryBlendOverride?(byte)1:null;
    public bool RestoreActualPreviousBlendState=>RequiresTemporaryBlendOverride;
    public bool RequiresNativeScissorOverride=>!RequiresGenericComputeDispatch&&SourcePassIndex!=53;
    public bool RestoreActualPreviousScissor=>RequiresNativeScissorOverride;
    public ReadOnlyMemory<byte> NativeDrawCommand=>RequiresGenericComputeDispatch||RequiresNativeIndirectDraw?
        ReadOnlyMemory<byte>.Empty:Convert.FromHexString("2D0003000000000003000000");
    public string SourceEvidence=>update.SourceEvidence+"-original-ordered-Before";
    public void RequireCurrent()
    {
        update.RequireCurrent();
        foreach(var setter in OrderedPixelTextureSetters)table.ValidateCurrent(setter.ActualTexture);
        if(ActualTarget0 is not null)table.ValidateCurrent(ActualTarget0);
    }
    public void RequirePrepared(MapPostEffect3ParameterDepthOfField2Update actual)
    {
        if(!ReferenceEquals(update,actual))
            throw new InvalidDataException("DOF2 Before requires the exact source Prepare/Initialize receipt that produced it.");
        RequireCurrent();
    }
}

public sealed class MapPostEffect3ParameterDepthOfField2Update
{
    private readonly MapPostEffect3ParameterState state;
    private readonly MapPostEffect3DepthOfField2Inputs inputs;
    private readonly IReadOnlyDictionary<string,byte[]> scalars;
    internal MapPostEffect3ParameterDepthOfField2Update(MapPostEffect3ParameterState state,
        MapPostEffect3DepthOfField2Inputs inputs,IReadOnlyDictionary<string,byte[]> scalars,
        MapPostEffect3ParameterConstantBuffer cb,MapPostEffect3DepthOfField2Controller controller,
        MapPostEffect3DepthOfField2ShapeRequirement shape,MapPostEffect3DepthOfField2PassSelection selection,
        int aliasSource,int aliasTarget,string evidence,MapPostEffect3DepthOfField2Controller prior)
        =>(this.state,this.inputs,this.scalars,ConstantBuffer,NextController,ShapeRequirement,PassSelection,
            InitializeAliasSourceIndex,InitializeAliasTargetIndex,SourceEvidence,PreviousController)=
            (state,inputs,scalars,cb,controller,shape,selection,aliasSource,aliasTarget,evidence,prior);
    public long FrameNumber=>state.FrameNumber;
    public MapPostEffect3ParameterConstantBuffer ConstantBuffer{get;}
    public MapPostEffect3DepthOfField2Controller NextController{get;}
    public MapPostEffect3DepthOfField2Controller PreviousController{get;}
    public MapPostEffect3DepthOfField2ShapeRequirement ShapeRequirement{get;}
    public MapPostEffect3DepthOfField2PassSelection PassSelection{get;}
    public int InitializeAliasSourceIndex{get;}
    public int InitializeAliasTargetIndex{get;}
    public bool RequiresInitializeTextureAliasProducer=>true;
    public MapPostEffect3DepthOfField2InitializationReceipt? AcceptedInitialization{get;private set;}
    public string InitializeAliasSourceName=>"DOF2InputColor";
    public string InitializeAliasTargetName=>"DOF2OutputColor";
    public bool PrepareAndInitializeReturn=>true;
    public bool RequiresPerPassHistoryBindings=>PassSelection.OrderedFactory7Passes.Count!=0;
    public bool RequiresSpriteCountReadback=>NextController.Count10==0&&PassSelection.OrderedFactory7Passes.Contains(57);
    public string SourceEvidence{get;}
    public bool ProvesGpuExecution=>false;
    public bool NativeAfterIsNoOp=>true;
    public bool BeforeHookAllows(int actualPassIndex)
    {
        RequireCurrent();
        if(actualPassIndex is <53 or >83)throw new InvalidDataException("DOF2 source before gate requires a factory7 pass.");
        return PassSelection.OrderedFactory7Passes.Contains(actualPassIndex);
    }
    public void RequireCurrent()
    {
        inputs.RequireCurrent(state,NextController.Allocation,true);
        foreach(var v in scalars)if(!state.RequireValue(v.Key).Span.SequenceEqual(v.Value))
            throw new InvalidDataException("DOF2 scalar changed after its original Prepare/Initialize: "+v.Key);
    }
    public void RequireCurrentTextureTable(MapPostEffect3TextureParameterState actualTable)
    {
        if(!ReferenceEquals(actualTable,inputs.Table))
            throw new InvalidDataException("DOF2 current invocation requires the exact texture table captured by source Prepare.");
        RequireCurrent();
    }
    public void RequireCurrentScalarState(MapPostEffect3ParameterState actualState)
    {
        if(!ReferenceEquals(actualState,state))
            throw new InvalidDataException("DOF2 current invocation requires the exact scalar state captured by source Prepare.");
        RequireCurrent();
    }
    public void RequireInitializationPending()
    {
        RequireCurrent();
        if(AcceptedInitialization is not null)
            throw new InvalidDataException("DOF2 actual source Init alias was already applied for this Prepare/Initialize receipt.");
    }
    internal void AcceptActualInitialization(MapPostEffect3TextureParameterState actualTable)
    {
        RequireCurrentTextureTable(actualTable);RequireInitializationPending();
        var from=actualTable.ReadKnownNullablePointer(InitializeAliasSourceIndex) is null?null:actualTable.Require(InitializeAliasSourceIndex);
        var to=actualTable.ReadKnownNullablePointer(InitializeAliasTargetIndex) is null?null:actualTable.Require(InitializeAliasTargetIndex);
        if(from?.Reference!=to?.Reference)
            throw new InvalidDataException("DOF2 source Init did not copy its actual input0 wrapper/side/mip pointer to output68.");
        AcceptedInitialization=new(this,actualTable,from,to,
            from is null?actualTable.CaptureKnownNullRevision(InitializeAliasSourceIndex):null,
            to is null?actualTable.CaptureKnownNullRevision(InitializeAliasTargetIndex):null);
    }
    public void RequireActualInitialization(MapPostEffect3TextureParameterState actualTable)
    {
        RequireCurrentTextureTable(actualTable);
        (AcceptedInitialization??throw new InvalidDataException("DOF2 actual source Init alias has not been applied.")).RequireCurrent(actualTable);
    }
    internal int RequireSpriteInstanceBufferIndex()=>state.FindResourceIndex(MapPostEffect3ParameterResourceTable.Buffer,
        "DOF2SpriteInstanceInfo",MapPostEffect3ParameterAliasMode.SourceAsciiZeroLocale)
        ??throw new InvalidDataException("DOF2 original indirect SpriteInstanceInfo buffer lookup absent.");
    public MapPostEffect3DepthOfField2BeforeBindings PrepareBefore(int sourcePassIndex,
        MapPostEffect3DepthOfField2SpriteCountReadback? actualReadback=null)
    {
        RequireCurrent();
        if(!BeforeHookAllows(sourcePassIndex))throw new InvalidDataException("DOF2 original mask rejects this Before invocation.");
        var table=inputs.Table;
        MapPostEffect3ResolvedTextureParameter Texture(string name)
        {
            int index=state.FindResourceIndex(MapPostEffect3ParameterResourceTable.Texture,name,
                MapPostEffect3ParameterAliasMode.SourceAsciiZeroLocale)
                ??throw new InvalidDataException("DOF2 actual Before texture lookup absent: "+name);
            return table.Require(index);
        }
        var setters=new List<MapPostEffect3DepthOfField2PixelTextureSetter>();
        MapPostEffect3ResolvedTextureParameter? target=null;
        bool flip=NextController.HistoryFlip16;
        if(sourcePassIndex is 66 or 67)
        {
            var current=Texture(flip?"DOF2CocHistory1":"DOF2CocHistory0");
            target=current;
            if(sourcePassIndex==67)
            {
                setters.Add(new(1,Texture(flip?"DOF2CocHistory0":"DOF2CocHistory1"),ConstantBuffer.Locale));
                if((PassSelection.NativeMaskA8&0x800)==0)setters.Add(new(1,Texture("DOF2PrefilteredCoc"),ConstantBuffer.Locale));
            }
        }
        // The original repeats cached+80 (pass69), so pass71 has no override.
        if(sourcePassIndex is 68 or 69 or 70)setters.Add(new(1,Texture(flip?"DOF2CocHistory1":"DOF2CocHistory0"),ConstantBuffer.Locale));
        bool readback=false;
        if(sourcePassIndex==57)
        {
            if(NextController.Count10!=0)
            {NextController.RearScatterEnabled14=true;NextController.ForeScatterEnabled15=true;}
            else
            {
                if(actualReadback is null||actualReadback.FrameNumber!=FrameNumber||
                    actualReadback.ContextOwnerIdentity!=ConstantBuffer.OwnerIdentity||
                    actualReadback.ActualStagingSide28!=NextController.SpriteSide28||string.IsNullOrWhiteSpace(actualReadback.SourceEvidence))
                    throw new NotSupportedException("DOF2 Before57 needs its actual current side28 staging-buffer map/readback result.");
                NextController.RearScatterEnabled14=actualReadback.NativeMapReturnedNull||actualReadback.RearCountAt04!=0;
                NextController.ForeScatterEnabled15=actualReadback.NativeMapReturnedNull||actualReadback.ForeCountAt14!=0;
                readback=true;
            }
        }
        else if(actualReadback is not null)throw new InvalidDataException("DOF2 sprite counters are read only in source Before57.");
        bool clear=(sourcePassIndex is 62 or 64)&&NextController.RearScatterEnabled14||
            (sourcePassIndex is 63 or 65)&&NextController.ForeScatterEnabled15;
        return new(this,table,sourcePassIndex,setters.AsReadOnly(),target,readback,clear);
    }
}

/// <summary>Only the actual texture-table Init writer creates this receipt.
/// It retains the exact source/destination wrapper revisions, including an
/// explicit native null copy. Adoption performs no second source assignment.</summary>
public sealed class MapPostEffect3DepthOfField2InitializationReceipt
{
    private readonly MapPostEffect3ParameterDepthOfField2Update update;
    private readonly MapPostEffect3TextureParameterState table;
    private readonly long? sourceNullRevision,targetNullRevision;
    internal MapPostEffect3DepthOfField2InitializationReceipt(MapPostEffect3ParameterDepthOfField2Update update,
        MapPostEffect3TextureParameterState table,MapPostEffect3ResolvedTextureParameter? source,
        MapPostEffect3ResolvedTextureParameter? target,long? sourceNullRevision,long? targetNullRevision)
        =>(this.update,this.table,ActualSource,ActualTarget,this.sourceNullRevision,this.targetNullRevision)=
            (update,table,source,target,sourceNullRevision,targetNullRevision);
    public MapPostEffect3ResolvedTextureParameter? ActualSource{get;}
    public MapPostEffect3ResolvedTextureParameter? ActualTarget{get;}
    public long FrameNumber=>update.FrameNumber;
    public string SourceEvidence=>update.SourceEvidence+"-actual-Init-copy0-to68;exact-current-pointer-revisions";
    public void RequireCurrent(MapPostEffect3TextureParameterState actualTable)
    {
        update.RequireCurrentTextureTable(actualTable);
        if(!ReferenceEquals(actualTable,table)||!ReferenceEquals(update.AcceptedInitialization,this))
            throw new InvalidDataException("DOF2 Init receipt belongs to another table or source invocation.");
        if(ActualSource is{} from)table.ValidateCurrent(from);else table.ValidateKnownNullRevision(update.InitializeAliasSourceIndex,sourceNullRevision!.Value);
        if(ActualTarget is{} to)table.ValidateCurrent(to);else table.ValidateKnownNullRevision(update.InitializeAliasTargetIndex,targetNullRevision!.Value);
    }
}

public sealed class LoadedMapPostEffect3DepthOfField2Source
{
    internal LoadedMapPostEffect3DepthOfField2Source(string evidence,string locale="TC")=>(SourceEvidence,Locale)=(evidence,locale);
    public string Locale{get;}
    public string SourceEvidence{get;}
    public string RuleVersion=>MapPostEffect3ParameterDepthOfField2.RuleVersion;
    public MapPostEffect3DepthOfField2Controller ConstructFreshController(
        MapPostEffect3ParameterState actualState,MapPostEffect3AllocationPlan allocation)
    {
        Validate(actualState,allocation);ReadScalars(actualState);
        return new(allocation,0,MapPostEffect3ParameterDepthOfField2.FreshHistoryCount,false,0,
            false,false,MapPostEffect3ParameterDepthOfField2.FreshShapeHeight,
            false,false,MapPostEffect3ParameterDepthOfField2.FreshInitializeHeight,false,
            SourceEvidence+";fresh-controller;typed-scalars;capability2",Locale);
    }
    public MapPostEffect3DepthOfField2Inputs CaptureCurrentInputs(
        MapPostEffect3ParameterState state,MapPostEffect3AllocationPlan allocation,
        MapPostEffect3TextureParameterState table,uint actualContext1C48Word,uint actualDescriptorMode58,
        IReadOnlyList<MapPostEffect3DepthOfField2ExternalExtent> actualExternalExtents,
        MapPostEffect3ParameterAliasMode actualAliasMode,string evidence)
    {
        ArgumentNullException.ThrowIfNull(table);ArgumentNullException.ThrowIfNull(actualExternalExtents);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidence);Validate(state,allocation);state.BindOwnedAllocation(allocation);
        if(!ReferenceEquals(table.Allocation,allocation)||table.FrameNumber!=state.FrameNumber)
            throw new InvalidDataException("DOF2 texture table is not the current owned allocation.");
        var captured=new List<(int,MapPostEffect3ResolvedTextureParameter?,long?)>();
        var extents=new Dictionary<string,(bool,ushort,ushort)>();
        foreach(string name in new[]{"DOF2SpriteDofAccumRear","DOF2InputColor","DOF2PrefilteredColor","DOF2PreComposite","DOF2ShapeInfo"})
        {
            int index=state.FindResourceIndex(MapPostEffect3ParameterResourceTable.Texture,name,actualAliasMode)
                ??throw new NotSupportedException("DOF2 original texture name is absent: "+name);
            if(table.ReadKnownNullablePointer(index) is null)
            {captured.Add((index,null,table.CaptureKnownNullRevision(index)));extents[name]=(false,1,1);continue;}
            var pointer=table.Require(index);captured.Add((index,pointer,null));
            if(pointer.Reference.PhysicalAllocationParameterIndex is int physical)
            {
                var texture=allocation.TextureAllocations.Single(a=>a.Parameter.Index==physical);
                extents[name]=(true,texture.Width16,texture.Height16);continue;
            }
            var supplied=actualExternalExtents.Where(e=>e.ActualTexture.ParameterIndex==index).ToArray();
            if(supplied.Length!=1||string.IsNullOrWhiteSpace(supplied[0].SourceEvidence))
                throw new NotSupportedException("DOF2 actual external wrapper lacks unique extent fields: "+name);
            table.ValidateCurrent(supplied[0].ActualTexture);
            if(supplied[0].ActualTexture!=pointer)throw new InvalidDataException("DOF2 external extent pointer differs.");
            extents[name]=(true,supplied[0].Width40,supplied[0].Height42);
        }
        return new(state,table,captured.AsReadOnly(),extents,actualContext1C48Word,actualDescriptorMode58,SourceEvidence+";"+evidence);
    }

    /// <summary>Original prepare18 executes shape1156E20 then CB1156550;
    /// init20 executes and its separate alias. Capability2 writes CB18
    /// and consumes a shape texture; it never initializes CB19.</summary>
    public MapPostEffect3ParameterDepthOfField2Update PrepareAndInitialize(
        MapPostEffect3ParameterState state,MapPostEffect3AllocationPlan allocation,
        MapPostEffect3DepthOfField2Inputs inputs,MapPostEffect3DepthOfField2Controller prior)
    {
        Validate(state,allocation);ArgumentNullException.ThrowIfNull(inputs);ArgumentNullException.ThrowIfNull(prior);
        inputs.RequireCurrent(state,allocation);
        if(prior.Locale!=Locale||!ReferenceEquals(prior.Allocation,allocation)||prior.FrameNumber>=state.FrameNumber)
            throw new InvalidDataException("DOF2 controller is stale or belongs to another allocation.");
        if(!Sse.IsSupported)throw new NotSupportedException("DOF2 source SSE conversion/max backend is unavailable.");
        var values=ReadScalars(state);
        float F(string n,int word=0)=>BinaryPrimitives.ReadSingleLittleEndian(values[n].AsSpan(word*4));
        int I(string n)=>BinaryPrimitives.ReadInt32LittleEndian(values[n]);
        bool fix4k=I("Enable4KResFix")==1,multi=I("EnableMultiResFix")==1;
        float height=allocation.ContextHeight;uint nativeHeight=allocation.ContextHeight;
        bool shapeChanged=I("ChangeFStop")!=0||prior.ShapeFix4K!=fix4k||prior.ShapeMultiResolution!=multi||prior.ShapeHeight!=nativeHeight;
        int oddSide=multi?unchecked((int)nativeHeight)/120:9;if((oddSide&1)==0)oddSide++;
        int shapeIndex=state.FindResourceIndex(MapPostEffect3ParameterResourceTable.Texture,"DOF2ShapeInfo",
            MapPostEffect3ParameterAliasMode.SourceAsciiZeroLocale)!.Value;
        var shape=new MapPostEffect3DepthOfField2ShapeRequirement(shapeChanged,unchecked((uint)oddSide),shapeIndex);
        var rear=inputs.Extents["DOF2SpriteDofAccumRear"];var scene=inputs.Extents["DOF2InputColor"];
        var prefiltered=inputs.Extents["DOF2PrefilteredColor"];var preComposite=inputs.Extents["DOF2PreComposite"];
        float invRear=rear.Present?Div(1,rear.Height):1;
        float invSceneW=scene.Present?Div(1,scene.Width):1,invSceneH=scene.Present?Div(1,scene.Height):1;
        float invPreW=prefiltered.Present?Div(1,prefiltered.Width):1,invPreH=prefiltered.Present?Div(1,prefiltered.Height):1;
        float resolutionScale=multi?Div(height,1080):1,gather=Mul(resolutionScale,F("GatherMaxCoC"));
        int gatherType=Math.Clamp(I("GatherSizeType"),0,4);
        float gatherStep=Div(Mul(gather,4),gatherType);
        int nativeGatherType=multi?unchecked(gatherType*(int)MathF.Ceiling(resolutionScale)):gatherType;
        float maxGather=Sse.MaxScalar(Vector128.CreateScalar(gather),Vector128.CreateScalar(Mul(invRear,2.25f))).ToScalar();
        float anaX=F("AnamorphicRateXY"),anaY=F("AnamorphicRateXY",1);
        float anamorphicAspect=anaX>anaY?1:Div(Mul(anaX,anaX),Mul(anaY,anaY));
        float invNear=Div(1,F("CameraNearFarCm")),invFar=Div(1,F("CameraNearFarCm",1)),difference=Sub(invFar,invNear);
        byte[] bytes=new byte[144];
        Put(0,F("FocasuedDistance"));Put(4,F("DofLensCoeff"));Put(8,invRear);Put(12,I("EnableAutoExposure")>0?-1:F("UserExposureScale"));
        Put(16,gather);Put(20,nativeGatherType);Put(24,gatherStep);Put(28,Div(1,F("ScreenWhAspect")));
        Put(32,F("SpriteDofThreshold"));Put(36,maxGather);Put(40,I("StopBladeCount"));Put(44,F("StopPolyRate"));
        Put(48,difference);Put(52,invNear);Put(56,Div(1,difference));
        Put(60,BitConverter.UInt32BitsToSingle(BitConverter.SingleToUInt32Bits(Div(invNear,difference))^0x80000000));
        Put(64,invSceneW);Put(68,invSceneH);Put(72,invPreW);Put(76,invPreH);
        Put(80,Div(1,anamorphicAspect));Put(84,BitConverter.UInt32BitsToSingle(inputs.ActualContext1C48Word));
        Put(88,Div(1,anaX));Put(92,Div(1,anaY));Put(96,oddSide);
        Put(100,multi?BitConverter.UInt32BitsToSingle(0x3AF2B9D6):invRear);
        Put(104,fix4k?1:Div(1,anaX));Put(108,fix4k?Div(anaX,anaY):Div(1,anaY));
        Put(112,F("DOF2HeatSimmerParams"));Put(116,F("DOF2HeatSimmerParams",1));Put(120,F("DOF2HeatSimmerParams",2));Put(124,0);
        bool cropped=F("Viewport",2)!=1||F("Viewport",3)!=1;
        float clipX=1,clipY=1,rawX=0,rawY=0;
        if(cropped&&preComposite.Present)
        {
            rawX=Sub(Sse.ConvertToInt32WithTruncation(Vector128.CreateScalar(Mul(preComposite.Width,F("Viewport",2)))),.5f);
            rawY=Sub(Sse.ConvertToInt32WithTruncation(Vector128.CreateScalar(Mul(preComposite.Height,F("Viewport",3)))),.5f);
            clipX=Div(rawX,preComposite.Width);clipY=Div(rawY,preComposite.Height);
            if(inputs.ActualDescriptorMode58<2){rawX=0;rawY=0;}
        }
        Put(128,clipX);Put(132,clipY);Put(136,rawX);Put(140,rawY);
        int index=state.FindResourceIndex(MapPostEffect3ParameterResourceTable.ConstantBuffer,"cbDof2")
            ??throw new NotSupportedException("Actual cbDof2 declaration is absent.");
        var declaration=allocation.ConstantBufferParameters[index];
        if(index!=18||declaration.NameHash!=0x09427879||declaration.ByteCount!=144||
            declaration.Ownership!=MapPostEffect3AllocationOwnership.OwnedDeclaration)
            throw new InvalidDataException("Actual cbDof2 CB18/144B ownership differs.");
        // caches IsFirstExec(+138) into rbp+48 for the Clear/AA
        // branch; ChangeFStop(+120) goes to rbp+58 and only gates shape bit0.
        uint mask=I("IsFirstExec")!=0?0x800u:0x1000u;
        uint count=I("IsFirstExec")!=0?2:prior.Count10;
        if(I("ChangeFStop")!=0||prior.InitializeFix4K!=fix4k||prior.InitializeMultiResolution!=multi||prior.InitializeHeight!=nativeHeight)mask|=1;
        if(I("EnableDof")!=0)
        {
            bool sprite=I("EnableSpriteDof")!=0;
            if(sprite)
            {
                mask|=fix4k?0x18u:0x14u;
                int spritePass=cropped&&inputs.ActualDescriptorMode58>=2?(fix4k?30:29):(fix4k?6:5);
                mask|=1u<<spritePass;mask|=I("DiaphragmType")==0?0x180u:0x600u;
            }
            else mask|=2;
            mask|=1u<<(cropped?(sprite?22:21):(sprite?14:13));
            bool heat=F("DOF2HeatSimmerParams")!=0;int quality=I("QualityLevel");
            int compose=quality>=10?(heat?18:15):quality>=5?(heat?19:16):(heat?20:17);
            if(cropped)compose+=8;mask|=1u<<compose;
        }
        int[] cachePasses=[53,54,55,56,57,58,59,62,63,64,65,66,67,68,70,72,73,74,75,76,77,69,71,78,79,80,81,82,83,60,61];
        string[] cacheNames=["MakeDofShapeInfoPS","PrefilterNoSpriteDofPS","PrefilterPS","Prefilter_4KResFixPS","ClearSpritesInstanceInfoCS",
            "MakeSpritesInfoCS","MakeSpritesInfo_4KResFixCS","ScatterSpritesFromFuncRearPS","ScatterSpritesFromFuncForePS",
            "ScatterSpritesFromImageRearPS","ScatterSpritesFromImageForePS","ClearCoCHistoryPS","AntialiasCocPS",
            "PreCompositeNoSpriteDofPS","PreCompositeDofPS","CompositeDof9SamplePS","CompositeDof5SamplePS",
            "CompositeDof5SampleReductionPS","CompositeDof9SampleWithHeatSimmerPS","CompositeDof5SampleWithHeatSimmerPS",
            "CompositeDof5SampleReductionWithHeatSimmerPS","PreCompositeNoSpriteDof_ForViewportPS","PreCompositeDof_ForViewportPS",
            "CompositeDof9Sample_ForViewportPS","CompositeDof5Sample_ForViewportPS","CompositeDof5SampleReduction_ForViewportPS",
            "CompositeDof9SampleWithHeatSimmer_ForViewportPS","CompositeDof5SampleWithHeatSimmer_ForViewportPS",
            "CompositeDof5SampleReductionWithHeatSimmer_ForViewportPS","MakeSpritesInfo_ForViewportCS","MakeSpritesInfo_4KResFix_ForViewportCS"];
        for(int i=0;i<31;i++)
            if(allocation.Source.Passes[cachePasses[i]].NameHash!=MapPostEffect3ParameterState.PrimaryNameHash(cacheNames[i]))
                throw new InvalidDataException("DOF2 native31 cached pass names differ from the counted source directory.");
        var selected=cachePasses.Where((_,i)=>(mask&(1u<<i))!=0).Order().ToArray();
        var next=new MapPostEffect3DepthOfField2Controller(allocation,state.FrameNumber,count,!prior.HistoryFlip16,
            prior.SpriteSide28==0?1u:0u,shapeChanged?fix4k:prior.ShapeFix4K,shapeChanged?multi:prior.ShapeMultiResolution,
            shapeChanged?nativeHeight:prior.ShapeHeight,fix4k,multi,nativeHeight,cropped,SourceEvidence+";actual-owned-prepare18-then-init20",Locale)
            {RearScatterEnabled14=prior.RearScatterEnabled14,ForeScatterEnabled15=prior.ForeScatterEnabled15};
        int aliasSource=state.FindResourceIndex(MapPostEffect3ParameterResourceTable.Texture,"DOF2InputColor",
            MapPostEffect3ParameterAliasMode.SourceAsciiZeroLocale)!.Value;
        int aliasTarget=state.FindResourceIndex(MapPostEffect3ParameterResourceTable.Texture,"DOF2OutputColor",
            MapPostEffect3ParameterAliasMode.SourceAsciiZeroLocale)!.Value;
        return new(state,inputs,values,new(state,declaration,MapPostEffect3ConstantBufferWriter.DepthOfField2,bytes,Locale),next,shape,
            new(mask,Array.AsReadOnly(selected)),aliasSource,aliasTarget,SourceEvidence+";"+inputs.SourceEvidence,prior);
        void Put(int at,float value)=>BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(at),value);
    }

    private static Dictionary<string,byte[]> ReadScalars(MapPostEffect3ParameterState state)
    {
        var values=new Dictionary<string,byte[]>(StringComparer.Ordinal);
        foreach(var d in MapPostEffect3ApplyCompleteInitialScalars.DepthOfField2Defaults)
        {
            int index=state.FindScalarIndex(d.Name)??throw new NotSupportedException("DOF2 source typed lookup is absent: "+d.Name);
            var target=state.Declarations[index];
            if(target.NativeValueType!=d.NativeType||target.ElementCount!=d.ElementCount)
                throw new InvalidDataException("DOF2 source cached scalar type/count differs: "+d.Name);
            values.Add(d.Name,state.RequireValue(d.Name).ToArray());
        }
        return values;
    }
    private static void Validate(MapPostEffect3ParameterState state,MapPostEffect3AllocationPlan allocation)
    {
        ArgumentNullException.ThrowIfNull(state);ArgumentNullException.ThrowIfNull(allocation);
        MapPostEffect3ParameterState.ValidateSource(allocation.Source);
        if(state.SourceSha256!=allocation.Source.Sha256||state.OwnerIdentity!=allocation.ContextOwnerIdentity||state.FrameNumber<=0||
            allocation.ContextWidth==0||allocation.ContextHeight==0||allocation.ContextWidth>int.MaxValue||allocation.ContextHeight>int.MaxValue)
            throw new InvalidDataException("DOF2 actual scalar/allocation identity or descriptor extent differs.");
    }
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Mul(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Div(float a,float b)=>a/b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Sub(float a,float b)=>a-b;
}

public static class MapPostEffect3ParameterDepthOfField2
{
    public const string ResourceUri="san14://rules/post/depth-of-field2";
    public const string RuleVersion="depth-of-field2-v1";
    internal const uint FreshHistoryCount=2;
    internal const uint FreshShapeHeight=1080;
    internal const uint FreshInitializeHeight=1080;

    public static LoadedMapPostEffect3DepthOfField2Source Load(GameResourceRepository repository,string locale="TC")
    {
        ArgumentNullException.ThrowIfNull(repository);
        return RenderSamplerSourceAdapters.ReadPostEffect3DepthOfField2Source(RenderSamplerSourceAdapters.Load(repository.SourceDirectory,locale));
    }
}

public static partial class RenderSamplerSourceAdapters
{
    internal static LoadedMapPostEffect3DepthOfField2Source ReadPostEffect3DepthOfField2Source(LoadedDeferredSamplerNativeSource native)
    {
        if(native.Locale is not ("TC" or "SC"))throw new NotSupportedException("DOF2 capability2 rules support SC/TC.");
        return new(MapPostEffect3ParameterDepthOfField2.ResourceUri+"@"+
            MapPostEffect3ParameterDepthOfField2.RuleVersion+$";locale={native.Locale};"+
            "shape-then-CB18;initialize-alias;factory7-pass-mask;capability2",native.Locale);
    }
}
