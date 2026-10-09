using Godot;
using San14.Data;

public sealed partial class PostProcessResourceBank
{
    private readonly Dictionary<(int Physical,int Side,uint Mip,uint Format),MapPostEffect3ComputeMipView> lensFlare2Views=[];
    internal async Task<MapPostEffect3ComputeMipView> RequireLensFlare2ViewAsync(
        MapPostEffect3DeviceTexture texture,uint mip,uint nativeFormat)
    {
        if(texture.Source.Parameter.Index is not(50 or 55)||mip>=texture.Shape.MipLevels||
            !texture.NativeFormatViews.TryGetValue(nativeFormat,out var full))
            throw new InvalidDataException("LensFlare2 mip view is not its original50/55 wrapper.");
        var key=(texture.Source.Parameter.Index,texture.InstanceIndex,mip,nativeFormat);
        lock(lifetime)if(lensFlare2Views.TryGetValue(key,out var existing))return existing;
        var created=await CreateComputeViewAsync(texture.StorageRid,full,nativeFormat,
            new((int)texture.Shape.Width,(int)texture.Shape.Height,1),(uint)texture.Shape.MipLevels,(int)mip,false);
        lock(lifetime)
        {
            lensFlare2Views.Add(key,created with{RetireAfterSubmission=false});
            if(created.RetireAfterSubmission){owned.Add(created.ViewRid);OwnedRidCount=owned.Count;}
        }
        return created with{RetireAfterSubmission=false};
    }
    public async Task<IMapPostEffect3FullscreenSelectedMipTargetLease> CaptureLensFlare2DiaphragmTargetAsync(
        MapPostEffect3TextureParameterState table,MapPostEffect3LensFlare2GpuPassSource source,
        IMapPostEffect3FullscreenDrawContextLease context)
    {
        using var scope=await AcquireBufferExecutionScopeAsync();source.RequireCurrentTextureTable(table);context.RequireCurrentForPass(45);
        if(!ReferenceEquals(table.Allocation,Source)||source.SourcePassIndex!=45||context.FrameNumber!=table.FrameNumber||
            context.ContextOwnerIdentity!=Source.ContextOwnerIdentity)
            throw new InvalidDataException("LensFlare2 target needs its actual source45/table/frontend.");
        var pointer=table.Require(50);var texture=RequireTexture(50,pointer.Reference.InstanceIndex);
        var view=await RequireLensFlare2ViewAsync(texture,0,texture.Shape.Formats.RenderTargetFormat0);
        source.RequireCurrentTextureTable(table);table.ValidateCurrent(pointer);
        return new LensFlare2Target(this,table,source,pointer,context,view);
    }
    public IMapPostEffect3ComputeMipTextureLease PublishLensFlare2DiaphragmCoverage(
        MapPostEffect3LensFlare2GpuPassSource source,MapPostEffect3FullscreenGraphicsExecutor executor,
        MapPostEffect3FullscreenCompletedPass completed)
    {
        source.RequireCurrent();lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(source.SourcePassIndex!=45||!ReferenceEquals(executor.Bank,this)||!ReferenceEquals(executor.LastCompletedPass,completed)||
                source.SourceSha256!=Source.Source.Sha256||source.ContextOwnerIdentity!=Source.ContextOwnerIdentity||
                completed.SourcePassIndex!=45||completed.FrameNumber!=source.FrameNumber||completed.ContextOwnerIdentity!=source.ContextOwnerIdentity||
                completed.PixelProgramSha256!=source.ProgramSha256||completed.VertexProgramSha256!=source.VertexProgramSha256||
                completed.ActualViewport!=new Rect2I(0,0,(int)source.OutputWidth,(int)source.OutputHeight)||
                completed.ActualScissor is{} scissor&&(scissor.Position.X>0||scissor.Position.Y>0||scissor.End.X<source.OutputWidth||scissor.End.Y<source.OutputHeight)||
                completed.ActualRaster.DepthTest||completed.ActualRaster.DepthWrite||completed.ActualRaster.NativeStencil is not null||
                completed.ActualRaster.Cull!=RenderingDevice.PolygonCullMode.Disabled||
                completed.ActualRaster.Attachments is not [var color]||color.EnableBlend||!color.WriteR||!color.WriteG||!color.WriteB||!color.WriteA||
                completed.ConstantBufferReads is not [{ParameterIndex:17,HasCompleteWriterContents:true} cb]||cb.CompleteBytesSha256!=source.ConstantBufferBytesSha256||
                completed.TextureReads.Count!=0||completed.Writes is not [var write]||write.SourceTargetRegister!=0||
                write.PhysicalAllocationParameterIndex!=50||write.ParameterIndex!=50||write.NativeMipLevel!=0||write.WriterRevision is not{} revision)
                throw new InvalidDataException("LensFlare2 diaphragm complete pixels require its exact current source draw/raster/CB/output receipt.");
            var texture=RequireTexture(50,write.InstanceIndex);var key=(50,write.InstanceIndex,0u);
            if(texture.StorageRid!=write.StorageRid||write.Extent!=new Vector2I(1024,1024)||
                !textureContents.TryGetValue(key,out var current)||current.Revision!=revision||current.Frame!=source.FrameNumber||current.Pass!=45)
                throw new InvalidDataException("LensFlare2 diaphragm actual storage/writer revision changed.");
            textureContents[key]=current with{Complete=true,Evidence=source.SourceEvidence+";"+completed.SourceEvidence};
            return CaptureSourceWrittenTextureLease(50,write.InstanceIndex,source.FrameNumber,0,1);
        }
    }
    public IMapPostEffect3ComputeMipTextureLease PublishLensFlare2ComputeCoverage(
        MapPostEffect3LensFlare2GpuPassSource source,MapPostEffect3ComputeExecutor executor,
        MapPostEffect3ComputeCompletedPass completed)
    {
        source.RequireCurrent();if(source.SourcePassIndex is <46 or >49)throw new InvalidDataException("LensFlare2 GPU source is not a compute initialization pass.");
        return PublishSourceTextureCoverage(executor,completed,source.FrameNumber,source.ContextOwnerIdentity,source.SourceSha256,
            source.SourcePassIndex,source.ProgramSha256,source.OutputParameterIndex,0,source.OutputWidth,source.OutputHeight,
            source.ActualDispatch!,source.LocalSize!,source.SourceEvidence,
            new Dictionary<int,string>{{17,source.ConstantBufferBytesSha256}},source.InputTextureParameters);
    }
    internal IMapPostEffect3ComputeMipTextureLease PublishLensFlare2AfterMipCoverage(
        MapPostEffect3LensFlare2ExecutionOwner owner,MapPostEffect3LensFlare2MipCompletedDraw completed)
    {
        completed.RequireCurrentSubmission();lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(!ReferenceEquals(owner.Bank,this)||!ReferenceEquals(owner.LastCompletedMip,completed)||
                completed.Plan.ContextOwnerIdentity!=Source.ContextOwnerIdentity||completed.Plan.SourceSha256!=Source.Source.Sha256)
                throw new InvalidDataException("LensFlare2 After mip is not this owner's current submitted stock draw.");
            int physical=completed.Plan.ActualTexture.ParameterIndex;uint mip=(uint)completed.Step.DestinationMipLevel;
            var texture=RequireTexture(physical,completed.Side);var key=(physical,completed.Side,mip);
            if(texture.StorageRid!=completed.StorageRid||!textureContents.TryGetValue(key,out var current)||
                current.Revision!=completed.WriterRevision||current.Frame!=completed.Plan.FrameNumber||current.Pass!=completed.Plan.SourcePassIndex)
                throw new InvalidDataException("LensFlare2 stock mip writer revision changed after submission.");
            textureContents[key]=current with{Complete=true,Evidence=completed.SourceEvidence};
            return CaptureSourceWrittenTextureLease(physical,completed.Side,completed.Plan.FrameNumber,mip,1);
        }
    }
    private sealed record LensFlare2Target(PostProcessResourceBank Bank,MapPostEffect3TextureParameterState Table,
        MapPostEffect3LensFlare2GpuPassSource Source,MapPostEffect3ResolvedTextureParameter Pointer,
        IMapPostEffect3FullscreenDrawContextLease Context,MapPostEffect3ComputeMipView View)
        :IMapPostEffect3FullscreenSelectedMipTargetLease
    {
        public long FrameNumber=>Table.FrameNumber;public string ContextOwnerIdentity=>Table.ContextOwnerIdentity;
        public string OwnedFrontendIdentity=>Context.OwnedFrontendIdentity;public string ViewIdentity=>Context.ViewIdentity;
        public int? ParameterIndex=>50;public int InstanceIndex=>Pointer.Reference.InstanceIndex;
        public string ProducerOwnerIdentity=>Pointer.Reference.ProducerOwnerIdentity;public Rid StorageRid=>View.StorageRid;
        public Rid TargetViewRid=>View.ViewRid;public uint NativeViewFormat=>View.NativeViewFormat;
        public RenderingDevice.DataFormat DeviceFormat=>View.DeviceFormat;public Vector2I Extent=>new(View.Extent.X,View.Extent.Y);
        public uint NativeFirstMipLevel=>0;public uint NativeMipLevelCount=>1;
        public string NativeMipSelectionEvidence=>Source.SourceEvidence+";native-kind3-wrapper-RTV+58/mip0";
        public string SourceEvidence=>NativeMipSelectionEvidence+";"+Context.SourceEvidence;
        public void RequireCurrentForPass(int pass)
        {lock(Bank.lifetime)ObjectDisposedException.ThrowIf(Bank.retiring,Bank);Source.RequireCurrentTextureTable(Table);
         Table.ValidateCurrent(Pointer);Context.RequireCurrentForPass(pass);if(pass!=45)throw new InvalidDataException("LensFlare2 diaphragm target belongs to pass45.");}
        public void RequireSourceSelectedTarget(int pass,int register)
        {RequireCurrentForPass(pass);if(register!=0)throw new InvalidDataException("LensFlare2 diaphragm only writes native RT0.");}
    }
}
