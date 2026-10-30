using San14.Data.Formats;

namespace San14.Data;

/// <summary>
/// Resolves the normal facility and connection categories through the current
/// CE field scene's declared catalog and dictionary.
/// </summary>
public static class CeFacilityModels
{
    public const uint FieldSceneId = 0x0449AC47;
    private const uint CatalogId = 0x77916472;
    private const uint DictionaryId = 0xE755DAEE;
    private const uint ModelClass = 0xD40B3C8F;
    // Declared normal category names61..88, signed UTF8 name hashes. These
    // identify WorldPQ definitions, including the separate connection models.
    private static readonly uint[] DefinitionHashes =
    [
        0xE37A48A2u,0x1EB96DE6u,0xA3ED34D0u,0x06C0BE75u,
        0x10D4CFD3u,0x20B850BBu,0x23328C51u,0x95541F56u,
        0x7AEF28B7u,0x52F53ADDu,0xCD54B7F1u,0x0622F1EBu,
        0xDF08940Bu,0xAE01A187u,0x602B603Cu,0x6A000341u,
        0xA53F2885u,0x2A72EF6Fu,0x8D467914u,0x975A8A72u,
        0xA73E0B5Au,0xA9B846F0u,0x1BD9D9F5u,0x0174E356u,
        0xD97AF57Cu,0x099357B5u,0x43F01FF8u,0x1BF6321Eu
    ];

    public static int CategoryFor(int objectDefinitionSlot,bool completed) => objectDefinitionSlot switch
    {
        >=3 and <=6 => (completed?61:76)+objectDefinitionSlot-3,
        >=7 and <=10 => (completed?66:81)+objectDefinitionSlot-7,
        >=11 and <=14 => 71+objectDefinitionSlot-11,
        15 => completed?65:80,
        16 => 70,
        17 => 75,
        _ => throw new InvalidDataException($"CE facility object slot {objectDefinitionSlot} has no source model binding.")
    };

    // The normal category table's projected-shadow field is0 for61..88.
    // The shared model bind therefore calls its explicit caster-disable writer.
    public static bool UsesProjectedShadow(int category)
    {
        if(category is <61 or >88)throw new ArgumentOutOfRangeException(nameof(category));
        return false;
    }

    public static (SceneModelReference Unfinished, SceneModelReference Completed) Resolve(
        GameResourceRepository repository, LoadedSceneResources scene,
        int objectDefinitionSlot = 3)
        => (Reference(LoadTextureResources(repository, scene, objectDefinitionSlot, false)),
            Reference(LoadTextureResources(repository, scene, objectDefinitionSlot, true)));

    /// <summary>Imports only the selected facility state, retaining the definition,
    /// model, material records and slot table for its actual asset owner.</summary>
    public static LoadedModelTextureResources LoadTextureResources(GameResourceRepository repository,
        LoadedSceneResources scene, int objectDefinitionSlot, bool completed)
        =>LoadTextureResourcesByCategory(repository,scene,CategoryFor(objectDefinitionSlot,completed));

    public static LoadedModelTextureResources LoadTextureResourcesByCategory(GameResourceRepository repository,
        LoadedSceneResources scene,int category)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(scene);
        if (scene.Scene.Source.FileId != FieldSceneId ||
            scene.ModelCatalog.Source.FileId != CatalogId)
            throw new InvalidDataException("CE facility models require the declared CE field scene and model catalog.");
        LoadedObjects[] dictionaries = scene.Dependencies.Select(item => item.Resource)
            .Where(item => item.Source.FileId == DictionaryId).ToArray();
        if (dictionaries.Length != 1)
            throw new InvalidDataException("CE field scene must declare exactly one facility model dictionary.");
        LoadedObjects dictionary = dictionaries[0];
        if(category is <61 or >88)throw new InvalidDataException($"CE facility category {category} has no source model binding.");
        uint definitionId = DefinitionHashes[category-61];
        LoadedModelTextureResources links = ModelTextureResources.LoadForDefinition(
            repository, dictionary, scene.ModelCatalog, definitionId);
        if (links.ModelObject.Kind != DokObjectKind.Inline || links.ModelObject.ClassId != ModelClass)
            throw new InvalidDataException($"CE facility definition {definitionId:X8} has no inline G1M model object.");
        return links;
    }

    private static SceneModelReference Reference(LoadedModelTextureResources resources)
        => new(resources.Dictionary, resources.ModelObject, resources.Model.Source.FileId);
}
