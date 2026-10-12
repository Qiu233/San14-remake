using San14.Data.Formats;

namespace San14.Data;

/// <summary>The default light's static ambient cube declaration. Shared readers
/// resolve its actual image, six faces, mip chains and source sampler.</summary>
public static class SourceResourceRegistrationAmbientCube
{
    public const uint ObjectKey = 0x5A53AD58;
    public const uint DefinitionResourceId = 0x7DB38795;
    public const string DefinitionArchive = "KIDSSystemResource";
    public const string TextureArchive = "RRPreview";

    public static StaticTextureBinding LoadGameDefault(GameResourceRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var source = StaticTextureResources.Load(repository,
            repository.LoadObjects(DefinitionArchive, DefinitionResourceId), TextureArchive, [ObjectKey]).Single();
        var image = source.Image;
        if (image.TextureSystem != 1 || image.LayerCount != 6 || image.Depth != 1 ||
            image.Width != image.Height || image.SubresourceLayout != G1TextureSubresourceLayout.MipMajorArray)
            throw new InvalidDataException("Default ambient resource requires a six-face cube mip chain.");
        return source;
    }

    public static byte[] GetSamplerDescriptor(StaticTextureBinding binding) =>
        G1TextureSamplerReader.ReadRetainedSource(binding.Image).EncodeD3D11Descriptor();

    public static byte[] GetSamplerKey(StaticTextureBinding binding) =>
        G1TextureSamplerReader.ReadRetainedSource(binding.Image).RawKey;
}
