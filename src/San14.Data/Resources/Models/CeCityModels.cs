using San14.Data.Formats;

namespace San14.Data;

/// <summary>Current CE field scene's source city model category references.</summary>
public static class CeCityModels
{
    public const uint FieldSceneId = 0x0449AC47;
    private const uint CatalogId = 0x77916472;
    private const uint DictionaryId = 0xE755DAEE;
    private const uint ModelClass = 0xD40B3C8F;
    // CE category IDs 11..54, in native variant/size/damage order. These are
    // catalog definition identities, not globally stable G1M FileIds.
    private static readonly uint[] DefinitionHashes =
    [
        0xE79C220A, 0x2B82FA66, 0x2B0C9467, 0x2A962E68,
        0xDC842F50, 0x206B07AC, 0x1FF4A1AD, 0x1F7E3BAE,
        0xDAAADC31, 0x1E91B48D, 0x1E1B4E8E, 0x1DA4E88F,
        0xFB43F58B, 0x3F2ACDE7, 0x3EB467E8, 0x3E3E01E9,
        0xF02C02D1, 0x3412DB2D, 0x339C752E, 0x33260F2F,
        0xEE52AFB2, 0x3239880E, 0x31C3220F, 0x314CBC10,
        0x0EEBC90C, 0x52D2A168, 0x525C3B69, 0x51E5D56A,
        0x03D3D652, 0x47BAAEAE, 0x474448AF, 0x46CDE2B0,
        0x01FA8333, 0x45E15B8F, 0x456AF590, 0x44F48F91,
        0x15A256B4, 0x59892F10, 0x5912C911, 0x589C6312,
        0x294A2A35, 0x6D310291, 0x6CBA9C92, 0x6C443693
    ];

    public static int CategoryFor(int variant, int sizeCode, int damageTier)
    {
        if (variant is < 0 or > 4 || sizeCode < 1 || damageTier is < 0 or > 3)
            throw new ArgumentOutOfRangeException(nameof(variant),
                "CE city model selector has an unsupported source value.");
        int baseCategory = variant switch
        {
            0 => sizeCode <= 1 ? 11 : sizeCode <= 3 ? 15 : 19,
            1 => sizeCode <= 1 ? 23 : sizeCode <= 3 ? 27 : 31,
            2 => sizeCode <= 1 ? 35 : sizeCode <= 3 ? 39 : 43,
            3 => 47,
            4 => 51,
            _ => throw new InvalidOperationException()
        };
        return baseCategory + damageTier;
    }

    /// <summary>Current CE City+66 damage band after the native capacity is known.</summary>
    public static int DamageTier(int durability, int capacity)
    {
        if (durability < 0 || capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity),
                "City durability and its source capacity must be valid.");
        float ratio = (float)durability / capacity;
        int percent = (int)MathF.Ceiling(ratio * 100f);
        if (percent is > 75 and <= 100) return 0;
        if (percent is > 50 and <= 75) return 1;
        if (percent is > 25 and <= 50) return 2;
        return percent is >= 0 and <= 25 ? 3 : 0;
    }

    public static SceneModelReference ResolvePingyuanNormalSmall(
        GameResourceRepository repository, LoadedSceneResources scene)
        => ResolveByCategory(repository, scene, 23);

    public static SceneModelReference ResolveByCategory(
        GameResourceRepository repository, LoadedSceneResources scene, int category)
    {
        LoadedModelTextureResources links = LoadTextureResourcesByCategory(repository, scene, category);
        return new(links.Dictionary, links.ModelObject, links.Model.Source.FileId);
    }

    /// <summary>Loads the category's original model and complete texture-reference chain.</summary>
    public static LoadedModelTextureResources LoadTextureResourcesByCategory(
        GameResourceRepository repository, LoadedSceneResources scene, int category)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(scene);
        if (scene.Scene.Source.FileId != FieldSceneId ||
            scene.ModelCatalog.Source.FileId != CatalogId)
            throw new InvalidDataException("CE city model requires the declared field scene and catalog.");
        if (category < 11 || category > 54)
            throw new InvalidDataException($"CE city model category {category} is not in the verified source set.");
        LoadedObjects[] dictionaries = scene.Dependencies.Select(item => item.Resource)
            .Where(item => item.Source.FileId == DictionaryId).ToArray();
        if (dictionaries.Length != 1)
            throw new InvalidDataException("CE field scene must declare exactly one city model dictionary.");
        LoadedObjects dictionary = dictionaries[0];
        LoadedModelTextureResources links = ModelTextureResources.LoadForDefinition(
            repository, dictionary, scene.ModelCatalog, DefinitionHashes[category - 11]);
        DokObject model = links.ModelObject;
        if (model.Kind != DokObjectKind.Inline || model.ClassId != ModelClass)
            throw new InvalidDataException($"CE city category {category} has no inline G1M model object.");
        return links;
    }
}
