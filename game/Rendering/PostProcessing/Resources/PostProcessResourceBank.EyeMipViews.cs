using Godot;
using San14.Data;

public sealed partial class PostProcessResourceBank
{
    private readonly Dictionary<(int Physical,int Side,uint Mip,uint Format),MapPostEffect3ComputeMipView> eyeMipViews=[];

    /// <summary>The source clone keeps the counted physical allocation and
    /// replaces its SRV/RTV with a single-mip view. Views belong to this bank;
    /// creating them never publishes readable pixel contents.</summary>
    private async Task<MapPostEffect3ComputeMipView> RequireEyeMipViewAsync(
        MapPostEffect3DeviceTexture texture,uint mip,uint nativeFormat)
    {
        if(texture.Source.Parameter.Index is not (85 or 86)||mip>=texture.Shape.MipLevels-1||
            !texture.NativeFormatViews.TryGetValue(nativeFormat,out var fullView))
            throw new InvalidDataException("Eye single-mip wrapper differs from the source85/86 view array.");
        var key=(texture.Source.Parameter.Index,texture.InstanceIndex,mip,nativeFormat);
        lock(lifetime)if(eyeMipViews.TryGetValue(key,out var found))return found;
        var group=BeginResources(texture.StorageRid);
        try
        {
            var created=await CreateComputeViewAsync(texture.StorageRid,fullView,nativeFormat,
                new((int)texture.Shape.Width,(int)texture.Shape.Height,1),(uint)texture.Shape.MipLevels,(int)mip,false,group.Resources);
            lock(lifetime)
            {
                // Callers hold the bank execution scope throughout creation.
                eyeMipViews.Add(key,created with{RetireAfterSubmission=false});
                if(group.Resources.Count==0)DiscardEmptyResources(group);
            }
            return created with{RetireAfterSubmission=false};
        }
        catch(Exception failure)
        {
            await ReleaseResourcesAfterFailureAsync(group,failure);
            throw;
        }
    }

    public async Task<IMapPostEffect3ComputeSelectedMipTextureLease> CaptureEyeMipAliasReadLeaseAsync(
        MapPostEffect3TextureParameterState table,int logicalParameter,int sourcePass,uint sourceRegister)
    {
        ArgumentNullException.ThrowIfNull(table);
        using var scope=await AcquireBufferExecutionScopeAsync();
        RequireEyeTable(table);
        var accepted=table.Require(logicalParameter);var reference=accepted.Reference;
        if(!reference.HasSourceSelectedMipView||reference.PhysicalAllocationParameterIndex is not (85 or 86)||
            reference.NativeMipLevelCount!=1)
            throw new InvalidDataException("Eye readable alias needs its actual copied single-mip wrapper.");
        if((uint)sourcePass>=Source.Passes.Count||!Source.Passes[sourcePass].ActiveBindings.Any(b=>
            b.Table==MapPostEffect3BindingTable.TextureParameters&&b.ParameterIndex==logicalParameter&&
            b.Role==MapPostEffect3BindingRole.ShaderResourceRead&&b.RegisterIndex==sourceRegister&&b.NativeViewKind==1))
            throw new InvalidDataException("Selected Eye alias is not this source pass/register's native texture setter.");
        // Native kind1 selects the wrapper's actual SRV.
        // Counted mip/slice columns are unused by this setter. The actual
        // source-cloned wrapper retains its one physical mip.
        int physical=reference.PhysicalAllocationParameterIndex.Value;uint mip=reference.NativeFirstMipLevel!.Value;
        var texture=RequireTexture(physical,reference.InstanceIndex);
        var contents=CaptureSourceWrittenTextureLease(physical,reference.InstanceIndex,table.FrameNumber,mip,1);
        var view=await RequireEyeMipViewAsync(texture,mip,texture.Shape.Formats.ShaderResourceFormat0);
        table.ValidateCurrent(accepted);contents.RequireCurrent();
        return new EyeSelectedMipReadLease(this,table,accepted,contents,view,sourcePass,sourceRegister,
            reference.SourceEvidence+";source-single-mip-wrapper");
    }

    /// <summary>Eye Before's dynamic PS t0 setter selects a cloned wrapper
    /// directly; it does not replace the full counted85/86 texture-table slot.</summary>
    public async Task<IMapPostEffect3ComputeSelectedMipTextureLease> CaptureEyeBeforeMipReadLeaseAsync(
        MapPostEffect3TextureParameterState table,MapPostEffect3EyeAdaptation2Frame frame,
        MapPostEffect3EyeBeforeBindings before)
    {
        ArgumentNullException.ThrowIfNull(table);ArgumentNullException.ThrowIfNull(frame);ArgumentNullException.ThrowIfNull(before);
        using var scope=await AcquireBufferExecutionScopeAsync();
        RequireEyeBefore(table,frame,before);
        if(!before.RequiresDynamicPixelTextureBinding||before.SourceTextureParameterIndex is not (85 or 86)||
            before.SourceMipLevel is not{} mip||mip<0)
            throw new InvalidDataException("Eye Before has no recovered current cloned t0 setter.");
        var accepted=table.Require(before.SourceTextureParameterIndex.Value);
        int physical=before.SourceTextureParameterIndex.Value;
        if(accepted.Reference.PhysicalAllocationParameterIndex!=physical||accepted.Reference.HasSourceSelectedMipView)
            throw new InvalidDataException("Eye dynamic source requires the full counted physical wrapper.");
        var texture=RequireTexture(physical,accepted.Reference.InstanceIndex);
        var contents=CaptureSourceWrittenTextureLease(physical,texture.InstanceIndex,frame.FrameNumber,(uint)mip,1);
        var view=await RequireEyeMipViewAsync(texture,(uint)mip,texture.Shape.Formats.ShaderResourceFormat0);
        frame.RequireCurrent();table.ValidateCurrent(accepted);contents.RequireCurrent();
        return new EyeSelectedMipReadLease(this,table,accepted,contents,view,before.Iteration.SourcePassIndex,0,
            before.SourceEvidence+";dynamic-PS-t0-current-single-mip-wrapper",frame);
    }

    private void RequireEyeTable(MapPostEffect3TextureParameterState table)
    {
        if(table.FrameNumber<=0||table.ContextOwnerIdentity!=Source.ContextOwnerIdentity||table.SourceSha256!=Source.Source.Sha256||
            table.Allocation.ContextWidth!=Source.ContextWidth||table.Allocation.ContextHeight!=Source.ContextHeight)
            throw new InvalidDataException("Eye texture table belongs to another bank/module/frame extent.");
    }
    private void RequireEyeBefore(MapPostEffect3TextureParameterState table,
        MapPostEffect3EyeAdaptation2Frame frame,MapPostEffect3EyeBeforeBindings before)
    {
        RequireEyeTable(table);frame.RequireCurrent();
        if(!ReferenceEquals(frame.Allocation,table.Allocation)||frame.FrameNumber!=table.FrameNumber||
            !frame.OrderedIterations.Contains(before.Iteration))
            throw new InvalidDataException("Eye Before belongs to another current factory iteration.");
    }

    private sealed record EyeSelectedMipReadLease(PostProcessResourceBank Bank,
        MapPostEffect3TextureParameterState Table,MapPostEffect3ResolvedTextureParameter Accepted,
        IMapPostEffect3ComputeMipTextureLease Contents,MapPostEffect3ComputeMipView View,
        int Pass,uint Register,string NativeMipSelectionEvidence,MapPostEffect3EyeAdaptation2Frame? Frame=null)
        :IMapPostEffect3ComputeSelectedMipTextureLease
    {
        public long FrameNumber=>Accepted.FrameNumber;
        public string ContextOwnerIdentity=>Accepted.ContextOwnerIdentity;
        public int ParameterIndex=>Accepted.ParameterIndex;
        public int InstanceIndex=>Contents.InstanceIndex;
        public Rid ShaderViewRid=>View.ViewRid;
        public Rid StorageRid=>View.StorageRid;
        public uint NativeViewFormat=>View.NativeViewFormat;
        public RenderingDevice.DataFormat DeviceFormat=>View.DeviceFormat;
        public Vector3I Extent=>View.Extent;
        public string SourceEvidence=>NativeMipSelectionEvidence+";"+Contents.SourceEvidence;
        public bool IsSourceSelectedMipView=>true;
        public uint NativeFirstMipLevel=>View.FirstMipLevel;
        public uint NativeMipLevelCount=>View.MipLevelCount;
        public void RequireCurrent()
        {lock(Bank.lifetime){ObjectDisposedException.ThrowIf(Bank.retiring,Bank);}
         Frame?.RequireCurrent();Table.ValidateCurrent(Accepted);Contents.RequireCurrent();}
        public void RequireReadableMipRange(uint first,uint count)
        {
            RequireCurrent();
            if(first!=NativeFirstMipLevel||count!=NativeMipLevelCount)
                throw new InvalidDataException("Selected Eye wrapper cannot expose another physical mip range.");
            Contents.RequireReadableMipRange(first,count);
        }
        public void RequireSourceSelectedView(int pass,uint register)
        {RequireCurrent();if(pass!=Pass||register!=Register)
             throw new InvalidDataException("Selected Eye wrapper belongs to another actual source setter.");}
    }
}
