using San14.Data.Formats;

namespace San14.Data;

/// <summary>Owned CPU upload bytes for one explicitly selected model image
/// and native view. Preparing this packet does not create a device texture.</summary>
public sealed class ModelTextureUpload
{
    private readonly byte[] encodedChain;
    public string SourceResourceUri { get; }
    public string SourceSha256 { get; }
    public G1TextureImage Image { get; }
    public uint NativeViewFormat { get; }
    public int ByteLength => encodedChain.Length;

    internal ModelTextureUpload(LoadedTextureContainer source, G1TextureImage image,
        uint nativeViewFormat, byte[] chain)
        => (SourceResourceUri, SourceSha256, Image, NativeViewFormat, encodedChain) =
            (source.Source.ResourceUri, source.Source.DecodedSha256, image, nativeViewFormat, chain);

    public byte[] CopyEncodedChain() => encodedChain.ToArray();
}

/// <summary>Prepare a selected planar image from an imported container.
/// Source encoding and mip ranges remain authoritative; no color conversion,
/// generated mips or implicit view selection is performed.</summary>
public static class ModelTexturePreparation
{
    public static ModelTextureUpload Prepare(LoadedTextureContainer source,
        int explicitImageIndex, uint explicitNativeViewFormat)
    {
        ArgumentNullException.ThrowIfNull(source);
        if ((uint)explicitImageIndex >= source.Container.Textures.Count)
            throw new InvalidDataException("Model image selector exceeds its imported texture container.");
        var image = source.Container.Textures[explicitImageIndex];
        if (image.LayerCount != 1 || image.SubresourceLayout != G1TextureSubresourceLayout.SingleLayerMipChain ||
            image.Depth != 1 || image.Width <= 0 || image.Height <= 0 || image.MipCount < 1 || image.Mips.Count != image.MipCount)
            throw new InvalidDataException("Model texture requires a complete single planar mip chain.");
        // This is the model device's existing supported encoding profile.
        // Other resource families retain their own explicit upload adapters.
        if (image.PixelFormat is not (0x00 or 0x01 or 0x02 or 0x03 or 0x04 or 0x59 or 0x5B or 0x5C or 0x5E or 0x5F))
            throw new InvalidDataException("Model texture encoding has no supported exact device adapter.");
        uint ordinary = ModelTextureFormatMath.Resolve(image.PixelFormat, 0).ShaderViewFormat;
        if (explicitNativeViewFormat != ordinary &&
            explicitNativeViewFormat != ModelTextureFormatMath.Resolve(image.PixelFormat, 1).ShaderViewFormat)
            throw new InvalidDataException("Requested model view is absent from its source format table.");

        var chain = new byte[image.Mips.Sum(m => m.BytesPerLayer)];
        int at = 0;
        for (int level = 0; level < image.MipCount; level++)
        {
            var bytes = image.GetEncodedMip(source.Source.Bytes, level);
            if (bytes.Length != image.Mips[level].BytesPerLayer)
                throw new InvalidDataException("Model texture mip range differs from its imported byte length.");
            bytes.CopyTo(chain.AsSpan(at)); at = checked(at + bytes.Length);
        }
        return new(source, image, explicitNativeViewFormat, chain);
    }
}
