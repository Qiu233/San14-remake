using Godot;
using San14.Data;

/// <summary>One actual managed lifetime of the original292 CPU cache slots.
/// Loans retain source allocation/producer identities, never pixel readiness.
/// Device lists continue to bind explicit shader interfaces in real executors.</summary>
internal sealed class MapPostEffect3Functor1FrontendCacheOwner
{
    internal sealed class Metadata
    {
        internal Metadata(object pointer,object retained,Rid storage,int parameter,int side,string evidence,Action require)
            =>(Pointer,Retained,Storage,Parameter,Side,Evidence,Require)=(pointer,retained,storage,parameter,side,evidence,require);
        internal readonly object Pointer,Retained;
        internal readonly Rid Storage;
        internal readonly int Parameter,Side;
        internal readonly string Evidence;
        internal readonly Action Require;
        internal object? PhysicalProducer{get;init;}
    }
    private sealed class Row
    {
        internal Metadata? Loan;
        internal MapPostEffect3Binding? Counted;
        // Fresh cached resource entries initialize type byte0=FF, not
        //selector bytes1/2. Preserve those unwritten selectors as unknown.
        internal byte NativeResourceKind=0xFF;
        internal sbyte? Mip,Slice;
        internal ushort Dirty;
    }
    private readonly Row[][] groups=[Make(96),Make(96),Make(96),Make(4)];
    private readonly Dictionary<object,int> retained=new(ReferenceEqualityComparer.Instance);
    private readonly List<object> events=[];
    private long eventOrdinal;
    internal int NativeMode1C04{get;private set;}=-1;
    internal int NativeFirstPass1BF4{get;private set;}=-1;
    internal int NativeDirectoryOrdinal1BF8{get;private set;}=-1;
    //The borrowed backend's initial2A98 byte is not in the module ctor.
    //Mode-change branches prove only its nonzero/zero result. Godot's
    //explicit pipelines execute separately; this is not a packed opcode log.
    internal bool? NativePendingComputeBoolean2A98{get;private set;}
    internal object? CurrentPassDescriptor{get;private set;}
    internal int OutstandingLoans=>retained.Values.Sum();
    internal IReadOnlyList<object> Events=>Array.AsReadOnly(events.ToArray());
    internal MapPostEffect3Functor1FrontendCacheOwner(LoadedMapPostEffect3Functor1FrontendCursorSource source)
    {if(source.FreshNativeMode1C04!=-1)throw new InvalidDataException("Cursor cache requires the original fresh constructor.");}
    private static Row[] Make(int count)=>Enumerable.Range(0,count).Select(_=>new Row()).ToArray();
    internal void SelectHeader(MapPostEffect3Functor1WrapperRecipe recipe,object actualDescriptor,int actualMode)
    {
        NativeFirstPass1BF4=45;NativeDirectoryOrdinal1BF8=recipe.SourceDirectoryOrdinal;CurrentPassDescriptor=actualDescriptor;
        if(actualMode is not(0 or 2))throw new InvalidDataException("Actual Select mode lacks its CS/PS catalog wrapper.");
        if(NativeMode1C04!=actualMode)
        {
            ResetResources("mode-change",recipe.SourcePassIndex);
            bool? prior=NativePendingComputeBoolean2A98;bool next=actualMode==0;
            int? provedSourceOpcode=prior is null||prior==next?null:next?35:36;
            NativePendingComputeBoolean2A98=next;
            Log("ModeChangeSourceBackendSemantic",recipe.SourcePassIndex,new{prior,next,provedSourceOpcode,
                packedNativeDeviceCommandObserved=false});
        }
        NativeMode1C04=actualMode;Log("SelectHeader",recipe.SourcePassIndex,new{NativeFirstPass1BF4,NativeDirectoryOrdinal1BF8,NativeMode1C04});
    }
    internal void SetCounted(MapPostEffect3FrontendCountedSetter setter,Metadata? metadata,int pass)
    {
        var b=setter.Binding;metadata?.Require();
        if(b.Table==MapPostEffect3BindingTable.TextureParameters&&metadata is null&&b.NativeViewKind is 0 or 3)
        {Log("KnownNullTextureSetterSkipped",pass,new{b.SourceRowIndex,b.ParameterIndex,b.NativeViewKind,setter.NativeStageOrdinal,b.RegisterIndex});return;}
        int group=b.Role switch{MapPostEffect3BindingRole.ShaderResourceRead=>0,
            MapPostEffect3BindingRole.UnorderedAccessReadWrite=>1,MapPostEffect3BindingRole.ConstantBufferRead=>2,
            MapPostEffect3BindingRole.RenderTargetWrite=>3,_=>throw new InvalidDataException("Unknown cache setter.")};
        byte nativeKind=group==2?(byte)3:group==3?(byte)1:group==1?
            (b.Table==MapPostEffect3BindingTable.TextureParameters?(byte)1:(byte)2):
            (b.Table==MapPostEffect3BindingTable.TextureParameters&&metadata is not null?(byte)1:(byte)0);
        Set(group,group==3?0:setter.NativeStageOrdinal,b.RegisterIndex,metadata,nativeKind,
            group is 1 or 3?b.MipSelector:(sbyte)-1,group==3?b.SliceSelector:(sbyte)-1,b,pass,"CountedSetter");
    }
    internal void SetBeforeTexture(int stage,int register,Metadata? metadata,int pass,string evidence)
    {metadata?.Require();Set(0,stage,register,metadata,1,-1,-1,null,pass,evidence);}
    internal void SetBeforeTarget(int register,Metadata? metadata,int mip,int slice,int pass,string evidence)
    {metadata?.Require();Set(3,0,register,metadata,1,checked((sbyte)mip),checked((sbyte)slice),null,pass,evidence);}
    private void Set(int group,int stage,int slot,Metadata? incoming,byte nativeKind,sbyte mip,sbyte slice,
        MapPostEffect3Binding? counted,int pass,string reason)
    {
        if(stage is <0 or >5||slot<0||slot>=(group==3?4:16))throw new InvalidDataException("Cache setter exceeds source slot.");
        var row=groups[group][group==3?slot:stage*16+slot];
        bool pointerChanged=!ReferenceEquals(row.Loan?.Pointer,incoming?.Pointer);
        if(group==3&&!pointerChanged&&(row.Mip is null||row.Slice is null))
            throw new NotSupportedException("A same-null RT setter needs actual previously written native mip/slice bytes; constructor does not initialize them.");
        bool changed=pointerChanged||group==3&&(row.Mip!=mip||row.Slice!=slice);
        if(changed)
        {
            Release(row,group,stage,slot,pass,reason);
            if(incoming is not null)
            {retained[incoming.Retained]=retained.GetValueOrDefault(incoming.Retained)+1;
                Log("Retain",pass,new{group,stage,slot,incoming.Parameter,incoming.Side,storage=incoming.Storage.Id,incoming.Evidence});}
            row.Loan=incoming;row.Dirty=0x0101;row.NativeResourceKind=nativeKind;row.Mip=mip;
            if(group==3)row.Slice=slice;
        }
        else row.Dirty|=0x0100; //same pointer setter only sets row+4.
        // Compare only the pointer: the same-pointer branch writes
        //row+4 and leaves the native UAV mip byte unchanged.
        row.Counted=counted;
        Log(reason,pass,new{group,stage,slot,parameter=incoming?.Parameter,side=incoming?.Side,storage=incoming?.Storage.Id,
            sourceRow=counted?.SourceRowIndex,mip,slice,knownNull=incoming is null,changed});
    }
    private void Release(Row row,int group,int stage,int slot,int pass,string reason)
    {
        if(row.Loan is not{} loan)return;
        if(!retained.TryGetValue(loan.Retained,out int count)||count<=0)throw new InvalidDataException("Actual cache released a loan it never acquired.");
        if(count==1)retained.Remove(loan.Retained);else retained[loan.Retained]=count-1;
        Log("Release",pass,new{group,stage,slot,loan.Parameter,loan.Side,storage=loan.Storage.Id,reason});row.Loan=null;
    }
    internal void FlushBeforeActualBind(int pass)=>Flush("before-bind",pass);
    private void Flush(string reason,int pass)
    {
        // Visit UAV, SRV and CB for each stage, clearing both dirty bytes.
        // Null CB skips buffer binding. RT rows are handled by actual target Bind.
        for(int stage=0;stage<6;stage++)foreach(int group in new[]{1,0,2})
        for(int slot=0;slot<16;slot++)
        {
            var row=groups[group][stage*16+slot];
            if((row.Dirty&0xFF)!=0)
                Log(group==2&&row.Loan is null?"FlushNullCbSkipped":group==1&&stage is not(0 or 2)?
                    "FlushUavInactiveStage":row.Loan is null?"FlushNullResource":"FlushMetadataResource",
                    pass,new{group,stage,slot,reason,row.NativeResourceKind,row.Mip,row.Slice,parameter=row.Loan?.Parameter,storage=row.Loan?.Storage.Id});
            row.Dirty=0;
        }
    }
    internal void ResetResources(string reason,int pass)
    {
        // Reset stages0..5:16SRV,16UAV,16CB; then four RT slots.
        for(int stage=0;stage<6;stage++)for(int group=0;group<3;group++)
        for(int slot=0;slot<16;slot++)ResetRow(group,stage,slot,reason,pass);
        for(int slot=0;slot<4;slot++)ResetRow(3,0,slot,reason,pass);
        Flush(reason,pass);Log("ResetResources",pass,new{reason,remainingLoans=OutstandingLoans});
    }
    private void ResetRow(int group,int stage,int slot,string reason,int pass)
    {
        var row=groups[group][group==3?slot:stage*16+slot];
        if(row.Loan is not null){Release(row,group,stage,slot,pass,reason);row.Dirty=0x0101;}
        row.Counted=null;
    }
    internal void ResetPass(int pass)
    {ResetResources("wrapper-reset",pass);CurrentPassDescriptor=null;Log("ResetDescriptor",pass,new{hasCurrentDescriptor=false});}
    internal void BeginModule()
    {
        if(OutstandingLoans!=0||CurrentPassDescriptor is not null)
            throw new InvalidDataException("Module Begin cannot replace outstanding actual cache loans.");
        events.Clear();eventOrdinal=0; //bounded current-invocation diagnostics
        NativeMode1C04=-1; // Every Begin writes this, not only construction.
        Log("ModuleBegin",-1,new{NativeMode1C04});
    }
    internal void EndModule()
    {
        // Clear only borrowed nonnull rows. Flush excludes RT. Do
        //not reuse Reset: its descriptor reset is a distinct source call.
        for(int group=0;group<4;group++)for(int i=0;i<groups[group].Length;i++)
        {
            var row=groups[group][i];
            if(row.Loan is null)continue;
            Release(row,group,group==3?0:i/16,group==3?i:i%16,116,"module-end");
            row.Dirty=0x0101;
        }
        bool? pending=NativePendingComputeBoolean2A98;
        if(pending is null)throw new InvalidDataException("Actual directory has not established native pending mode.");
        NativePendingComputeBoolean2A98=false;
        Flush("module-end-flush",116);
        if(OutstandingLoans!=0)throw new InvalidDataException("End did not release its acquired cache loans.");
        Log("ModuleEnd",116,new{argument=uint.MaxValue,pendingBefore=pending,conditionalSourceOpcode=pending==true?0x36:(int?)null,
            packedNativeDeviceCommandObserved=false,nullCbUploads=0,optionalFinalDraw=false,remainingLoans=OutstandingLoans});
    }
    internal IReadOnlyList<MapPostEffect3Binding?> ReadCountedTargets()
        =>Array.AsReadOnly(groups[3].Select(r=>r.Loan is null?null:r.Counted).ToArray());
    internal void RequireActualTargets(int pass,IReadOnlyList<MapPostEffect3DepthOfField2TargetView> actual)
    {
        var expected=groups[3].Select((row,register)=>(row,register)).Where(p=>p.row.Loan is not null).ToArray();
        if(expected.Length!=actual.Count||expected.Zip(actual).Any(p=>p.First.register!=p.Second.SourceTargetRegister||
            !ReferenceEquals(p.First.row.Loan!.Pointer,p.Second.ActualPointer.Reference)||p.First.row.Loan.Storage!=p.Second.StorageRid))
            throw new InvalidDataException("Actual target views differ from the central native counted/Before cache: "+pass);
    }
    internal void RequireEyeCloneBindings(MapPostEffect3Functor1EyeCloneCatalog catalog,
        MapPostEffect3EyeBeforeBindings before,IMapPostEffect3FullscreenSelectedMipTargetLease target,
        IReadOnlyList<IMapPostEffect3ComputeTextureLease>? reads=null)
    {
        var view=catalog.RequireTargetView(before);int pass=before.Iteration.SourcePassIndex;
        target.RequireCurrentForPass(pass);target.RequireSourceSelectedTarget(pass,0);
        var actualTargets=groups[3].Select((row,slot)=>(row,slot)).Where(p=>p.row.Loan is not null).ToArray();
        if(actualTargets.Length!=1||actualTargets[0].slot!=0)
            throw new InvalidDataException("Eye actual Bind needs exactly its source current cache RT0.");
        var row=actualTargets[0].row;var loan=row.Loan!;loan.Require();
        bool sourcePointerMatches=before.RequiresDynamicTargetBinding?
            ReferenceEquals(loan.Pointer,view.Source.FullPointer.Reference)&&ReferenceEquals(loan.PhysicalProducer,view.Texture)&&
                row.Mip==view.Source.NativeMipLevel:
            ReferenceEquals(loan.Pointer,view)&&ReferenceEquals(loan.PhysicalProducer,view)&&row.Mip==-1;
        if(!sourcePointerMatches||row.Slice!=-1||loan.Storage!=target.StorageRid||
            target.StorageRid!=view.Rtv.StorageRid||target.TargetViewRid!=view.Rtv.ViewRid||
            target.InstanceIndex!=view.Source.InstanceIndex||target.ParameterIndex!=view.Source.PhysicalParameterIndex||
            target.NativeFirstMipLevel!=view.Source.NativeMipLevel||target.NativeMipLevelCount!=1||
            target.NativeViewFormat!=view.Rtv.NativeViewFormat||target.Extent!=new Vector2I(view.Rtv.Extent.X,view.Rtv.Extent.Y))
            throw new InvalidDataException("Eye RT0 does not match its original counted clone0 or Before full-wrapper/mip setter.");
        if(reads is null)return;
        if(before.Iteration.NativeCachedPassOrdinal==0)
        {
            foreach(int register in new[]{0,1})
            {
                var source=groups[0][2*16+register].Loan??throw new InvalidDataException("Eye prefilter counted source texture is absent.");
                var actual=reads.SingleOrDefault(read=>read.ParameterIndex==source.Parameter);
                source.Require();actual?.RequireCurrent();
                if(actual is null||source.Storage!=actual.StorageRid||source.Side!=actual.InstanceIndex)
                    throw new InvalidDataException("Eye prefilter actual SRV differs from its original current counted source pointer.");
            }
        }
        else
        {
            var source=before.RequiresDynamicPixelTextureBinding?catalog.RequireDynamicSource(before):catalog.RequireAlias(69);
            var cached=groups[0][2*16].Loan;
            if(cached is null||!ReferenceEquals(cached.Pointer,source)||!ReferenceEquals(cached.PhysicalProducer,source)||
                reads is not [IMapPostEffect3ComputeSelectedMipTextureLease actual])
                throw new InvalidDataException("Eye actual t0 must borrow its exact independent source array-clone wrapper.");
            source.RequireCurrent();actual.RequireSourceSelectedView(pass,0);actual.RequireCurrent();
            if(actual.ParameterIndex!=cached.Parameter||actual.InstanceIndex!=source.Source.InstanceIndex||
                actual.StorageRid!=source.Srv.StorageRid||actual.ShaderViewRid!=source.Srv.ViewRid||
                actual.NativeViewFormat!=source.Srv.NativeViewFormat||actual.NativeFirstMipLevel!=source.Source.NativeMipLevel||
                actual.NativeMipLevelCount!=1||actual.Extent!=source.Srv.Extent)
                throw new InvalidDataException("Eye actual sampled mip/view differs from its source clone wrapper identity.");
        }
    }
    internal object Snapshot()=>new{NativeMode1C04,NativeFirstPass1BF4,NativeDirectoryOrdinal1BF8,NativePendingComputeBoolean2A98,
        hasCurrentDescriptor=CurrentPassDescriptor is not null,OutstandingLoans,
        rows=groups.SelectMany((rows,g)=>rows.Select((r,i)=>new{group=g,stage=g==3?0:i/16,slot=g==3?i:i%16,
            borrowed=r.Loan is not null,retained=r.Loan is not null,r.NativeResourceKind,r.Mip,r.Slice,r.Dirty,parameter=r.Loan?.Parameter,
            storage=r.Loan?.Storage.Id,physicalProducerType=r.Loan?.PhysicalProducer?.GetType().FullName})).ToArray()};
    private void Log(string operation,int pass,object fields)=>events.Add(new{ordinal=eventOrdinal++,operation,pass,fields});
}
