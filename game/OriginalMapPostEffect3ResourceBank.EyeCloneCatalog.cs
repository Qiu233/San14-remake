using San14.Data;

public sealed partial class OriginalMapPostEffect3ResourceBank
{
    /// <summary>Create the actual source clone views once in this bank. This
    /// allocates no new pixels and marks no mip as readable or written.</summary>
    internal async Task<IReadOnlyList<(OriginalMapPostEffect3EyeCloneWrapperDirectory.Entry Source,
        OriginalMapPostEffect3DeviceTexture Texture,OriginalMapPostEffect3ComputeMipView Srv,
        OriginalMapPostEffect3ComputeMipView Rtv)>> CaptureEyeCloneViewsAsync(OriginalMapPostEffect3EyeCloneWrapperDirectory source)
    {
        ArgumentNullException.ThrowIfNull(source);source.RequireCurrent();
        if(!ReferenceEquals(source.Frame.Allocation,Source))throw new InvalidDataException("Eye clone view catalog belongs to another bank allocation.");
        using var scope=await AcquireBufferExecutionScopeAsync();
        var result=new List<(OriginalMapPostEffect3EyeCloneWrapperDirectory.Entry,OriginalMapPostEffect3DeviceTexture,
            OriginalMapPostEffect3ComputeMipView,OriginalMapPostEffect3ComputeMipView)>();
        foreach(var entry in source.Entries)
        {
            var texture=RequireTexture(entry.PhysicalParameterIndex,entry.InstanceIndex);
            if(texture.Shape.Width!=entry.NativeWrapperWidth40||texture.Shape.Height!=entry.NativeWrapperHeight42||
                texture.Shape.MipLevels!=entry.Plan.NativeMipCount||!texture.Shape.HasRenderTargetPath)
                throw new InvalidDataException("Actual Eye clone physical wrapper differs from its native array recipe.");
            var srv=await RequireEyeMipViewAsync(texture,(uint)entry.NativeMipLevel,texture.Shape.Formats.ShaderResourceFormat0);
            var rtv=await RequireEyeMipViewAsync(texture,(uint)entry.NativeMipLevel,texture.Shape.Formats.RenderTargetFormat0);
            if(srv.StorageRid!=texture.StorageRid||rtv.StorageRid!=texture.StorageRid||srv.FirstMipLevel!=entry.NativeMipLevel||
                rtv.FirstMipLevel!=entry.NativeMipLevel||srv.MipLevelCount!=1||rtv.MipLevelCount!=1||srv.Extent!=rtv.Extent||
                !srv.ViewRid.IsValid||!rtv.ViewRid.IsValid)
                throw new InvalidDataException("Actual Eye clone SRV/RTV does not retain one exact physical mip.");
            result.Add((entry,texture,srv,rtv));
        }
        source.RequireCurrent();return result.AsReadOnly();
    }
}
