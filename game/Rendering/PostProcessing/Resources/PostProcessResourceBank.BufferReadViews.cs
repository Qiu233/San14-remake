using Godot;

public sealed partial class PostProcessResourceBank
{
    private readonly Dictionary<(int Index,int Side),(Rid View,long Revision)> bufferShaderReadViews=[];

    /// <summary>Exact completed StorageBuffer bytes bridged to vkd3d's readonly
    /// TextureBuffer SRV representation. The small initial Lens448B bridge is
    /// deliberately synchronous GPU readback; it never guesses source contents.
    /// Future GPU-only bridging can replace this backend implementation.</summary>
    public async Task<IMapPostEffect3ComputeBufferLease> PrepareBufferShaderReadViewAsync(int index,int side,long frame)
    {
        using var scope=await AcquireBufferExecutionScopeAsync();
        var buffer=RequireCurrentBuffer(index,side,frame);
        if(!buffer.NativeAllowsShaderRead)throw new InvalidDataException("Current source buffer has no original SRV declaration.");
        if(!buffer.UsesStorageBuffer)return CaptureCurrentBufferLease(index,side,frame);
        if(buffer.DeviceTexelFormat!=RenderingDevice.DataFormat.R32Uint||buffer.Source.ByteCount>4096)
            throw new NotSupportedException("Initial StorageBuffer-to-texel bridge is bounded to actual small uint32 Lens resources.");
        var key=(index,side);
        lock(lifetime)
            if(bufferShaderReadViews.TryGetValue(key,out var cached)&&cached.Revision==buffer.UploadRevision&&buffer.PreparedShaderReadRid==cached.View)
                return CaptureCurrentBufferLease(index,side,frame);
        long revision=buffer.UploadRevision;
        await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            byte[] bytes=rd.BufferGetData(buffer.StorageRid,0,buffer.Source.ByteCount);
            if(bytes.Length!=buffer.Source.ByteCount)throw new InvalidDataException("Completed source StorageBuffer bridge readback is incomplete.");
            var group=BeginResources(buffer.StorageRid);
            bool installed=false;Rid old=default;
            try
            {
                Rid view=rd.TextureBufferCreate(buffer.TexelCount,buffer.DeviceTexelFormat,bytes);
                if(!view.IsValid)throw new InvalidDataException("Device rejected completed source readonly buffer SRV bridge.");
                group.Resources.Own(view);
                lock(lifetime)
                {
                    if(!ReferenceEquals(RequireCurrentBuffer(index,side,frame),buffer)||buffer.UploadRevision!=revision)
                        throw new InvalidDataException("Source buffer changed while its serialized readonly bridge was prepared.");
                    if(bufferShaderReadViews.TryGetValue(key,out var previous))old=previous.View;
                    if(old.IsValid&&!owned.Any(group=>group.Resources.Contains(old)))
                        throw new InvalidDataException("Readonly buffer view lost bank ownership.");
                    bufferShaderReadViews[key]=(view,revision);buffer.PreparedShaderReadRid=view;installed=true;
                }
                if(old.IsValid)RetireOwnedRid(rd,old);return true;
            }
            catch(Exception failure)
            {
                if(!installed)ReleaseResourcesAfterFailure(rd,group,failure);
                throw;
            }
        });
        return CaptureCurrentBufferLease(index,side,frame);
    }
}
