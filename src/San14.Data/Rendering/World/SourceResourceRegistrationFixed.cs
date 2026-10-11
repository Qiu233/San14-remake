namespace San14.Data;

/// <summary>The default light's three registered planar/array declarations.
/// Actual image references, dimensions and layers come from those declarations;
/// source-format checks belong to the shared readers and device consumers.</summary>
public static class SourceResourceRegistrationFixed
{
    public const string Archive = "system";
    public const uint StaticClassId = 0xFF7DBFD4;
    public const uint TextureInterfaceClassId = StaticTextureResources.TextureInterfaceClassId;
    public const uint WhiteObjectKey = 0x80F9DB7B, WhiteDefinitionResourceId = 0x93FB3280;

    public static StaticTextureBinding LoadWhite(GameResourceRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return StaticTextureResources.Load(repository,
            repository.LoadObjects(Archive, WhiteDefinitionResourceId), Archive, [WhiteObjectKey]).Single();
    }

    public static IReadOnlyList<StaticTextureBinding> LoadGameDefault(GameResourceRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var white = LoadWhite(repository);
        var arrays = StaticTextureResources.Load(repository,
            repository.LoadObjects(Archive, 0x718CCB8B), Archive, [0x7F97260C, 0x54F8C0DE]);
        return Array.AsReadOnly(arrays.Prepend(white).ToArray());
    }
}
