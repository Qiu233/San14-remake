using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>One caller-selected original model image and its complete encoded
/// mip chain. Texture kind, proxy selector, view color space and sampler are
/// explicit inputs; this owner does not infer a material binding.</summary>
public sealed class ModelTextureDevice : IAsyncDisposable
{
    public string SourceResourceUri { get; }
    public string SourceSha256 { get; }
    public G1TextureImage Image { get; }
    public uint NativeViewFormat { get; }
    public Rid TextureRid { get; }
    private Task? _disposal;

    private ModelTextureDevice(ModelTextureUpload source, Rid rid) =>
        (SourceResourceUri, SourceSha256, Image, NativeViewFormat, TextureRid) =
            (source.SourceResourceUri, source.SourceSha256, source.Image, source.NativeViewFormat, rid);

    public static Task<ModelTextureDevice> BuildAsync(LoadedTextureContainer source,
        int explicitImageIndex, uint explicitNativeViewFormat)
        => BuildAsync(ModelTexturePreparation.Prepare(source, explicitImageIndex, explicitNativeViewFormat));

    public static async Task<ModelTextureDevice> BuildAsync(ModelTextureUpload source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var image = source.Image;
        var format = DeviceFormat(source.NativeViewFormat);
        byte[] chain = source.CopyEncodedChain();
        Rid rid = await TerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            var usage = RenderingDevice.TextureUsageBits.SamplingBit | RenderingDevice.TextureUsageBits.CanCopyFromBit;
            if (!rd.TextureIsFormatSupportedForUsage(format, usage))
                throw new NotSupportedException("Device cannot retain the original model texture encoding.");
            using var shape = new RDTextureFormat
            {
                Format = format, Width = (uint)image.Width, Height = (uint)image.Height, Depth = 1,
                ArrayLayers = 1, Mipmaps = (uint)image.MipCount, UsageBits = usage,
                TextureType = RenderingDevice.TextureType.Type2D
            };
            using var view = new RDTextureView();
            Rid value = rd.TextureCreate(shape, view, [chain]);
            if (!value.IsValid) throw new InvalidDataException("Device rejected the original model mip chain.");
            try
            {
                using var actual = rd.TextureGetFormat(value);
                if (actual.Width != image.Width || actual.Height != image.Height || actual.Depth != 1 ||
                    actual.ArrayLayers != 1 || actual.Mipmaps != image.MipCount || actual.Format != format ||
                    actual.TextureType != RenderingDevice.TextureType.Type2D)
                    throw new InvalidDataException("Device changed the original model image shape or encoding.");
                return value;
            }
            catch { rd.FreeRid(value); throw; }
        });
        return new(source, rid);
    }

    private static RenderingDevice.DataFormat DeviceFormat(uint nativeViewFormat) => nativeViewFormat switch
    {
        28 => RenderingDevice.DataFormat.R8G8B8A8Unorm,
        29 => RenderingDevice.DataFormat.R8G8B8A8Srgb,
        87 => RenderingDevice.DataFormat.B8G8R8A8Unorm,
        91 => RenderingDevice.DataFormat.B8G8R8A8Srgb,
        41 => RenderingDevice.DataFormat.R32Sfloat,
        10 => RenderingDevice.DataFormat.R16G16B16A16Sfloat,
        2 => RenderingDevice.DataFormat.R32G32B32A32Sfloat,
        71 => RenderingDevice.DataFormat.Bc1RgbaUnormBlock,
        72 => RenderingDevice.DataFormat.Bc1RgbaSrgbBlock,
        77 => RenderingDevice.DataFormat.Bc3UnormBlock,
        78 => RenderingDevice.DataFormat.Bc3SrgbBlock,
        80 => RenderingDevice.DataFormat.Bc4UnormBlock,
        95 => RenderingDevice.DataFormat.Bc6HUfloatBlock,
        98 => RenderingDevice.DataFormat.Bc7UnormBlock,
        99 => RenderingDevice.DataFormat.Bc7SrgbBlock,
        _ => throw new InvalidDataException("Imported model view has no supported device format adapter.")
    };

    public Task<byte[]> ReadEncodedChainAsync()
    {
        ObjectDisposedException.ThrowIf(_disposal is not null, this);
        return TerrainDeviceTexture.OnRenderThreadAsync(rd =>
        { ObjectDisposedException.ThrowIf(_disposal is not null, this); return rd.TextureGetData(TextureRid, 0); });
    }

    public ValueTask DisposeAsync() => new(_disposal ??= TerrainDeviceTexture.OnRenderThreadAsync(rd =>
    { rd.FreeRid(TextureRid); return true; }));
}
