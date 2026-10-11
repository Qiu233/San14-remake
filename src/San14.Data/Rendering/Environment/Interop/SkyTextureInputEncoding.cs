namespace San14.Data;

/// <summary>Encodes an already imported Sky image for the existing Native
/// planar/volume uploader. Cloud and Main Sky share these exact media bytes.</summary>
internal static class SkyTextureInputEncoding
{
    internal static string Add(IDictionary<string, byte[]> files, string directory, string program,
        uint slot, string prefix, SkyTextureSource texture)
    {
        byte colorSpace = texture.NativeViewFormat is 91 or 72 or 99 ? (byte)1 : (byte)0;
        var formats = ModelTextureFormatMath.Resolve(texture.PixelFormat, colorSpace);
        if (formats.ShaderViewFormat != texture.NativeViewFormat)
            throw new NotSupportedException("The imported Sky image view is not supported by the Native texture uploader.");
        for (int mip = 0; mip < texture.MipCount; mip++)
            files.Add(directory + $"{prefix}-mip{mip}.bin", texture.GetEncodedMip(mip));
        files.Add(directory + prefix + "-sampler.bin", texture.GetSamplerDescriptor());
        return FormattableString.Invariant($"{program} {slot} {texture.SourceDimension} {texture.Width} {texture.Height} {texture.Depth} {texture.MipCount} {formats.ResourceFormat} {formats.ShaderViewFormat} {prefix} {texture.ResourceUri} {texture.ImageIndex} {texture.SourceSha256} {texture.SamplerDescriptorSha256}");
    }
}
