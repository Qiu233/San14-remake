using Godot;
using San14.Data;

/// <summary>A view of one actual physical mip. This describes allocation/view
/// identity only: creating it never establishes readable contents.</summary>
internal sealed record MapPostEffect3ComputeMipView(Rid ViewRid,Rid StorageRid,
    uint NativeViewFormat,RenderingDevice.DataFormat DeviceFormat,Vector3I Extent,
    uint FirstMipLevel,uint MipLevelCount,bool RetireAfterSubmission)
{
    // Retains the actual producer when Godot reports the parent's UNORM
    // metadata for a source-proven shared sRGB SRV. It grants no new contents.
    public IMapPostEffect3ComputeTextureLease? ActualSampledProducer{get;init;}
}

public sealed partial class PostProcessResourceBank
{
    // The caller holds AcquireBufferExecutionScopeAsync until every descriptor
    // set and temporary view has retired. This also protects texture owners.
    internal Task<MapPostEffect3ComputeMipView> CreateComputeWriteMipViewAsync(
        MapPostEffect3DeviceTexture texture,int mipSelector,int sliceSelector,RenderDeviceResources resources)
    {
        RequireSlice(sliceSelector);
        if(texture.Shape.Dimension!=MapPostEffect3TextureDimension.Texture2D||
            !texture.Shape.AllowsUnorderedAccess||texture.Shape.Formats.UnorderedAccessFormat==0)
            throw new NotSupportedException("Source UAV requires an owned ordinary2D storage texture.");
        // Native compute kind1 -1 selects wrapper+60[0].
        // Kind4 has the same -1=>0 selection.
        int mip=mipSelector==-1?0:mipSelector;
        if(mip<0||(uint)mip>=texture.Shape.MipLevels)
            throw new NotSupportedException("Original UAV mip selector lies outside its actual source texture.");
        return CreateComputeViewAsync(texture.StorageRid,
            texture.NativeFormatViews[texture.Shape.Formats.UnorderedAccessFormat],
            texture.Shape.Formats.UnorderedAccessFormat,
            new(checked((int)texture.Shape.Width),checked((int)texture.Shape.Height),1),
            checked((uint)texture.Shape.MipLevels),mip,true,resources);
    }

    internal async Task<MapPostEffect3ComputeMipView> CreateComputeReadMipViewAsync(
        IMapPostEffect3ComputeTextureLease lease,int mipSelector,int sliceSelector,RenderDeviceResources resources,
        MapPostEffect3LensTextureReadFootprint? actualLensReadFootprint=null)
    {
        RequireSlice(sliceSelector);lease.RequireCurrent();
        if(lease is IMapPostEffect3ComputeSelectedMipTextureLease{IsSourceSelectedMipView:true} selected)
        {
            if(mipSelector!=-1||selected.NativeMipLevelCount==0||string.IsNullOrWhiteSpace(selected.NativeMipSelectionEvidence))
                throw new InvalidDataException("A selected native wrapper must retain its actual SRV mip range, not receive another selector override.");
            selected.RequireReadableMipRange(selected.NativeFirstMipLevel,selected.NativeMipLevelCount);
            return await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
            {
                if(!rd.TextureIsValid(lease.StorageRid)||!rd.TextureIsValid(lease.ShaderViewRid))
                    throw new InvalidDataException("Original selected mip wrapper retired before dispatch.");
                using var storage=rd.TextureGetFormat(lease.StorageRid);using var view=rd.TextureGetFormat(lease.ShaderViewRid);
                uint first=selected.NativeFirstMipLevel,count=selected.NativeMipLevelCount;
                if(storage.TextureType!=RenderingDevice.TextureType.Type2D||storage.ArrayLayers!=1||storage.Depth!=1||
                    first>=storage.Mipmaps||count>storage.Mipmaps-first||first>=32||
                    view.TextureType!=RenderingDevice.TextureType.Type2D||view.ArrayLayers!=1||view.Depth!=1||view.Mipmaps!=count||
                    view.Width!=Math.Max(1u,storage.Width>>checked((int)first))||view.Height!=Math.Max(1u,storage.Height>>checked((int)first))||
                    view.Width!=lease.Extent.X||view.Height!=lease.Extent.Y||view.Format!=lease.DeviceFormat||
                    view.Format!=ViewFormat(lease.NativeViewFormat)||!view.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.SamplingBit))
                    throw new InvalidDataException("Actual original selected mip wrapper differs from its physical range, native format or extent.");
                return new MapPostEffect3ComputeMipView(lease.ShaderViewRid,lease.StorageRid,lease.NativeViewFormat,
                    lease.DeviceFormat,lease.Extent,first,count,false){ActualSampledProducer=lease};
            });
        }
        uint mips=await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            if(!rd.TextureIsValid(lease.ShaderViewRid))throw new InvalidDataException("Source sampled owner retired before mip selection.");
            using var f=rd.TextureGetFormat(lease.ShaderViewRid);
            bool effectiveFormatMatches=f.Format==lease.DeviceFormat;
            if(!effectiveFormatMatches&&lease is IMapPostEffect3SharedSampledViewLease shared)
            {shared.RequireSharedSampledViewProof().RequireCurrentNativeView(lease,rd,f);effectiveFormatMatches=true;}
            if(f.TextureType!=RenderingDevice.TextureType.Type2D||f.ArrayLayers!=1||f.Depth!=1||
                f.Width!=lease.Extent.X||f.Height!=lease.Extent.Y||!effectiveFormatMatches||f.Mipmaps==0)
                throw new InvalidDataException("Source mip capability must retain its exact full ordinary2D native sampled view.");
            return f.Mipmaps;
        });
        if(mipSelector < -1||mipSelector>=0&&(uint)mipSelector>=mips)
            throw new InvalidDataException("Original sampled mip is outside this actual source view.");
        uint first=mipSelector==-1?0u:(uint)mipSelector,count=mipSelector==-1?mips:1u;
        if(actualLensReadFootprint is{} footprint)
        {
            if(mipSelector!=-1||!footprint.RetainsFullSourceShaderResourceView||footprint.ParameterIndex!=lease.ParameterIndex||
                footprint.FrameNumber!=lease.FrameNumber||footprint.ContextOwnerIdentity!=lease.ContextOwnerIdentity||
                footprint.SourceSha256!=Source.Source.Sha256||footprint.RequiredMipLevels.Count==0||
                lease is not IMapPostEffect3ComputeMipTextureLease readable)
                throw new InvalidDataException("Actual Lens read footprint cannot authorize a different view/frame/module or producer.");
            foreach(int mip in footprint.RequiredMipLevels)
            {
                if(mip<0||(uint)mip>=mips)throw new InvalidDataException("Lens exact shader footprint exceeds the actual native whole-SRV.");
                readable.RequireReadableMipRange(checked((uint)mip),1);
            }
        }
        else if(mips>1)
        {
            if(lease is IMapPostEffect3ComputeMipTextureLease readable)readable.RequireReadableMipRange(first,count);
            else throw new NotSupportedException("Full storage readiness does not prove this sampled mip range; a source content capability is required.");
        }
        return await CreateComputeViewAsync(lease.StorageRid,lease.ShaderViewRid,lease.NativeViewFormat,
            lease.Extent,mips,mipSelector,false,resources,lease);
    }

    private static void RequireSlice(int slice)
    {if(slice is not (-1 or 0))throw new NotSupportedException("Original Lens mip adapter only admits ordinary2D slice0 or its absent selector.");}

    private static Task<MapPostEffect3ComputeMipView> CreateComputeViewAsync(Rid storage,Rid fullView,
        uint nativeFormat,Vector3I baseExtent,uint expectedMips,int mipSelector,bool write,RenderDeviceResources resources,
        IMapPostEffect3ComputeTextureLease? actualSampledProducer=null)
        =>TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            if(!rd.TextureIsValid(storage)||!rd.TextureIsValid(fullView))
                throw new InvalidDataException("Actual source storage/native view retired before mip allocation.");
            using var source=rd.TextureGetFormat(fullView);
            var format=ViewFormat(nativeFormat);
            bool effectiveFormatMatches=source.Format==format;
            if(!effectiveFormatMatches&&!write&&actualSampledProducer is IMapPostEffect3SharedSampledViewLease shared)
            {
                if(storage!=actualSampledProducer.StorageRid||fullView!=actualSampledProducer.ShaderViewRid||
                    nativeFormat!=actualSampledProducer.NativeViewFormat||format!=actualSampledProducer.DeviceFormat||
                    baseExtent!=actualSampledProducer.Extent||expectedMips!=1||mipSelector is not(-1 or 0))
                    throw new InvalidDataException("Compute full sampled view differs from the exact actual shared native source.");
                shared.RequireSharedSampledViewProof().RequireCurrentNativeView(actualSampledProducer,rd,source);
                effectiveFormatMatches=true;
            }
            if(source.TextureType!=RenderingDevice.TextureType.Type2D||source.ArrayLayers!=1||source.Depth!=1||
                source.Width!=baseExtent.X||source.Height!=baseExtent.Y||source.Mipmaps!=expectedMips||!effectiveFormatMatches||
                !source.UsageBits.HasFlag(write?RenderingDevice.TextureUsageBits.StorageBit:RenderingDevice.TextureUsageBits.SamplingBit))
                throw new InvalidDataException("Native mip view owner has a different actual format, full extent, mip count or usage.");
            uint first=mipSelector<0?0u:(uint)mipSelector,count=mipSelector<0?expectedMips:1u;
            Vector3I extent=new(Math.Max(1,baseExtent.X>>checked((int)first)),Math.Max(1,baseExtent.Y>>checked((int)first)),1);
            bool temporary=mipSelector>=0&&expectedMips>1;
            Rid actual=fullView;
            if(temporary)
            {
                using var view=new RDTextureView{FormatOverride=format};
                actual=rd.TextureCreateSharedFromSlice(view,storage,0,first,1,RenderingDevice.TextureSliceType.Slice2D);
                if(!actual.IsValid)throw new InvalidDataException("RenderingDevice rejected the exact source2D mip view.");
                resources.Own(actual);
                if(!rd.TextureIsValid(actual))throw new InvalidDataException("RenderingDevice rejected the exact source2D mip view.");
            }
            using var f=rd.TextureGetFormat(actual);
            bool actualFormatMatches=f.Format==format;
            if(!actualFormatMatches&&!write&&!temporary&&actual==fullView&&
                actualSampledProducer is IMapPostEffect3SharedSampledViewLease actualShared)
            {actualShared.RequireSharedSampledViewProof().RequireCurrentNativeView(actualSampledProducer,rd,f);actualFormatMatches=true;}
            if(f.TextureType!=RenderingDevice.TextureType.Type2D||f.ArrayLayers!=1||f.Depth!=1||!actualFormatMatches||
                f.Width!=extent.X||f.Height!=extent.Y||f.Mipmaps!=count||
                !f.UsageBits.HasFlag(write?RenderingDevice.TextureUsageBits.StorageBit:RenderingDevice.TextureUsageBits.SamplingBit))
                throw new InvalidDataException("Actual shared source mip view has a different format/subresource extent.");
            return new MapPostEffect3ComputeMipView(actual,storage,nativeFormat,format,extent,first,count,temporary)
                {ActualSampledProducer=write?null:actualSampledProducer};
        });
}
