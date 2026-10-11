namespace San14.Data;

/// <summary>The registered seasonal diffuse texture. Its Static declaration
/// selects the actual image; dimensions, encoding and sampler come from that image.</summary>
public static class SourceResourceRegistrationDirectionalDiffuse
{
    public const uint ObjectKey = 0x73ECE15E, DefinitionResourceId = 0xD2ECCCDF;
    public const string DefinitionArchive = "KIDSSystemResource", TextureArchive = "RRPreview";
    public const uint TextureInterfaceClassId = StaticTextureResources.TextureInterfaceClassId;

    public static StaticTextureBinding Load(GameResourceRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return StaticTextureResources.Load(repository,
            repository.LoadObjects(DefinitionArchive, DefinitionResourceId), TextureArchive, [ObjectKey]).Single();
    }
}
