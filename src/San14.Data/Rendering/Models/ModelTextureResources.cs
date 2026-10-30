using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public sealed record ModelTextureSlot(int Index, uint ProxyObjectId, DokObject Proxy,
    uint TextureFileId, uint RawValue00200571, byte[] RawTableRow)
{
    public string TextureUri => GameResourceId.Rdb("MaterialEditor", TextureFileId).ToString();
}

public sealed record ModelTextureEntryBinding(int RecordIndex, G1TextureEntry Entry, ModelTextureSlot Slot);
public sealed record ModelDrawTextureResources(G1DrawTextureBinding Binding,
    IReadOnlyList<ModelTextureEntryBinding> TextureEntries);

public sealed record LoadedModelTextureResources(LoadedObjects Dictionary, DokObject ModelObject,
    LoadedModelMaterials Model, DokObject TextureGroupObject, LoadedRdbAsset TextureTable,
    IReadOnlyList<ModelTextureSlot> Slots, IReadOnlyList<ModelTextureEntryBinding> TextureEntries)
{
    public LoadedObjects? Catalog { get; init; }
    public DokObject? CatalogDefinition { get; init; }
    public LoadedObjects? Scene { get; init; }
    public SceneInstance? Instance { get; init; }
}

/// <summary>
/// Follows an explicit D40B3C8F model object through its local texture group,
/// external slot table and local texture proxies. Loads one model and its small
/// table; G1T containers are read separately on demand. Optional draw resolution
/// uses the verified +24 texture-record reference. +12/+20, shader/material
/// execution and proxy value 00200571 remain unresolved.
/// </summary>
public static class ModelTextureResources
{
    private const uint ModelClass = 0xD40B3C8F, GroupClass = 0x3059B9C3,
        ProxyClass = 0xFF7DBFD4, CatalogClass = 0xC4B9B28D;

    public static LoadedModelTextureResources Load(GameResourceRepository repository,
        string archive, uint dictionaryId, uint modelObjectId) =>
        Load(repository, repository.LoadObjects(archive, dictionaryId), modelObjectId);

    public static LoadedModelTextureResources LoadForDefinition(GameResourceRepository repository,
        LoadedObjects dictionary, LoadedObjects catalog, uint definitionObjectId,
        string modelArchive = "FieldEditor4", string tableArchive = "FieldEditor4")
    {
        ArgumentNullException.ThrowIfNull(catalog);
        DokObject definition = Object(catalog, definitionObjectId, CatalogClass, "catalog definition");
        uint modelId = Scalar(definition, 0x0B6E1578, 5, catalog.Source.ResourceUri);
        LoadedModelTextureResources loaded;
        try { loaded = Load(repository, dictionary, modelId, modelArchive, tableArchive); }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"Model catalog {catalog.Source.ResourceUri} object {definitionObjectId:X8} property 0B6E1578 target local model {modelId:X8}: {error.Message}", error);
        }
        return loaded with { Catalog = catalog, CatalogDefinition = definition };
    }

    public static LoadedModelTextureResources LoadForInstance(GameResourceRepository repository,
        LoadedSceneResources scene, int instanceIndex)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(scene);
        if ((uint)instanceIndex >= scene.Instances.Count) throw new ArgumentOutOfRangeException(nameof(instanceIndex));
        return LoadForInstance(repository,scene,scene.Instances[instanceIndex]);
    }

    /// <summary>Resolve one actual resource-owned instance independently of
    /// VisibilitySet membership. Definition/material/texture resolution is the
    /// same original path used by the indexed API.</summary>
    public static LoadedModelTextureResources LoadForInstance(GameResourceRepository repository,
        LoadedSceneResources scene,SceneInstance instance)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(instance);
        DokObject definition = Object(instance.DefinitionDocument, instance.Definition.ObjectId,
            CatalogClass, $"scene {scene.Scene.Source.ResourceUri} instance {instance.Index} definition");
        uint modelId = Scalar(definition, 0x0B6E1578, 5, instance.DefinitionDocument.Source.ResourceUri);
        LoadedObjects[] dictionaries = scene.Dependencies.Select(dependency => dependency.Resource).Where(resource =>
            resource.Document.Objects.Any(item => item.ObjectId == modelId &&
                item.Kind == DokObjectKind.Inline && item.ClassId == ModelClass)).ToArray();
        if (dictionaries.Length != 1)
            throw Error(scene.Scene.Source.ResourceUri, $"instance {instance.Index} definition {instance.DefinitionDocument.Source.ResourceUri}/{definition.ObjectId:X8} property 0B6E1578 target local model {modelId:X8} requires one typed dependency dictionary; found {dictionaries.Length}");
        LoadedModelTextureResources loaded = LoadForDefinition(repository, dictionaries[0],
            instance.DefinitionDocument, definition.ObjectId);
        return loaded with { Scene = scene.Scene, Instance = instance };
    }

    public static LoadedModelTextureResources Load(GameResourceRepository repository,
        LoadedObjects dictionary, uint modelObjectId,
        string modelArchive = "FieldEditor4", string tableArchive = "FieldEditor4")
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(dictionary);
        // The model object stores FileIds, not archive names. The scene/category
        // source chooses the archive explicitly; do not search arbitrary archives.
        _ = GameResourceId.Rdb(modelArchive, 0);
        _ = GameResourceId.Rdb(tableArchive, 0);
        string context = dictionary.Source.ResourceUri;
        DokObject modelObject = Object(dictionary, modelObjectId, ModelClass, "model");
        uint modelId = Scalar(modelObject, 0x8AB68B3F, 5, context);
        LoadedModelMaterials model = Linked(context, modelObjectId, 0x8AB68B3F,
            modelArchive, modelId, () => repository.LoadModelMaterials(modelArchive, modelId));
        uint groupId = Scalar(modelObject, 0xF92C5190, 5, context);
        DokObject group = Object(dictionary, groupId, GroupClass,
            $"object {modelObjectId:X8} property F92C5190 local texture group");
        uint tableId = Scalar(group, 0x7A1E1EF8, 5, context);
        LoadedRdbAsset table = Linked(context, groupId, 0x7A1E1EF8,
            tableArchive, tableId, () => repository.LoadRdbAsset(tableArchive, tableId));
        if (table.Bytes.Length == 0 || table.Bytes.Length % 8 != 0)
            throw Error(context, $"object {groupId:X8} property 7A1E1EF8 table {table.ResourceUri} must contain complete eight-byte rows");
        var slots = new List<ModelTextureSlot>(table.Bytes.Length / 8);
        for (int offset = 0; offset < table.Bytes.Length; offset += 8)
        {
            uint slot = BinaryPrimitives.ReadUInt32LittleEndian(table.Bytes.AsSpan(offset));
            if (slot != (uint)slots.Count)
                throw Error(context, $"table {table.ResourceUri} row {slots.Count} has unsupported slot index {slot}; expected {slots.Count}");
            uint proxyId = BinaryPrimitives.ReadUInt32LittleEndian(table.Bytes.AsSpan(offset + 4));
            DokObject proxy = Object(dictionary, proxyId, ProxyClass,
                $"table {table.ResourceUri} slot {slot} local texture proxy");
            uint textureId = Scalar(proxy, 0x6C7321D2, 5, context);
            uint rawValue = Scalar(proxy, 0x00200571, 4, context);
            slots.Add(new((int)slot, proxyId, proxy, textureId, rawValue, table.Bytes.AsSpan(offset, 8).ToArray()));
        }
        var entries = new List<ModelTextureEntryBinding>();
        foreach (G1TextureRecord record in model.Materials.TextureRecords)
            foreach (G1TextureEntry entry in record.TextureEntries)
            {
                if (entry.RawValue0 >= slots.Count)
                    throw Error(context, $"model {model.Source.ResourceUri} texture record {record.Index} entry {entry.Index} slot {entry.RawValue0} exceeds table {table.ResourceUri} count {slots.Count}");
                entries.Add(new(record.Index, entry, slots[entry.RawValue0]));
            }
        return new(dictionary, modelObject, model, group, table, slots, entries);
    }

    /// <summary>Loads the selected slot's whole G1T container without assigning an image-index meaning to 00200571.</summary>
    public static LoadedTextureContainer LoadTextureContainer(GameResourceRepository repository,
        LoadedModelTextureResources model, int slotIndex)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(model);
        if ((uint)slotIndex >= model.Slots.Count) throw new ArgumentOutOfRangeException(nameof(slotIndex));
        ModelTextureSlot slot = model.Slots[slotIndex];
        return Linked(model.Dictionary.Source.ResourceUri, slot.ProxyObjectId, 0x6C7321D2,
            "MaterialEditor", slot.TextureFileId,
            () => repository.LoadTextureContainer("MaterialEditor", slot.TextureFileId));
    }

    /// <summary>Resolves the selected draw's verified +24 texture record to the model's own slots without loading any G1T.</summary>
    public static ModelDrawTextureResources ResolveDrawTextures(LoadedModelTextureResources model, int drawIndex)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (drawIndex < 0) throw new ArgumentOutOfRangeException(nameof(drawIndex));
        try
        {
            G1ModelDrawBindings draws = G1ModelDrawReader.Read(model.Model.Geometry);
            if (drawIndex >= draws.Draws.Count) throw new ArgumentOutOfRangeException(nameof(drawIndex));
            G1DrawRecord draw = draws.Draws[drawIndex];
            if (draw.RawIndex24 >= model.Model.Materials.TextureRecords.Count)
                throw new InvalidDataException($"Draw {drawIndex} texture record {draw.RawIndex24} exceeds the imported material table.");
            G1DrawTextureBinding binding = new(draw, draw.RawIndex24, model.Model.Materials.TextureRecords[(int)draw.RawIndex24]);
            var entries = new List<ModelTextureEntryBinding>(binding.TextureRecord.TextureEntries.Count);
            foreach (G1TextureEntry entry in binding.TextureRecord.TextureEntries)
            {
                if (entry.RawValue0 >= model.Slots.Count)
                    throw new InvalidDataException($"Draw {drawIndex} texture entry {entry.Index} slot {entry.RawValue0} exceeds model table {model.TextureTable.ResourceUri}.");
                entries.Add(new(binding.TextureRecord.Index, entry, model.Slots[entry.RawValue0]));
            }
            return new(binding, entries);
        }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"Model draw texture binding failed: {model.Model.Source.ResourceUri}. {error.Message}", error);
        }
    }

    private static T Linked<T>(string context, uint objectId, uint property,
        string archive, uint fileId, Func<T> read)
    {
        try { return read(); }
        catch (Exception error) when (error is KeyNotFoundException or InvalidDataException)
        {
            throw new InvalidDataException($"Model resource dictionary {context} object {objectId:X8} property {property:X8} target {GameResourceId.Rdb(archive, fileId)}: {error.Message}", error);
        }
    }

    private static DokObject Object(LoadedObjects dictionary, uint id, uint expectedClass, string role)
    {
        DokObject[] matches = dictionary.Document.Objects.Where(item => item.ObjectId == id).ToArray();
        if (matches.Length != 1 || matches[0].Kind != DokObjectKind.Inline || matches[0].ClassId != expectedClass)
            throw Error(dictionary.Source.ResourceUri, $"{role} object {id:X8} must be unique inline class {expectedClass:X8}");
        return matches[0];
    }

    private static uint Scalar(DokObject item, uint name, uint type, string context)
    {
        DokProperty[] matches = item.Properties.Where(property => property.NameHash == name).ToArray();
        if (matches.Length != 1 || matches[0].TypeCode != type ||
            matches[0].ElementCount != 1 || matches[0].RawValue.Length != 4)
            throw Error(context, $"object {item.ObjectId:X8} property {name:X8} must be a unique type-{type} scalar");
        return BinaryPrimitives.ReadUInt32LittleEndian(matches[0].RawValue);
    }

    private static InvalidDataException Error(string context, string message) =>
        new($"Model resource dictionary {context}: {message}.");
}
