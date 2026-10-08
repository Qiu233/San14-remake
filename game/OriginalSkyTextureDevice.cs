using Godot;
using San14.Data;
using San14.Data.Formats;
using System.Security.Cryptography;

/// <summary>Exact source sky image upload, with caller-selected native SRV format.
/// Keeps volume depth separate from array layers and retains all encoded mips.</summary>
public sealed class OriginalSkyTextureDevice : IAsyncDisposable
{
    public LoadedTextureContainer Source { get; }
    public G1TextureImage Image { get; }
    public uint NativeViewFormat { get; }
    public IReadOnlyList<string> MipSha256 { get; }
    public Rid TextureRid { get; }
    private Task? _disposal;

    private OriginalSkyTextureDevice(LoadedTextureContainer source,G1TextureImage image,uint nativeFormat,
        string[] hashes,Rid rid)=>(Source,Image,NativeViewFormat,MipSha256,TextureRid)=
            (source,image,nativeFormat,Array.AsReadOnly(hashes),rid);

    public static async Task<OriginalSkyTextureDevice> BuildAsync(LoadedTextureContainer source,int imageIndex,
        uint explicitNativeViewFormat)
    {
        ArgumentNullException.ThrowIfNull(source);
        if(Convert.ToHexString(SHA256.HashData(source.Source.Bytes))!=source.Source.DecodedSha256)
            throw new InvalidDataException("Sky texture source differs from its decoded hash.");
        var canonical=G1TextureContainerReader.Read(source.Source.Bytes);
        if((uint)imageIndex>=canonical.Textures.Count)
            throw new InvalidDataException("Sky texture image index lies outside its explicit container.");
        var image=canonical.Textures[imageIndex];
        if(image.LayerCount!=1 || image.SubresourceLayout is not
            (G1TextureSubresourceLayout.SingleLayerMipChain or G1TextureSubresourceLayout.VolumeMipChain))
            throw new InvalidDataException("Sky upload requires a verified 2D or volume mip chain.");
        var format=(image.PixelFormat,explicitNativeViewFormat) switch {
            (0x01,87)=>RenderingDevice.DataFormat.B8G8R8A8Unorm,
            (0x01,91)=>RenderingDevice.DataFormat.B8G8R8A8Srgb,
            (0x03,10)=>RenderingDevice.DataFormat.R16G16B16A16Sfloat,
            (0x59,71)=>RenderingDevice.DataFormat.Bc1RgbaUnormBlock,
            (0x59,72)=>RenderingDevice.DataFormat.Bc1RgbaSrgbBlock,
            (0x5F,98)=>RenderingDevice.DataFormat.Bc7UnormBlock,
            (0x5F,99)=>RenderingDevice.DataFormat.Bc7SrgbBlock,
            _=>throw new InvalidDataException("Sky source encoding and explicit native SRV format do not match.")};
        var hashes=new string[image.MipCount];
        for(int mip=0;mip<image.MipCount;mip++)
            hashes[mip]=Convert.ToHexString(SHA256.HashData(image.TextureSystem==2
                ?image.GetEncodedVolumeMip(source.Source.Bytes,mip):image.GetEncodedMip(source.Source.Bytes,mip)));
        byte[] bytes=image.EncodedDataRange.Slice(source.Source.Bytes).ToArray();
        Rid rid=await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            var usage=RenderingDevice.TextureUsageBits.SamplingBit|RenderingDevice.TextureUsageBits.CanCopyFromBit;
            if(!rd.TextureIsFormatSupportedForUsage(format,usage))
                throw new NotSupportedException("Device cannot retain the requested original sky texture encoding.");
            using var shape=new RDTextureFormat{Format=format,Width=(uint)image.Width,Height=(uint)image.Height,
                Depth=(uint)image.Depth,ArrayLayers=1,Mipmaps=(uint)image.MipCount,UsageBits=usage,
                TextureType=image.TextureSystem==2?RenderingDevice.TextureType.Type3D:RenderingDevice.TextureType.Type2D};
            using var view=new RDTextureView();Rid created=rd.TextureCreate(shape,view,[bytes]);
            if(!created.IsValid)throw new InvalidDataException("Device rejected the exact original sky mip chain.");
            try
            {
                using var actual=rd.TextureGetFormat(created);
                if(actual.Width!=image.Width || actual.Height!=image.Height || actual.Depth!=image.Depth ||
                    actual.ArrayLayers!=1 || actual.Mipmaps!=image.MipCount || actual.Format!=format ||
                    actual.TextureType!=shape.TextureType)
                    throw new InvalidDataException("Device changed the original sky texture shape or encoding.");
                return created;
            }
            catch{rd.FreeRid(created);throw;}
        });
        return new(source,image,explicitNativeViewFormat,hashes,rid);
    }

    public Task<byte[]> ReadEncodedChainAsync()
    {
        ObjectDisposedException.ThrowIf(_disposal is not null,this);
        return OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>rd.TextureGetData(TextureRid,0));
    }

    private readonly object disposalGate=new();
    public ValueTask DisposeAsync()
    {
        lock(disposalGate)
        {
            if(_disposal is null||_disposal.IsFaulted||_disposal.IsCanceled)
                _disposal=OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
                {if(rd.TextureIsValid(TextureRid))rd.FreeRid(TextureRid);return true;});
            return new(_disposal);
        }
    }
}
