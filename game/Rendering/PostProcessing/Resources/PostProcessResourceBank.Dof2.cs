using Godot;
using San14.Data;

public sealed partial class PostProcessResourceBank
{
    public IMapPostEffect3ComputeTextureLease CaptureDepthOfField2IndependentExposureRead(
        MapPostEffect3TextureParameterState table,MapPostEffect3DepthOfField2GraphicsSource proof,
        MapPostEffect3Binding row)
    {
        RequireDepthOfField2Table(table,proof);
        if(!proof.AllowsUninitializedExposureLiteralRead||!proof.EffectivePixelTextureReads.Contains(row)||
            row.ParameterIndex!=61||row.RegisterIndex!=2)
            throw new InvalidDataException("DOF2 exposure read exemption requires original PS10/11 literal t2 and its actual CB18+12 branch.");
        var pointer=table.Require(61);
        if(pointer.Reference.PhysicalAllocationParameterIndex is not int physical||pointer.Reference.HasSourceSelectedMipView)
            throw new InvalidDataException("DOF2 allocated exposure read cannot replace an external/current selected-mip producer.");
        var texture=RequireTexture(physical,pointer.Reference.InstanceIndex);
        if(texture.Shape.MipLevels!=1||texture.Shape.Depth!=1||texture.Shape.Formats.ShaderResourceFormat0!=41)
            throw new InvalidDataException("DOF2 original literal exposure allocation is not its actual R32F single native mip.");
        return new Dof2IndependentExposureRead(this,table,proof,pointer,texture);
    }
    public MapPostEffect3DepthOfField2TargetView ReadDepthOfField2ScatterTargetView(
        MapPostEffect3TextureParameterState table,MapPostEffect3ParameterDepthOfField2Update prepared,
        MapPostEffect3DepthOfField2BeforeBindings before)
    {
        prepared.RequireCurrentTextureTable(table);before.RequireCurrent();int pass=before.SourcePassIndex;
        if(!ReferenceEquals(table.Allocation,Source)||pass is not(62 or 63)||!before.RequiresNativeIndirectDraw)
            throw new InvalidDataException("DOF2 scatter target requires this actual selected source/table/bank.");
        var row=Source.Passes[pass].ActiveBindings.Last(b=>b.Role==MapPostEffect3BindingRole.RenderTargetWrite&&b.RegisterIndex==0);
        var pointer=table.Require(row.ParameterIndex);
        if(row.ParameterIndex!=(pass==62?63:64)||pointer.Reference.PhysicalAllocationParameterIndex!=row.ParameterIndex||pointer.Reference.HasSourceSelectedMipView)
            throw new InvalidDataException("DOF2 scatter target must preserve its source double-sided physical allocation.");
        var texture=RequireTexture(row.ParameterIndex,pointer.Reference.InstanceIndex);
        if(texture.Shape.MipLevels!=1||texture.Shape.Depth!=1||texture.Shape.Formats.RenderTargetFormat0!=10||!texture.Shape.HasRenderTargetPath)
            throw new InvalidDataException("DOF2 scatter requires its original RGBA16F single-mip2D target.");
        return new(0,pointer,texture.StorageRid,texture.NativeFormatViews[10],10,RenderingDevice.DataFormat.R16G16B16A16Sfloat,
            new((int)texture.Shape.Width,(int)texture.Shape.Height),before.SourceEvidence+";actual-native-scatter-target63/64-current-wrapper-side");
    }

    public IMapPostEffect3ComputeMipTextureLease PublishDepthOfField2ScatterCoverage(
        MapPostEffect3DepthOfField2ScatterExecutor actualScatter,MapPostEffect3FullscreenCompletedPass completed)
    {
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(!ReferenceEquals(actualScatter.Bank,this)||!ReferenceEquals(actualScatter.LastCompletedPass,completed)||
                actualScatter.LastInputs is not{} input||actualScatter.LastStagingCopy is not{} copy||
                completed.SourcePassIndex is not(62 or 63)||
                (input.Before.RequiresNativeClearMaskAndColorOverride?
                    input.Before.NativeClearMaskContext1C18!=15||input.Before.NativeClearColorRgba8Context1C1C!=0||input.PriorDestination is not null:
                    input.PriorDestination is null)||!completed.WasPixelDrawSubmitted||completed.Writes is not[var write]||
                write.PhysicalAllocationParameterIndex is not int physical||write.WriterRevision is not long revision||write.NativeMipLevel!=0||
                physical!=(completed.SourcePassIndex==62?63:64)||write.NativeViewFormat!=10||
                copy.SourcePassIndex!=completed.SourcePassIndex||copy.ByteCount!=32||copy.SourceSide28!=input.Before.ActualStagingSide28||
                copy.ActualArgumentsStorageRid!=input.ActualArguments.StorageRid)
                throw new InvalidDataException("DOF2 scatter pixels require the actual source conditional-clear/preserve, staging-copy and indirect submission.");
            input.Prepared.RequireCurrentTextureTable(input.TextureParameters);input.Before.RequireCurrent();input.ActualSpritePrefix.RequireCurrent();
            var texture=RequireTexture(physical,write.InstanceIndex);var key=(physical,write.InstanceIndex,0u);
            if(texture.StorageRid!=write.StorageRid||texture.NativeFormatViews[10]!=write.TargetViewRid||texture.Shape.MipLevels!=1||
                write.Extent!=new Vector2I((int)texture.Shape.Width,(int)texture.Shape.Height)||
                !textureContents.TryGetValue(key,out var current)||current.Revision!=revision||current.Frame!=completed.FrameNumber||
                current.Pass!=completed.SourcePassIndex||completed.ActualViewport!=new Rect2I(Vector2I.Zero,write.Extent)||
                completed.ActualScissor!=completed.ActualViewport)
                throw new InvalidDataException("DOF2 actual clear/scatter storage, extent, writer revision or pixel domain changed.");
            if(!write.PriorDestinationPixelsWereValidated||input.PriorDestination is{} prior&&
                (prior.StorageRid!=write.StorageRid||prior.ParameterIndex!=physical||prior.InstanceIndex!=write.InstanceIndex||
                    prior.WriterFrameNumber>=completed.FrameNumber||revision!=checked(prior.WriterRevision+1)))
                throw new InvalidDataException("Source no-clear scatter lacks exact previously validated destination pixels and adjacent writer revision.");
            textureContents[key]=current with{Complete=true,Evidence=completed.SourceEvidence+
                (input.Before.RequiresNativeClearMaskAndColorOverride?";original-Bind-full-clear-provides-all-prior-pixels":
                    ";actual-older-complete-destination-preserved-with-original-writer")+
                ";partial-sprite-draw-preserves-complete-outside"};
            return CaptureSourceWrittenTextureLease(physical,write.InstanceIndex,completed.FrameNumber,0,1);
        }
    }
    public MapPostEffect3DepthOfField2TargetView ReadDepthOfField2TargetView(
        MapPostEffect3TextureParameterState table,MapPostEffect3DepthOfField2GraphicsSource proof,
        int register,MapPostEffect3ResolvedTextureParameter pointer)
    {
        RequireDepthOfField2Table(table,proof);table.ValidateCurrent(pointer);
        if(!proof.ReadEffectiveTargets().Any(t=>t.Register==register&&t.Texture==pointer)||
            pointer.Reference.PhysicalAllocationParameterIndex is not int physical||pointer.Reference.HasSourceSelectedMipView)
            throw new InvalidDataException("DOF2 bank RT requires its actual effective source target wrapper.");
        var texture=RequireTexture(physical,pointer.Reference.InstanceIndex);
        if(texture.Shape.MipLevels!=1||texture.Shape.Depth!=1||!texture.Shape.HasRenderTargetPath||
            !texture.NativeFormatViews.TryGetValue(texture.Shape.Formats.RenderTargetFormat0,out var view))
            throw new NotSupportedException("DOF2 bank target requires its exact single-mip2D native RTV.");
        return new(register,pointer,texture.StorageRid,view,texture.Shape.Formats.RenderTargetFormat0,
            ViewFormat(texture.Shape.Formats.RenderTargetFormat0),new((int)texture.Shape.Width,(int)texture.Shape.Height),
            proof.SourceEvidence+";actual-bank-effective-RT"+register);
    }

    public IMapPostEffect3StockGraphicsTargetLease CaptureDepthOfField2Target(
        MapPostEffect3TextureParameterState table,MapPostEffect3DepthOfField2GraphicsSource proof,
        MapPostEffect3DepthOfField2TargetView actualView,IMapPostEffect3FullscreenDrawContextLease actualContext)
    {
        actualContext.RequireCurrentForPass(proof.SourcePassIndex);
        if(actualView!=ReadDepthOfField2TargetView(table,proof,actualView.SourceTargetRegister,actualView.ActualPointer))
            throw new InvalidDataException("DOF2 bank target view changed after source Bind.");
        IMapPostEffect3ComputeMipTextureLease? prior=actualContext.ActualRaster.Attachments[actualView.SourceTargetRegister].EnableBlend?
            CaptureSourceWrittenTextureAliasLease(table,actualView.ActualPointer.ParameterIndex,table.FrameNumber,0,1):null;
        return new Dof2Target(this,table,proof,actualView,actualContext,prior);
    }

    public IMapPostEffect3ComputeTextureLease CaptureDepthOfField2UnusedHistoryRead(
        MapPostEffect3TextureParameterState table,MapPostEffect3DepthOfField2GraphicsSource proof,
        MapPostEffect3Binding actualRead)
    {
        RequireDepthOfField2Table(table,proof);
        if(!proof.AllowsAllocatedOnlyUnusedHistorySource||!proof.EffectivePixelTextureReads.Contains(actualRead)||
            actualRead.ParameterIndex!=60||actualRead.RegisterIndex!=0)
            throw new InvalidDataException("Allocated-only DOF2 source is permitted only for actual unused PS16 history-copy t0/60.");
        var pointer=table.Require(60);
        if(pointer.Reference.PhysicalAllocationParameterIndex!=60||pointer.Reference.HasSourceSelectedMipView)
            throw new InvalidDataException("DOF2 unused-history read cannot replace another physical producer or mip selection.");
        var texture=RequireTexture(60,pointer.Reference.InstanceIndex);
        if(texture.Shape.MipLevels!=1||texture.Shape.Depth!=1||texture.Shape.Formats.ShaderResourceFormat0!=54)
            throw new InvalidDataException("DOF2 PS16 source allocation is not its original R16F single mip.");
        return new Dof2UnusedHistoryRead(this,table,proof,pointer,texture);
    }

    public IMapPostEffect3ComputeMipTextureLease PublishDepthOfField2GraphicsCoverage(
        MapPostEffect3DepthOfField2GraphicsCoverageReceipt receipt,int register)
    {
        receipt.RequireCurrentSubmission();var completed=receipt.Completed;
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            var write=completed.Writes.Single(w=>w.SourceTargetRegister==register);
            if(!ReferenceEquals(receipt.Owner.Bank,this)||!receipt.CompleteTargetPixelDomains.TryGetValue(register,out bool complete)||
                !complete||receipt.UsedAllocatedOnlyUnusedHistorySource||!completed.WasPixelDrawSubmitted||
                write.PhysicalAllocationParameterIndex is not int physical||write.WriterRevision is not long revision||write.NativeMipLevel!=0)
                throw new InvalidDataException("DOF2 readable bank pixels require this actual owner's full target output/read coverage.");
            var texture=RequireTexture(physical,write.InstanceIndex);var key=(physical,write.InstanceIndex,0u);
            if(texture.Shape.MipLevels!=1||texture.StorageRid!=write.StorageRid||
                texture.Shape.Formats.RenderTargetFormat0!=write.NativeViewFormat||
                write.Extent!=new Vector2I((int)texture.Shape.Width,(int)texture.Shape.Height)||
                !textureContents.TryGetValue(key,out var current)||current.Revision!=revision||
                current.Frame!=completed.FrameNumber||current.Pass!=completed.SourcePassIndex)
                throw new InvalidDataException("DOF2 bank target writer revision, view or actual extent changed.");
            textureContents[key]=current with{Complete=true,Evidence=receipt.Source.SourceEvidence+";"+completed.SourceEvidence};
            return CaptureSourceWrittenTextureLease(physical,write.InstanceIndex,completed.FrameNumber,0,1);
        }
    }

    private void RequireDepthOfField2Table(MapPostEffect3TextureParameterState table,
        MapPostEffect3DepthOfField2GraphicsSource proof)
    {
        proof.RequireCurrentTextureTable(table);
        if(!ReferenceEquals(table.Allocation,Source)||
            table.FrameNumber!=proof.FrameNumber||table.ContextOwnerIdentity!=proof.ContextOwnerIdentity)
            throw new InvalidDataException("DOF2 source/table belongs to another actual bank invocation.");
    }

    private sealed record Dof2Target(PostProcessResourceBank Bank,MapPostEffect3TextureParameterState Table,
        MapPostEffect3DepthOfField2GraphicsSource Proof,MapPostEffect3DepthOfField2TargetView View,
        IMapPostEffect3FullscreenDrawContextLease Context,IMapPostEffect3ComputeMipTextureLease? Prior)
        :IMapPostEffect3StockGraphicsReadableTargetLease
    {
        public long FrameNumber=>Table.FrameNumber;public string ContextOwnerIdentity=>Table.ContextOwnerIdentity;
        public string OwnedFrontendIdentity=>Context.OwnedFrontendIdentity;public string ViewIdentity=>Context.ViewIdentity;
        public int? ParameterIndex=>View.ActualPointer.ParameterIndex;public int InstanceIndex=>View.ActualPointer.Reference.InstanceIndex;
        public string ProducerOwnerIdentity=>View.ActualPointer.Reference.ProducerOwnerIdentity;
        public Rid StorageRid=>View.StorageRid;public Rid TargetViewRid=>View.TargetViewRid;
        public uint NativeViewFormat=>View.NativeViewFormat;public RenderingDevice.DataFormat DeviceFormat=>View.DeviceFormat;
        public Vector2I Extent=>View.Extent;public string SourceEvidence=>View.SourceEvidence+";"+Context.SourceEvidence;
        public void RequireCurrentForPass(int pass)
        {lock(Bank.lifetime)ObjectDisposedException.ThrowIf(Bank.retiring,Bank);Proof.RequireCurrent();Table.ValidateCurrent(View.ActualPointer);
         Context.RequireCurrentForPass(pass);if(pass!=Proof.SourcePassIndex)throw new InvalidDataException("DOF2 target belongs to another source pass.");}
        public void RequireReadableForPass(int pass)
        {RequireCurrentForPass(pass);(Prior??throw new InvalidDataException("DOF2 blend requires its actual prior complete destination pixels.")).RequireReadableMipRange(0,1);}
    }
    private sealed record Dof2UnusedHistoryRead(PostProcessResourceBank Bank,MapPostEffect3TextureParameterState Table,
        MapPostEffect3DepthOfField2GraphicsSource Proof,MapPostEffect3ResolvedTextureParameter Pointer,
        MapPostEffect3DeviceTexture Texture):IMapPostEffect3ComputeTextureLease
    {
        public long FrameNumber=>Table.FrameNumber;public string ContextOwnerIdentity=>Table.ContextOwnerIdentity;
        public int ParameterIndex=>60;public int InstanceIndex=>Pointer.Reference.InstanceIndex;
        public Rid StorageRid=>Texture.StorageRid;public Rid ShaderViewRid=>Texture.NativeFormatViews[54];
        public uint NativeViewFormat=>54;public RenderingDevice.DataFormat DeviceFormat=>RenderingDevice.DataFormat.R16Sfloat;
        public Vector3I Extent=>new((int)Texture.Shape.Width,(int)Texture.Shape.Height,1);
        public string SourceEvidence=>Proof.SourceEvidence+";native-PS16-load-retained;output-history-unused-by-current-color;allocated-pixels-not-initialized";
        public void RequireCurrent()
        {lock(Bank.lifetime)ObjectDisposedException.ThrowIf(Bank.retiring,Bank);Proof.RequireCurrent();Table.ValidateCurrent(Pointer);
         if(!Proof.AllowsAllocatedOnlyUnusedHistorySource)throw new InvalidDataException("DOF2 source history became a selected color input.");}
    }
    private sealed record Dof2IndependentExposureRead(PostProcessResourceBank Bank,MapPostEffect3TextureParameterState Table,
        MapPostEffect3DepthOfField2GraphicsSource Proof,MapPostEffect3ResolvedTextureParameter Pointer,
        MapPostEffect3DeviceTexture Texture):IMapPostEffect3ComputeTextureLease
    {
        public long FrameNumber=>Table.FrameNumber;public string ContextOwnerIdentity=>Table.ContextOwnerIdentity;
        public int ParameterIndex=>61;public int InstanceIndex=>Pointer.Reference.InstanceIndex;
        public Rid StorageRid=>Texture.StorageRid;public Rid ShaderViewRid=>Texture.NativeFormatViews[41];
        public uint NativeViewFormat=>41;public RenderingDevice.DataFormat DeviceFormat=>RenderingDevice.DataFormat.R32Sfloat;
        public Vector3I Extent=>new((int)Texture.Shape.Width,(int)Texture.Shape.Height,1);
        public string SourceEvidence=>Proof.SourceEvidence+";original-PS10/11-t2-ld(0,0)-retained;CB18+12-lt0=false/output-independent;no-exposure-pixels-ready-claim";
        public void RequireCurrent()
        {lock(Bank.lifetime)ObjectDisposedException.ThrowIf(Bank.retiring,Bank);Proof.RequireCurrent();Table.ValidateCurrent(Pointer);
         if(!Proof.AllowsUninitializedExposureLiteralRead)throw new InvalidDataException("DOF2 actual source branch depends on exposure pixels.");}
    }
}
