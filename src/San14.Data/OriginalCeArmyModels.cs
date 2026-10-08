using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Source identity joined from the current CE category name, catalog,
/// dictionary and CharacterEditor index. This is not a FormationId mapping.</summary>
public sealed record OriginalCeArmyModelCategory(int Category, string SourceName,
    uint DefinitionId, uint ModelObjectId, uint ModelFileId);

public sealed record OriginalArmyModelDefinition(DokObject Definition, DokObject ModelObject,
    uint ModelFileId);

public sealed record LoadedOriginalArmyCatalog(LoadedObjects Catalog, LoadedObjects Dictionary,
    IReadOnlyList<OriginalArmyModelDefinition> Definitions);

public sealed record LoadedOriginalArmyModel(OriginalCeArmyModelCategory Category,
    LoadedOriginalArmyCatalog Catalog, OriginalArmyModelDefinition Definition,
    LoadedModelTextureResources Resources, LoadedModelTransforms Transforms)
{
    public SceneModelReference Reference => new(Catalog.Dictionary,
        Definition.ModelObject, Definition.ModelFileId);
}

/// <summary>
/// Loads the declared CE Army catalog and dictionary on demand. CharacterEditor
/// contains the model and eight-byte texture slot table; MaterialEditor textures
/// are read separately. Missing runtime texture proxies fail explicitly. Neither
/// Core formation IDs nor troop counts are interpreted as native model categories.
/// </summary>
public static class OriginalCeArmyModels
{
    public const uint CatalogId = 0x2082AD97;
    public const uint DictionaryId = 0xB290631C;
    private const uint DefinitionClass = 0xC4B9B28D;
    private const uint ModelClass = 0xD40B3C8F;
    private static readonly IReadOnlyList<OriginalCeArmyModelCategory> Categories = Array.AsReadOnly(
        new OriginalCeArmyModelCategory[]
        {
            new(0, "ma005_soldier_a", 0x76F89755, 0x55CB7814, 0x5B38B582),
            new(1, "ma001_soldier_a", 0x6F934AD9, 0x450A4410, 0xD3C18F06),
            new(2, "mp001_flag_a", 0x878F8DBA, 0xEAF9A10F, 0x9EEA5B67),
            new(3, "mp002_flag_a", 0x8968E0D9, 0x6F29EE10, 0x80C82506),
            new(4, "mf000_seiran_a", 0x23AE9D8D, 0x639FCADC, 0xE5DC4FBA),
            new(5, "mf001_shousha_a", 0xE325056F, 0xE5ABDEBA, 0xC0D5FA9C),
            new(6, "mf002_touseki_a", 0xC11AEB75, 0x91FE1FF4, 0x682039A2),
            new(7, "mf101_moushou_a", 0x92F8D4B1, 0x0C601F38, 0x9A851CDE),
            new(8, "mf100_souka_a", 0xE44E6191, 0xE5B57658, 0xBD1EF9BE),
            new(9, "mf102_rousen_a", 0x0D728B86, 0x2919B9C3, 0x9962AB33),
            new(10, "mh002_elephant_a", 0x692FD6F6, 0x23CD4053, 0xA9A19EA3)
        });

    public static IReadOnlyList<OriginalCeArmyModelCategory> KnownSourceCategories => Categories;

    public static LoadedOriginalArmyCatalog LoadCatalog(OriginalGameRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return ReadCatalog(repository.LoadObjects("KIDSSystemResource", CatalogId),
            repository.LoadObjects("KIDSSystemResource", DictionaryId));
    }

    public static LoadedOriginalArmyCatalog ReadCatalog(LoadedObjects catalog, LoadedObjects dictionary)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(dictionary);
        if (catalog.Source.ResourceUri != GameResourceId.Rdb("KIDSSystemResource", CatalogId).ToString() ||
            dictionary.Source.ResourceUri != GameResourceId.Rdb("KIDSSystemResource", DictionaryId).ToString())
            throw new InvalidDataException("Army model catalog requires its explicit KIDS source documents.");
        var result = new List<OriginalArmyModelDefinition>();
        var definitionIds = new HashSet<uint>();
        foreach (DokObject definition in catalog.Document.Objects.Where(item =>
                     item.Kind == DokObjectKind.Inline && item.ClassId == DefinitionClass))
        {
            if (!definitionIds.Add(definition.ObjectId))
                throw new InvalidDataException($"Army catalog has duplicate definition {definition.ObjectId:X8}.");
            uint modelId = Scalar(definition, 0x0B6E1578, catalog.Source.ResourceUri);
            DokObject[] models = dictionary.Document.Objects.Where(item => item.ObjectId == modelId).ToArray();
            if (models.Length != 1 || models[0].Kind != DokObjectKind.Inline || models[0].ClassId != ModelClass)
                throw new InvalidDataException($"Army catalog {catalog.Source.ResourceUri} definition {definition.ObjectId:X8}: " +
                    $"target {modelId:X8} must be one inline D40B3C8F object in {dictionary.Source.ResourceUri}.");
            result.Add(new(definition, models[0], Scalar(models[0], 0x8AB68B3F, dictionary.Source.ResourceUri)));
        }
        if (result.Count == 0) throw new InvalidDataException("Army catalog has no model definitions.");
        return new(catalog, dictionary, result.AsReadOnly());
    }

    public static LoadedOriginalArmyModel LoadKnownCategory(OriginalGameRepository repository,
        LoadedOriginalArmyCatalog catalog, int category)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(catalog);
        if ((uint)category >= Categories.Count)
            throw new InvalidDataException($"Army model category {category} has no source-backed identity.");
        OriginalCeArmyModelCategory known = Categories[category];
        OriginalArmyModelDefinition[] matches = catalog.Definitions.Where(item =>
            item.Definition.ObjectId == known.DefinitionId).ToArray();
        if (matches.Length != 1 || matches[0].ModelObject.ObjectId != known.ModelObjectId ||
            matches[0].ModelFileId != known.ModelFileId)
            throw new InvalidDataException($"Army category {category}/{known.SourceName}: catalog identity differs " +
                "from the supported source profile; no alternate model is selected.");
        LoadedModelTextureResources resources = OriginalModelTextureResources.LoadForDefinition(repository,
            catalog.Dictionary, catalog.Catalog, known.DefinitionId, "CharacterEditor", "CharacterEditor");
        var transforms = new LoadedModelTransforms(resources.Model.Geometry,
            G1ModelTransformReader.Read(resources.Model.Source.Bytes, resources.Model.Geometry), resources.Model.Source);
        return new(known, catalog, matches[0], resources, transforms);
    }

    private static uint Scalar(DokObject item, uint name, string context)
    {
        DokProperty[] properties = item.Properties.Where(p => p.NameHash == name).ToArray();
        if (properties.Length != 1 || properties[0].TypeCode != 5 ||
            properties[0].ElementCount != 1 || properties[0].ElementSize != 4 || properties[0].RawValue.Length != 4)
            throw new InvalidDataException($"Army source {context} object {item.ObjectId:X8} " +
                $"property {name:X8} must be one type-5 scalar.");
        return BinaryPrimitives.ReadUInt32LittleEndian(properties[0].RawValue);
    }
}
