using Godot;
using San14.Data;

/// <summary>Closed source wrapper identities backed by the same bank's actual
/// single-mip SRV/RTV views. The catalog retains allocation metadata only.
/// Source CPU aliases map to one canonical clone, never to its full wrapper.</summary>
internal sealed class OriginalMapPostEffect3Functor1EyeCloneCatalog
{
    internal sealed class Entry
    {
        internal Entry(OriginalMapPostEffect3Functor1EyeCloneCatalog owner,OriginalMapPostEffect3EyeCloneWrapperDirectory.Entry source,
            OriginalMapPostEffect3DeviceTexture texture,OriginalMapPostEffect3ComputeMipView srv,OriginalMapPostEffect3ComputeMipView rtv)
            =>(Owner,Source,Texture,Srv,Rtv)=(owner,source,texture,srv,rtv);
        internal readonly OriginalMapPostEffect3Functor1EyeCloneCatalog Owner;
        internal readonly OriginalMapPostEffect3EyeCloneWrapperDirectory.Entry Source;
        internal readonly OriginalMapPostEffect3DeviceTexture Texture;
        internal readonly OriginalMapPostEffect3ComputeMipView Srv,Rtv;
        internal void RequireCurrent()
        {
            Owner.Source.RequireCurrent();
            if(!ReferenceEquals(Owner.Bank.RequireTexture(Source.PhysicalParameterIndex,Source.InstanceIndex),Texture)||
                Texture.StorageRid!=Srv.StorageRid||Texture.StorageRid!=Rtv.StorageRid||!Srv.ViewRid.IsValid||!Rtv.ViewRid.IsValid)
                throw new InvalidDataException("Eye cloned wrapper lost its exact bank physical resource/views.");
        }
        internal object Snapshot()=>new{Source.PhysicalParameterIndex,Source.InstanceIndex,Source.NativeMipLevel,
            Source.NativeWrapperWidth40,Source.NativeWrapperHeight42,Source.NativeMipLevelCount,
            storage=Texture.StorageRid.Id,srv=Srv.ViewRid.Id,rtv=Rtv.ViewRid.Id,Source.SourceEvidence,
            allocationOnly=true,grantsPixelReadiness=false};
    }
    private readonly Dictionary<OriginalMapPostEffect3EyeCloneWrapperDirectory.Entry,Entry> entries=[];
    internal OriginalMapPostEffect3EyeCloneWrapperDirectory Source{get;}
    internal OriginalMapPostEffect3ResourceBank Bank{get;}
    private OriginalMapPostEffect3Functor1EyeCloneCatalog(OriginalMapPostEffect3EyeCloneWrapperDirectory source,
        OriginalMapPostEffect3ResourceBank bank)=>(Source,Bank)=(source,bank);
    internal static async Task<OriginalMapPostEffect3Functor1EyeCloneCatalog> CaptureAsync(
        OriginalMapPostEffect3WorldControlledFactoryFrame frame,OriginalMapPostEffect3ProgramLibrary library,
        LoadedOriginalMapPostEffect3EyeAdaptation2Source source)
    {
        frame.RequireCurrent();
        if(library.SourceSha256!=frame.Camera.Allocation.Source.Sha256)
            throw new InvalidDataException("Eye clone catalog needs this actual source program library.");
        foreach(var iteration in frame.Eye.OrderedIterations.Where(i=>!i.IsCompute))
            if(iteration.NativeCachedPassOrdinal is not(0 or 2 or 3 or 4 or 9 or 10 or 11))
                throw new NotSupportedException("Selected Eye debug/histogram graphics needs its own original vertex/resource/output admission.");
        var directory=OriginalMapPostEffect3EyeCloneWrapperDirectory.Prepare(source,frame.Eye,frame.Textures);
        var result=new OriginalMapPostEffect3Functor1EyeCloneCatalog(directory,frame.World.Bank);
        foreach(var view in await result.Bank.CaptureEyeCloneViewsAsync(directory))
            result.entries.Add(view.Source,new(result,view.Source,view.Texture,view.Srv,view.Rtv));
        if(frame.Eye.BloomEnabled&&frame.Eye.LevelCount>directory.Entries.Count(e=>e.PhysicalParameterIndex==85))
            throw new NotSupportedException("Selected Eye source level count exceeds its actual initialized clone arrays.");
        result.Source.RequireCurrent();return result;
    }
    internal Entry RequireAlias(int logical)
    {Source.RequireCurrent();return Require(Source.ResolveCurrentAlias(logical));}
    internal Entry RequireDynamicSource(OriginalMapPostEffect3EyeBeforeBindings before)=>Require(Source.RequireDynamicSource(before));
    internal Entry RequireTargetView(OriginalMapPostEffect3EyeBeforeBindings before)=>Require(Source.RequireTargetView(before));
    private Entry Require(OriginalMapPostEffect3EyeCloneWrapperDirectory.Entry source)
    {var entry=entries[source];entry.RequireCurrent();return entry;}
    internal OriginalMapPostEffect3Functor1FrontendCacheOwner.Metadata Metadata(Entry entry,int logical)
    {
        entry.RequireCurrent();
        if(!ReferenceEquals(entry.Owner,this))throw new InvalidDataException("Eye clone metadata belongs to another catalog.");
        return new(entry,entry,entry.Texture.StorageRid,logical,entry.Source.InstanceIndex,entry.Source.SourceEvidence+
            ";actual-canonical-Eye-clone-SRV="+entry.Srv.ViewRid.Id+"/RTV="+entry.Rtv.ViewRid.Id+";metadata/no-pixels",entry.RequireCurrent)
            {PhysicalProducer=entry};
    }
    internal object Snapshot()=>new{Source.SourceEvidence,cloneCount=entries.Count,
        entries=Source.Entries.Select(source=>entries[source].Snapshot()).ToArray(),
        actualViewsCreated=true,grantsPixelReadiness=false};
    internal object CaptureCompletedBindings(OriginalMapPostEffect3EyeBeforeBindings before,OriginalMapPostEffect3FullscreenCompletedPass completed)
    {
        var target=RequireTargetView(before);
        if(completed.SourcePassIndex!=before.Iteration.SourcePassIndex||completed.FrameNumber!=Source.Frame.FrameNumber||
            completed.Writes is not [var write]||write.StorageRid!=target.Texture.StorageRid||write.TargetViewRid!=target.Rtv.ViewRid||
            write.ParameterIndex!=target.Source.PhysicalParameterIndex||write.InstanceIndex!=target.Source.InstanceIndex||
            write.NativeMipLevel!=target.Source.NativeMipLevel)
            throw new InvalidDataException("Historical Eye target does not join its exact actual array/catalog view.");
        Entry? input=null;
        if(before.Iteration.NativeCachedPassOrdinal!=0)
        {
            input=before.RequiresDynamicPixelTextureBinding?RequireDynamicSource(before):RequireAlias(69);
            var read=completed.TextureReads.SingleOrDefault(r=>r.SourceStageCode==0&&r.SourceRegister==0);
            if(read is null||read.StorageRid!=input.Texture.StorageRid||read.ShaderViewRid!=input.Srv.ViewRid||
                read.InstanceIndex!=input.Source.InstanceIndex||read.NativeFirstMipLevel!=input.Source.NativeMipLevel||
                read.NativeMipLevelCount!=1)
                throw new InvalidDataException("Historical Eye source does not join its exact actual array/catalog clone SRV.");
        }
        return new{target=target.Snapshot(),input=input?.Snapshot(),sourcePointerKind=before.RequiresDynamicPixelTextureBinding?
            "Before-array-clone-t0":before.Iteration.NativeCachedPassOrdinal==0?"counted-Scene68/exposure61":"counted-clone-alias69",
            targetPointerKind=before.RequiresDynamicTargetBinding?"Before-full-physical-wrapper-plus-mip":"counted-clone-alias69/mip=-1",
            actualSubmissionAlreadyOwnerValidated=true,grantsPixelReadiness=false};
    }
}

//The actual controlled frontend supplies this closed cache/catalog join to
//EyeBloomOwner before its real Draw. Standalone component callers retain
//their existing explicit source/raster/texture gates.
internal interface IOriginalMapPostEffect3EyeCloneBindingContext
{
    void RequireEyeCloneBindings(OriginalMapPostEffect3EyeAdaptation2Frame frame,
        OriginalMapPostEffect3EyeBeforeBindings before,IOriginalMapPostEffect3FullscreenSelectedMipTargetLease target,
        IReadOnlyList<IOriginalMapPostEffect3ComputeTextureLease> reads);
}
