using Godot;
using San14.Data;

internal enum PostPipelineMode { Compute, Graphics }

/// <summary>Owns retained binding metadata across a post-processing directory.
/// Device executors bind shader resources; this cache tracks borrowed identities.</summary>
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
        // Target selectors remain unknown until a setter writes them.
        internal sbyte? Mip,Slice;
    }
    private readonly Row[][] groups=[Make(96),Make(96),Make(96),Make(4)];
    private readonly Dictionary<object,int> retained=new(ReferenceEqualityComparer.Instance);
    private PostPipelineMode? currentMode;
    private bool modeEstablished;
    internal object? CurrentPassDescriptor{get;private set;}
    internal int OutstandingLoans=>retained.Values.Sum();
    internal MapPostEffect3Functor1FrontendCacheOwner(LoadedMapPostEffect3Functor1FrontendCursorSource source)
    {if(source.FreshNativeMode1C04!=-1)throw new InvalidDataException("Cursor cache requires the original fresh constructor.");}
    private static Row[] Make(int count)=>Enumerable.Range(0,count).Select(_=>new Row()).ToArray();
    internal void SelectHeader(object actualDescriptor,PostPipelineMode mode)
    {
        CurrentPassDescriptor=actualDescriptor;
        if(currentMode!=mode)
        {
            ResetResources();
            modeEstablished=true;
        }
        currentMode=mode;
    }
    internal void SetCounted(MapPostEffect3FrontendCountedSetter setter,Metadata? metadata)
    {
        var b=setter.Binding;metadata?.Require();
        if(b.Table==MapPostEffect3BindingTable.TextureParameters&&metadata is null&&b.NativeViewKind is 0 or 3)
            return;
        int group=b.Role switch{MapPostEffect3BindingRole.ShaderResourceRead=>0,
            MapPostEffect3BindingRole.UnorderedAccessReadWrite=>1,MapPostEffect3BindingRole.ConstantBufferRead=>2,
            MapPostEffect3BindingRole.RenderTargetWrite=>3,_=>throw new InvalidDataException("Unknown cache setter.")};
        Set(group,group==3?0:setter.NativeStageOrdinal,b.RegisterIndex,metadata,
            group is 1 or 3?b.MipSelector:(sbyte)-1,group==3?b.SliceSelector:(sbyte)-1,b);
    }
    internal void SetBeforeTexture(int stage,int register,Metadata? metadata)
    {metadata?.Require();Set(0,stage,register,metadata,-1,-1,null);}
    internal void SetBeforeTarget(int register,Metadata? metadata,int mip,int slice)
    {metadata?.Require();Set(3,0,register,metadata,checked((sbyte)mip),checked((sbyte)slice),null);}
    private void Set(int group,int stage,int slot,Metadata? incoming,sbyte mip,sbyte slice,
        MapPostEffect3Binding? counted)
    {
        if(stage is <0 or >5||slot<0||slot>=(group==3?4:16))throw new InvalidDataException("Cache setter exceeds source slot.");
        var row=groups[group][group==3?slot:stage*16+slot];
        bool pointerChanged=!ReferenceEquals(row.Loan?.Pointer,incoming?.Pointer);
        if(group==3&&!pointerChanged&&(row.Mip is null||row.Slice is null))
            throw new NotSupportedException("A same-null RT setter needs actual previously written native mip/slice bytes; constructor does not initialize them.");
        bool changed=pointerChanged||group==3&&(row.Mip!=mip||row.Slice!=slice);
        if(changed)
        {
            Release(row);
            if(incoming is not null)
                retained[incoming.Retained]=retained.GetValueOrDefault(incoming.Retained)+1;
            row.Loan=incoming;row.Mip=mip;
            if(group==3)row.Slice=slice;
        }
        // A same-pointer update preserves the retained UAV mip selector.
        row.Counted=counted;
    }
    private void Release(Row row)
    {
        if(row.Loan is not{} loan)return;
        if(!retained.TryGetValue(loan.Retained,out int count)||count<=0)throw new InvalidDataException("Actual cache released a loan it never acquired.");
        if(count==1)retained.Remove(loan.Retained);else retained[loan.Retained]=count-1;
        row.Loan=null;
    }
    internal void ResetResources()
    {
        // Reset stages0..5:16SRV,16UAV,16CB; then four RT slots.
        for(int stage=0;stage<6;stage++)for(int group=0;group<3;group++)
        for(int slot=0;slot<16;slot++)ResetRow(group,stage,slot);
        for(int slot=0;slot<4;slot++)ResetRow(3,0,slot);
    }
    private void ResetRow(int group,int stage,int slot)
    {
        var row=groups[group][group==3?slot:stage*16+slot];
        if(row.Loan is not null)Release(row);
        row.Counted=null;
    }
    internal void ResetPass()
    {ResetResources();CurrentPassDescriptor=null;}
    internal void BeginModule()
    {
        if(OutstandingLoans!=0||CurrentPassDescriptor is not null)
            throw new InvalidDataException("Module Begin cannot replace outstanding actual cache loans.");
        currentMode=null;
    }
    internal void EndModule()
    {
        // End releases borrowed rows while preserving the current descriptor.
        for(int group=0;group<4;group++)for(int i=0;i<groups[group].Length;i++)
        {
            var row=groups[group][i];
            if(row.Loan is null)continue;
            Release(row);

        }
        if(!modeEstablished)throw new InvalidDataException("Actual directory has not established native pending mode.");
        if(OutstandingLoans!=0)throw new InvalidDataException("End did not release its acquired cache loans.");
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
}
