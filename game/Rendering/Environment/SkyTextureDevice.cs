using Godot;
using San14.Data;

/// <summary>Exact source sky image upload, with caller-selected native SRV format.
/// Keeps volume depth separate from array layers and retains all encoded mips.</summary>
public sealed class SkyTextureDevice : IAsyncDisposable
{
    public SkyTextureSource Source { get; }
    public uint NativeViewFormat => Source.NativeViewFormat;
    public IReadOnlyList<string> MipSha256 => Source.MipSha256;
    public Rid TextureRid { get; }
    internal RenderingDevice.DataFormat Format { get; }
    private readonly bool allowReadback;
    private Task? _disposal;

    private SkyTextureDevice(SkyTextureSource source,Rid rid,RenderingDevice.DataFormat format,bool allowReadback)
        =>(Source,TextureRid,Format,this.allowReadback)=(source,rid,format,allowReadback);

    public static Task<SkyTextureDevice> BuildAsync(LoadedTextureContainer source,int imageIndex,
        uint explicitNativeViewFormat)
        => BuildAsync(SkyTextureSource.Import(source,imageIndex,explicitNativeViewFormat));

    public static async Task<SkyTextureDevice> BuildAsync(SkyTextureSource source,bool allowReadback=true)
    {
        ArgumentNullException.ThrowIfNull(source);
        var format=source.NativeViewFormat switch {
            87=>RenderingDevice.DataFormat.B8G8R8A8Unorm,
            91=>RenderingDevice.DataFormat.B8G8R8A8Srgb,
            10=>RenderingDevice.DataFormat.R16G16B16A16Sfloat,
            71=>RenderingDevice.DataFormat.Bc1RgbaUnormBlock,
            72=>RenderingDevice.DataFormat.Bc1RgbaSrgbBlock,
            98=>RenderingDevice.DataFormat.Bc7UnormBlock,
            99=>RenderingDevice.DataFormat.Bc7SrgbBlock,
            _=>throw new NotSupportedException("Sky device does not support the imported native view.")};
        byte[] bytes=source.GetEncodedChain();
        Rid rid=await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            var usage=RenderingDevice.TextureUsageBits.SamplingBit;
            if(allowReadback)usage|=RenderingDevice.TextureUsageBits.CanCopyFromBit;
            if(!rd.TextureIsFormatSupportedForUsage(format,usage))
                throw new NotSupportedException("Device cannot retain the requested original sky texture encoding.");
            using var shape=new RDTextureFormat{Format=format,Width=(uint)source.Width,Height=(uint)source.Height,
                Depth=(uint)source.Depth,ArrayLayers=1,Mipmaps=(uint)source.MipCount,UsageBits=usage,
                TextureType=source.SourceDimension==8?RenderingDevice.TextureType.Type3D:RenderingDevice.TextureType.Type2D};
            using var view=new RDTextureView();Rid created=rd.TextureCreate(shape,view,[bytes]);
            if(!created.IsValid)throw new InvalidDataException("Device rejected the exact original sky mip chain.");
            try
            {
                using var actual=rd.TextureGetFormat(created);
                if(actual.Width!=source.Width || actual.Height!=source.Height || actual.Depth!=source.Depth ||
                    actual.ArrayLayers!=1 || actual.Mipmaps!=source.MipCount || actual.Format!=format ||
                    actual.TextureType!=shape.TextureType)
                    throw new InvalidDataException("Device changed the original sky texture shape or encoding.");
                return created;
            }
            catch{rd.FreeRid(created);throw;}
        });
        return new(source,rid,format,allowReadback);
    }

    public Task<byte[]> ReadEncodedChainAsync()
    {
        ObjectDisposedException.ThrowIf(_disposal is not null,this);
        if(!allowReadback)throw new InvalidOperationException("The encoded image was not created for readback.");
        return TerrainDeviceTexture.OnRenderThreadAsync(rd=>rd.TextureGetData(TextureRid,0));
    }

    private readonly object disposalGate=new();
    public ValueTask DisposeAsync()
    {
        lock(disposalGate)
        {
            if(_disposal is null||_disposal.IsFaulted||_disposal.IsCanceled)
                _disposal=TerrainDeviceTexture.OnRenderThreadAsync(rd=>
                {if(rd.TextureIsValid(TextureRid))rd.FreeRid(TextureRid);return true;});
            return new(_disposal);
        }
    }
}
