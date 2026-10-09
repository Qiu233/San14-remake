using Godot;
using San14.Data;

public sealed partial class PostProcessResourceBank
{
    public async Task<IMapPostEffect3ComputeTextureLease> CaptureComposite2TextureReadLeaseAsync(
        MapPostEffect3TextureParameterState table,MapPostEffect3Composite2BeforeBindings before,
        MapPostEffect3Binding binding)
    {
        before.RequireCurrent();RequireComposite2Table(table,before);
        if(!Source.Passes[before.SourcePassIndex].ActiveBindings.Contains(binding)||binding.NativeViewKind!=1||
            binding.Role!=MapPostEffect3BindingRole.ShaderResourceRead||binding.Table!=MapPostEffect3BindingTable.TextureParameters||
            (binding.StageMask&4)==0)
            throw new InvalidDataException("Composite2 read needs its exact original current native texture setter.");
        var accepted=table.Require(binding.ParameterIndex);
        if(accepted.Reference.HasSourceSelectedMipView)
            return await CaptureEyeMipAliasReadLeaseAsync(table,binding.ParameterIndex,before.SourcePassIndex,checked((uint)binding.RegisterIndex));
        return CaptureSourceWrittenTextureAliasLease(table,binding.ParameterIndex,table.FrameNumber,0,1);
    }

    public IMapPostEffect3ComputeTextureLease CaptureComposite2IndependentTextureRead(
        MapPostEffect3TextureParameterState table,MapPostEffect3Composite2BeforeBindings before,
        MapPostEffect3Composite2ReadFootprint proof,MapPostEffect3ShaderReadFootprint footprint)
    {
        before.RequireCurrent();proof.RequireCurrent();RequireComposite2Table(table,before);
        if(proof.SourcePassIndex!=before.SourcePassIndex||!proof.ShaderReads.Contains(footprint)||footprint.RequiresInitializedContents)
            throw new InvalidDataException("Allocated-only Composite2 read needs its exact original no-output-dependency footprint.");
        var accepted=table.Require(footprint.NativeBinding.ParameterIndex);
        if(accepted.Reference.PhysicalAllocationParameterIndex is not int physical||accepted.Reference.HasSourceSelectedMipView)
            throw new InvalidDataException("Composite2 allocated-only gate cannot replace an external or selected-mip producer.");
        var texture=RequireTexture(physical,accepted.Reference.InstanceIndex);
        return new Composite2IndependentRead(this,table,accepted,texture,before,proof);
    }

    /// <summary>Captures the actual source selected single-mip RT, without
    /// initialized pixels. Caller inheritance remains a frontend capability.</summary>
    public IMapPostEffect3StockGraphicsTargetLease CaptureComposite2TargetLease(
        MapPostEffect3TextureParameterState table,MapPostEffect3Composite2BeforeBindings before,
        IMapPostEffect3FullscreenDrawContextLease actualPostBindingContext,int actualLogicalParameter)
    {
        before.RequireCurrent();RequireComposite2Table(table,before);actualPostBindingContext.RequireCurrentForPass(before.SourcePassIndex);
        if(before.ClearsNativeTarget0||!Source.Passes[before.SourcePassIndex].ActiveBindings.Any(b=>
            b.Role==MapPostEffect3BindingRole.RenderTargetWrite&&b.RegisterIndex==0&&b.ParameterIndex==actualLogicalParameter))
            throw new InvalidDataException("Composite2 RT capture differs from its actual noncleared original RT0 setter.");
        var accepted=table.Require(actualLogicalParameter);
        if(accepted.Reference.PhysicalAllocationParameterIndex is not int physical||accepted.Reference.HasSourceSelectedMipView)
            throw new InvalidDataException("Composite2 bank target cannot replace an external or selected-mip wrapper.");
        var texture=RequireTexture(physical,accepted.Reference.InstanceIndex);
        if(texture.Shape.MipLevels!=1||!texture.Shape.HasRenderTargetPath||
            !texture.NativeFormatViews.TryGetValue(texture.Shape.Formats.RenderTargetFormat0,out var view))
            throw new NotSupportedException("Composite2 bank target requires its actual single-mip render target path.");
        IMapPostEffect3ComputeMipTextureLease? prior=(before.RequiresPriorTargetContents||actualPostBindingContext.ActualRaster.Attachments.Single().EnableBlend)?
            CaptureSourceWrittenTextureLease(physical,texture.InstanceIndex,table.FrameNumber,0,1):null;
        return new Composite2Target(this,table,accepted,texture,view,before,actualPostBindingContext,prior);
    }

    public IMapPostEffect3ComputeMipTextureLease PublishComposite2GraphicsCoverage(
        MapPostEffect3Composite2GraphicsCoverageReceipt sourceReceipt)
    {
        sourceReceipt.RequireCurrentSubmission();var completed=sourceReceipt.Completed;
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(!ReferenceEquals(sourceReceipt.Owner.Bank,this)||!sourceReceipt.HasCompleteTargetPixelDomain||
                !completed.WasPixelDrawSubmitted||completed.Writes is not [var write]||
                write.PhysicalAllocationParameterIndex is not int physical||write.WriterRevision is not long revision||write.NativeMipLevel!=0)
                throw new InvalidDataException("Composite2 complete bank pixels need this actual source owner's full-target submission.");
            var texture=RequireTexture(physical,write.InstanceIndex);var key=(physical,write.InstanceIndex,0u);
            if(texture.Shape.MipLevels!=1||texture.StorageRid!=write.StorageRid||
                write.NativeViewFormat!=texture.Shape.Formats.RenderTargetFormat0||
                write.Extent!=new Vector2I((int)texture.Shape.Width,(int)texture.Shape.Height)||
                !textureContents.TryGetValue(key,out var current)||current.Revision!=revision||
                current.Frame!=completed.FrameNumber||current.Pass!=completed.SourcePassIndex)
                throw new InvalidDataException("Composite2 bank target storage or writer revision changed after the actual draw.");
            textureContents[key]=current with{Complete=true,Evidence=sourceReceipt.Recipe.Before.SourceEvidence+";"+completed.SourceEvidence};
            return CaptureSourceWrittenTextureLease(physical,write.InstanceIndex,completed.FrameNumber,0,1);
        }
    }

    private void RequireComposite2Table(MapPostEffect3TextureParameterState table,MapPostEffect3Composite2BeforeBindings before)
    {
        if(!ReferenceEquals(before.Frame.Allocation,Source)||!ReferenceEquals(table.Allocation,Source)||
            table.FrameNumber!=before.Frame.FrameNumber||table.ContextOwnerIdentity!=Source.ContextOwnerIdentity)
            throw new InvalidDataException("Composite2 typed source/table belongs to another actual bank invocation.");
    }
    private sealed record Composite2IndependentRead(PostProcessResourceBank Bank,
        MapPostEffect3TextureParameterState Table,MapPostEffect3ResolvedTextureParameter Pointer,
        MapPostEffect3DeviceTexture Texture,MapPostEffect3Composite2BeforeBindings Before,
        MapPostEffect3Composite2ReadFootprint Proof):IMapPostEffect3ComputeTextureLease
    {
        public long FrameNumber=>Pointer.FrameNumber;public string ContextOwnerIdentity=>Pointer.ContextOwnerIdentity;
        public int ParameterIndex=>Pointer.ParameterIndex;public int InstanceIndex=>Pointer.Reference.InstanceIndex;
        public Rid StorageRid=>Texture.StorageRid;public Rid ShaderViewRid=>Texture.NativeFormatViews[NativeViewFormat];
        public uint NativeViewFormat=>Texture.Shape.Formats.ShaderResourceFormat0;
        public RenderingDevice.DataFormat DeviceFormat=>ViewFormat(NativeViewFormat);
        public Vector3I Extent=>new((int)Texture.Shape.Width,(int)Texture.Shape.Height,(int)Texture.Shape.Depth);
        public string SourceEvidence=>Proof.SourceEvidence+";actual-allocated-native-view;original-read-retained;no-initialized-pixel-claim";
        public void RequireCurrent()
        {lock(Bank.lifetime)ObjectDisposedException.ThrowIf(Bank.retiring,Bank);Before.RequireCurrent();Proof.RequireCurrent();Table.ValidateCurrent(Pointer);}
    }
    private sealed record Composite2Target(PostProcessResourceBank Bank,MapPostEffect3TextureParameterState Table,
        MapPostEffect3ResolvedTextureParameter Pointer,MapPostEffect3DeviceTexture Texture,Rid TargetViewRid,
        MapPostEffect3Composite2BeforeBindings Before,IMapPostEffect3FullscreenDrawContextLease Context,
        IMapPostEffect3ComputeMipTextureLease? Prior):IMapPostEffect3StockGraphicsReadableTargetLease
    {
        public long FrameNumber=>Pointer.FrameNumber;public string ContextOwnerIdentity=>Pointer.ContextOwnerIdentity;
        public string OwnedFrontendIdentity=>Context.OwnedFrontendIdentity;public string ViewIdentity=>Context.ViewIdentity;
        public int? ParameterIndex=>Pointer.ParameterIndex;public int InstanceIndex=>Texture.InstanceIndex;
        public string ProducerOwnerIdentity=>Pointer.Reference.ProducerOwnerIdentity;public Rid StorageRid=>Texture.StorageRid;
        public uint NativeViewFormat=>Texture.Shape.Formats.RenderTargetFormat0;
        public RenderingDevice.DataFormat DeviceFormat=>ViewFormat(NativeViewFormat);
        public Vector2I Extent=>new((int)Texture.Shape.Width,(int)Texture.Shape.Height);
        public string SourceEvidence=>Before.SourceEvidence+";actual-source-bank-RT0;"+Context.SourceEvidence;
        public void RequireCurrentForPass(int pass)
        {lock(Bank.lifetime)ObjectDisposedException.ThrowIf(Bank.retiring,Bank);Before.RequireCurrent();Table.ValidateCurrent(Pointer);
         Context.RequireCurrentForPass(pass);if(pass!=Before.SourcePassIndex)throw new InvalidDataException("Composite2 bank target belongs to another pass.");}
        public void RequireReadableForPass(int pass)
        {RequireCurrentForPass(pass);(Prior??throw new InvalidDataException("Composite2 blending has no actual prior target pixels.")).RequireReadableMipRange(0,1);}
    }
}
