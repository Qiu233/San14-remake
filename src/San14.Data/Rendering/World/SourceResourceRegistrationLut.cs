using San14.Data.Formats;

namespace San14.Data;

/// <summary>The default light's registered lookup-table volume. Its declaration
/// selects the actual image; shared readers supply its encoding, depth and mips.</summary>
public static class SourceResourceRegistrationLut
{
    public const string DefinitionArchive = "KIDSSystemResource";
    public const string TextureArchive = "RRPreview";
    public const uint DefinitionResourceId = 0x7DB38795;
    public const uint ObjectKey = 0x7D85DA95;

    public static StaticTextureBinding LoadGameDefault(GameResourceRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var source = StaticTextureResources.Load(repository,
            repository.LoadObjects(DefinitionArchive, DefinitionResourceId), TextureArchive, [ObjectKey]).Single();
        if (source.Image.TextureSystem != 2 || source.Image.LayerCount != 1 ||
            source.Image.SubresourceLayout != G1TextureSubresourceLayout.VolumeMipChain)
            throw new InvalidDataException("Default lighting lookup table requires a volume image.");
        return source;
    }
}
