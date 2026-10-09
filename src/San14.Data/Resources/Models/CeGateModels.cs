using San14.Data.Formats;

namespace San14.Data;

/// <summary>Current CE field scene's four dynamic gate model categories.</summary>
public static class CeGateModels
{
    private const uint FieldSceneId = 0x0449AC47;
    private const uint CatalogId = 0x77916472;
    private const uint DictionaryId = 0xE755DAEE;
    private const uint ModelClass = 0xD40B3C8F;
    private static readonly uint[] DefinitionHashes =
        [0x5A27211C, 0xBDAA1578, 0x4A4A4079, 0xD6EA6B7A];

    public static SceneModelReference ResolveByCategory(
        GameResourceRepository repository, LoadedSceneResources scene, int category)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(scene);
        if (scene.Scene.Source.FileId != FieldSceneId ||
            scene.ModelCatalog.Source.FileId != CatalogId)
            throw new InvalidDataException("CE gate model requires the declared field scene and catalog.");
        if (category is < 55 or > 58)
            throw new InvalidDataException($"CE gate model category {category} is not in the verified source set.");
        LoadedObjects[] dictionaries = scene.Dependencies.Select(item => item.Resource)
            .Where(item => item.Source.FileId == DictionaryId).ToArray();
        if (dictionaries.Length != 1)
            throw new InvalidDataException("CE field scene must declare exactly one gate model dictionary.");
        LoadedObjects dictionary = dictionaries[0];
        LoadedModelTextureResources links = ModelTextureResources.LoadForDefinition(
            repository, dictionary, scene.ModelCatalog, DefinitionHashes[category - 55]);
        DokObject model = links.ModelObject;
        if (model.Kind != DokObjectKind.Inline || model.ClassId != ModelClass)
            throw new InvalidDataException($"CE gate category {category} has no inline G1M model object.");
        return new(dictionary, model, links.Model.Source.FileId);
    }
}
