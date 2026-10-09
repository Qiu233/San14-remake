using San14.Data.Formats;

namespace San14.Data;

/// <summary>
/// Resolves supported two-state facility models through the current CE field scene's
/// declared catalog and dictionary. It does not assign these models to other scenes.
/// </summary>
public static class CeFacilityModels
{
    public const uint FieldSceneId = 0x0449AC47;
    private const uint CatalogId = 0x77916472;
    private const uint DictionaryId = 0xE755DAEE;
    private const uint ModelClass = 0xD40B3C8F;
    private const uint BuildingDefinitionId = 0xE37A48A2; // category 61, object bit 0 set
    private const uint UnfinishedDefinitionId = 0x6A000341; // category 76, object bit 0 clear
    private const uint FortBuildingDefinitionId = 0x1EB96DE6; // category 62, bit 0 set
    private const uint FortUnfinishedDefinitionId = 0xA53F2885; // category 77, bit 0 clear
    private const uint ArcherTowerBuildingDefinitionId = 0xA3ED34D0; // category 63, bit 0 set
    private const uint ArcherTowerUnfinishedDefinitionId = 0x2A72EF6F; // category 78, bit 0 clear

    public static (SceneModelReference Unfinished, SceneModelReference Completed) Resolve(
        GameResourceRepository repository, LoadedSceneResources scene,
        int objectDefinitionSlot = 3)
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
        (uint unfinishedId, uint completedId) = objectDefinitionSlot switch
        {
            3 => (UnfinishedDefinitionId, BuildingDefinitionId),
            4 => (FortUnfinishedDefinitionId, FortBuildingDefinitionId),
            5 => (ArcherTowerUnfinishedDefinitionId, ArcherTowerBuildingDefinitionId),
            _ => throw new InvalidDataException($"CE facility object slot {objectDefinitionSlot} has no source model binding.")
        };
        return (Read(unfinishedId), Read(completedId));

        SceneModelReference Read(uint definitionId)
        {
            LoadedModelTextureResources links = ModelTextureResources.LoadForDefinition(
                repository, dictionary, scene.ModelCatalog, definitionId);
            DokObject model = links.ModelObject;
            if (model.Kind != DokObjectKind.Inline || model.ClassId != ModelClass)
                throw new InvalidDataException($"CE facility definition {definitionId:X8} has no inline G1M model object.");
            return new(dictionary, model, links.Model.Source.FileId);
        }
    }
}
