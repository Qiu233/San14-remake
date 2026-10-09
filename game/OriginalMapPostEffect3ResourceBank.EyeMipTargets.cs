using Godot;
using San14.Data;

public sealed partial class OriginalMapPostEffect3ResourceBank
{
    public async Task<IOriginalMapPostEffect3FullscreenSelectedMipTargetLease> CaptureEyeMipTargetLeaseAsync(
        OriginalMapPostEffect3TextureParameterState table,OriginalMapPostEffect3EyeAdaptation2Frame frame,
        OriginalMapPostEffect3EyeBeforeBindings before,IOriginalMapPostEffect3FullscreenDrawContextLease actualDrawContext)
    {
        ArgumentNullException.ThrowIfNull(table);ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(before);ArgumentNullException.ThrowIfNull(actualDrawContext);
        using var scope=await AcquireBufferExecutionScopeAsync();RequireEyeBefore(table,frame,before);
        int pass=before.Iteration.SourcePassIndex;actualDrawContext.RequireCurrentForPass(pass);
        if(before.TargetTextureParameterIndex is not (85 or 86)||before.TargetMipLevel is not{} mip||mip<0||
            actualDrawContext.FrameNumber!=frame.FrameNumber||actualDrawContext.ContextOwnerIdentity!=Source.ContextOwnerIdentity)
            throw new InvalidDataException("Eye target requires its actual current selected physical mip and draw context.");
        int physical=before.TargetTextureParameterIndex.Value;
        var accepted=table.Require(physical);
        if(accepted.Reference.PhysicalAllocationParameterIndex!=physical||accepted.Reference.HasSourceSelectedMipView)
            throw new InvalidDataException("Eye target cloning requires the full counted85/86 wrapper.");
        var texture=RequireTexture(physical,accepted.Reference.InstanceIndex);
        if(!texture.Shape.HasRenderTargetPath||texture.Shape.Formats.RenderTargetFormat0==0)
            throw new InvalidDataException("Eye source selected wrapper has no native render target view.");
        var view=await RequireEyeMipViewAsync(texture,(uint)mip,texture.Shape.Formats.RenderTargetFormat0);
        await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            using var format=rd.TextureGetFormat(view.ViewRid);
            if(!format.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.ColorAttachmentBit))
                throw new InvalidDataException("Actual Eye selected mip cannot be used as a color target.");
            return true;
        });
        IOriginalMapPostEffect3ComputeMipTextureLease? prior=null;
        if(before.RequiresTemporaryBlendOverride)
            prior=CaptureSourceWrittenTextureLease(physical,texture.InstanceIndex,frame.FrameNumber,(uint)mip,1);
        frame.RequireCurrent();table.ValidateCurrent(accepted);actualDrawContext.RequireCurrentForPass(pass);
        return new EyeSelectedMipTargetLease(this,table,accepted,frame,before,actualDrawContext,view,prior);
    }

    private sealed record EyeSelectedMipTargetLease(OriginalMapPostEffect3ResourceBank Bank,
        OriginalMapPostEffect3TextureParameterState Table,OriginalMapPostEffect3ResolvedTextureParameter Accepted,
        OriginalMapPostEffect3EyeAdaptation2Frame Frame,OriginalMapPostEffect3EyeBeforeBindings Before,
        IOriginalMapPostEffect3FullscreenDrawContextLease DrawContext,OriginalMapPostEffect3ComputeMipView View,
        IOriginalMapPostEffect3ComputeMipTextureLease? PriorContents)
        :IOriginalMapPostEffect3FullscreenSelectedMipTargetLease,IOriginalMapPostEffect3StockGraphicsReadableTargetLease
    {
        public long FrameNumber=>Frame.FrameNumber;
        public string ContextOwnerIdentity=>Accepted.ContextOwnerIdentity;
        public string OwnedFrontendIdentity=>DrawContext.OwnedFrontendIdentity;
        public string ViewIdentity=>DrawContext.ViewIdentity;
        public int? ParameterIndex=>Accepted.ParameterIndex;
        public int InstanceIndex=>Accepted.Reference.InstanceIndex;
        public string ProducerOwnerIdentity=>Accepted.Reference.ProducerOwnerIdentity;
        public Rid StorageRid=>View.StorageRid;
        public Rid TargetViewRid=>View.ViewRid;
        public uint NativeViewFormat=>View.NativeViewFormat;
        public RenderingDevice.DataFormat DeviceFormat=>View.DeviceFormat;
        public Vector2I Extent=>new(View.Extent.X,View.Extent.Y);
        public uint NativeFirstMipLevel=>View.FirstMipLevel;
        public uint NativeMipLevelCount=>View.MipLevelCount;
        public string NativeMipSelectionEvidence=>Before.SourceEvidence+";source-cloned-mip-RTV;native-RT0-setter";
        public string SourceEvidence=>NativeMipSelectionEvidence+";"+DrawContext.SourceEvidence;
        public void RequireCurrentForPass(int pass)
        {
            lock(Bank.lifetime){ObjectDisposedException.ThrowIf(Bank.retiring,Bank);}
            Frame.RequireCurrent();Table.ValidateCurrent(Accepted);DrawContext.RequireCurrentForPass(pass);
            if(pass!=Before.Iteration.SourcePassIndex||Before.TargetTextureParameterIndex!=Accepted.ParameterIndex||
                Before.TargetMipLevel!=NativeFirstMipLevel)
                throw new InvalidDataException("Eye target belongs to another current native Before setter.");
        }
        public void RequireSourceSelectedTarget(int pass,int targetRegister)
        {RequireCurrentForPass(pass);if(targetRegister!=0)
             throw new InvalidDataException("Eye cloned target writer only selects native RT0.");}
        public void RequireReadableForPass(int pass)
        {
            RequireCurrentForPass(pass);
            if(PriorContents is null)throw new InvalidDataException("Eye target has no accepted prior pixel producer for destination blending.");
            PriorContents.RequireReadableMipRange(NativeFirstMipLevel,NativeMipLevelCount);
        }
    }
}
