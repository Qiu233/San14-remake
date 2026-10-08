using System.Security.Cryptography;
using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>One caller-selected original model image and its complete encoded
/// mip chain. Texture kind, proxy selector, view color space and sampler are
/// explicit inputs; this owner does not infer a material binding.</summary>
public sealed class OriginalModelTextureDevice : IAsyncDisposable
{
    public string SourceResourceUri { get; }
    public string SourceSha256 { get; }
    public G1TextureImage Image { get; }
    public uint NativeViewFormat { get; }
    public IReadOnlyList<string> MipSha256 { get; }
    public Rid TextureRid { get; }
    private Task? _disposal;

    private OriginalModelTextureDevice(LoadedTextureContainer source, G1TextureImage image,
        uint view, string[] hashes, Rid rid) =>
        (SourceResourceUri, SourceSha256, Image, NativeViewFormat, MipSha256, TextureRid) =
            (source.Source.ResourceUri, source.Source.DecodedSha256, image, view, Array.AsReadOnly(hashes), rid);

    public static async Task<OriginalModelTextureDevice> BuildAsync(LoadedTextureContainer source,
        int explicitImageIndex, uint explicitNativeViewFormat)
    {
        ArgumentNullException.ThrowIfNull(source);
        source = source with { Source = source.Source with { Bytes = source.Source.Bytes.ToArray() } };
        if (!Convert.ToHexString(SHA256.HashData(source.Source.Bytes)).Equals(source.Source.DecodedSha256,
            StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Original model texture bytes differ from their source hash.");
        var canonical = G1TextureContainerReader.Read(source.Source.Bytes);
        if ((uint)explicitImageIndex >= canonical.Textures.Count)
            throw new InvalidDataException("Original model image selector exceeds the exact source container.");
        var image = canonical.Textures[explicitImageIndex];
        if (image.LayerCount != 1 || image.SubresourceLayout != G1TextureSubresourceLayout.SingleLayerMipChain)
            throw new InvalidDataException("Original model texture device currently requires a verified single 2D mip chain.");
        // Exact SC/TC resource/SRV rows and the original 2D color-space loader
        // are retained in ce-model-texture-native-format-20261002. The caller
        // chooses a real supported source view; no colorspace is inferred.
        var ordinary = OriginalModelTextureFormatMath.Resolve(image.PixelFormat, 0);
        uint? alternate;
        try { alternate = OriginalModelTextureFormatMath.Resolve(image.PixelFormat, 1).ShaderViewFormat; }
        catch (InvalidDataException) { alternate = null; }
        if (explicitNativeViewFormat != ordinary.ShaderViewFormat && explicitNativeViewFormat != alternate)
            throw new InvalidDataException("Requested model SRV format is absent from the original format table.");
        var format = (image.PixelFormat, explicitNativeViewFormat) switch
        {
            (0x00, 28) => RenderingDevice.DataFormat.R8G8B8A8Unorm,
            (0x00, 29) => RenderingDevice.DataFormat.R8G8B8A8Srgb,
            (0x01, 87) => RenderingDevice.DataFormat.B8G8R8A8Unorm,
            (0x01, 91) => RenderingDevice.DataFormat.B8G8R8A8Srgb,
            (0x02, 41) => RenderingDevice.DataFormat.R32Sfloat,
            (0x03, 10) => RenderingDevice.DataFormat.R16G16B16A16Sfloat,
            (0x04, 2) => RenderingDevice.DataFormat.R32G32B32A32Sfloat,
            (0x59, 71) => RenderingDevice.DataFormat.Bc1RgbaUnormBlock,
            (0x59, 72) => RenderingDevice.DataFormat.Bc1RgbaSrgbBlock,
            (0x5B, 77) => RenderingDevice.DataFormat.Bc3UnormBlock,
            (0x5B, 78) => RenderingDevice.DataFormat.Bc3SrgbBlock,
            (0x5C, 80) => RenderingDevice.DataFormat.Bc4UnormBlock,
            (0x5E, 95) => RenderingDevice.DataFormat.Bc6HUfloatBlock,
            (0x5F, 98) => RenderingDevice.DataFormat.Bc7UnormBlock,
            (0x5F, 99) => RenderingDevice.DataFormat.Bc7SrgbBlock,
            _ => throw new InvalidDataException("Original model encoding and explicit native SRV format have no supported exact adapter.")
        };
        byte[] chain = new byte[image.Mips.Sum(m => m.BytesPerLayer)];
        string[] hashes = new string[image.MipCount];
        int at = 0;
        for (int mip = 0; mip < image.MipCount; mip++)
        {
            ReadOnlySpan<byte> bytes = image.GetEncodedMip(source.Source.Bytes, mip);
            bytes.CopyTo(chain.AsSpan(at)); at += bytes.Length;
            hashes[mip] = Convert.ToHexString(SHA256.HashData(bytes));
        }
        if (at != chain.Length) throw new InvalidDataException("Original model mip chain length changed.");
        Rid rid = await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd =>
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
        return new(source, image, explicitNativeViewFormat, hashes, rid);
    }

    public Task<byte[]> ReadEncodedChainAsync()
    {
        ObjectDisposedException.ThrowIf(_disposal is not null, this);
        return OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd =>
        { ObjectDisposedException.ThrowIf(_disposal is not null, this); return rd.TextureGetData(TextureRid, 0); });
    }

    public ValueTask DisposeAsync() => new(_disposal ??= OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd =>
    { rd.FreeRid(TextureRid); return true; }));
}
