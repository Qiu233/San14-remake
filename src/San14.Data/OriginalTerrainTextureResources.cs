using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public enum TerrainTextureGroupKind { Field, Root, Auxiliary }

public sealed record TerrainTextureBinding(uint Slot, DokObject ProxyObject,
    LoadedTextureContainer Resource, int TextureIndex)
{
    public G1TextureImage Image => Resource.Container.Textures[TextureIndex];
}
public sealed record LoadedTerrainTextureGroup(DokObject Definition, LoadedRdbAsset TableSource,
    IReadOnlyList<TerrainTextureBinding> Bindings);
public sealed record LoadedTerrainTextureResources(LoadedTerrainResources Terrain,
    LoadedTerrainTextureGroup FieldTextures, LoadedTerrainTextureGroup RootTextures,
    LoadedTerrainTextureGroup AuxiliaryTextures, FloatTexture HeightTexture);
public sealed record LoadedTerrainFieldIndexMap(TerrainTextureBinding Binding, G1FieldIndexMap Map);

/// <summary>
/// Resolves the three verified texture groups through local DOK objects and explicit external slot tables.
/// Container/image references and the field group's scalar height at slot zero are established;
/// other shader channel uses, world coordinates and scene/edition selection are not inferred.
/// </summary>
public static class OriginalTerrainTextureResources
{
    private const uint TextureGroupClass = 0x3059B9C3, TextureProxyClass = 0xFF7DBFD4;
    public const double HeightMatchTolerance = 0.0005;

    /// <summary>
    /// Reads the source bytes of Field slot 1 without assuming its mapping to
    /// Root material layers or the terrain shader's BlendMap register.
    /// </summary>
    public static LoadedTerrainFieldIndexMap ReadFieldIndexMap(LoadedTerrainTextureGroup field)
    {
        ArgumentNullException.ThrowIfNull(field);
        TerrainTextureBinding[] matches = field.Bindings.Where(binding => binding.Slot == 1).ToArray();
        if (matches.Length != 1)
            throw new InvalidDataException($"Field texture table {field.TableSource.ResourceUri} requires one slot 1.");
        TerrainTextureBinding binding = matches[0];
        G1FieldIndexMap map = G1FieldIndexMapReader.Read(binding.Image, binding.Resource.Source.Bytes);
        return new(binding, map);
    }

    /// <summary>Loads only the explicitly selected group and its referenced containers.</summary>
    public static LoadedTerrainTextureGroup LoadGroup(OriginalGameRepository repository,
        LoadedTerrainResources terrain, TerrainTextureGroupKind group)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(terrain);
        var cache = new Dictionary<uint, LoadedTextureContainer>();
        return group switch
        {
            TerrainTextureGroupKind.Field => ReadGroup(repository, terrain, terrain.FieldObject, 0xDC491051, cache),
            TerrainTextureGroupKind.Root => ReadGroup(repository, terrain, terrain.RootObject, 0x0C47E481, cache),
            TerrainTextureGroupKind.Auxiliary => ReadGroup(repository, terrain, terrain.FieldObject, 0x848CF8FC, cache),
            _ => throw new ArgumentOutOfRangeException(nameof(group), group, "Unknown terrain texture group.")
        };
    }

    public static LoadedTerrainTextureResources Load(OriginalGameRepository repository, LoadedTerrainResources terrain)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(terrain);
        string context = terrain.Objects.Source.ResourceUri;
        var cache = new Dictionary<uint, LoadedTextureContainer>();
        LoadedTerrainTextureGroup field = ReadGroup(repository, terrain, terrain.FieldObject, 0xDC491051, cache);
        LoadedTerrainTextureGroup root = ReadGroup(repository, terrain, terrain.RootObject, 0x0C47E481, cache);
        LoadedTerrainTextureGroup auxiliary = ReadGroup(repository, terrain, terrain.FieldObject, 0x848CF8FC, cache);
        TerrainTextureBinding? height = field.Bindings.SingleOrDefault(binding => binding.Slot == 0);
        if (height is null) throw new InvalidDataException($"Terrain bundle {context} has no field texture slot zero.");
        FloatTexture values;
        try { values = G1FloatTextureReader.Read(height.Image, height.Resource.Source.Bytes); }
        catch (InvalidDataException exception)
        { throw new InvalidDataException($"Terrain float height {height.Resource.Source.ResourceUri}: {exception.Message}", exception); }
        MhkHeightField mhk = terrain.HeightField.HeightField;
        if (values.Width != mhk.SampleWidth || values.Height != mhk.SampleHeight)
            throw new InvalidDataException($"Terrain height texture {height.Resource.Source.ResourceUri} and grid {terrain.HeightField.Source.ResourceUri} have different dimensions.");
        for (int row = 0; row < values.Height; row++)
            for (int column = 0; column < values.Width; column++)
                if (Math.Abs(values.GetSample(column, row) - mhk.GetHeight(column, row)) > HeightMatchTolerance)
                    throw new InvalidDataException($"Terrain height texture {height.Resource.Source.ResourceUri} and grid {terrain.HeightField.Source.ResourceUri} disagree at sample ({column}, {row}).");
        return new(terrain, field, root, auxiliary, values);
    }

    private static LoadedTerrainTextureGroup ReadGroup(OriginalGameRepository repository, LoadedTerrainResources terrain,
        DokObject owner, uint property, Dictionary<uint, LoadedTextureContainer> cache)
    {
        string context = terrain.Objects.Source.ResourceUri;
        uint objectId = Scalar(owner, property, 5, context);
        DokObject definition = Local(terrain.Objects.Document, objectId, TextureGroupClass, context);
        uint tableId = Scalar(definition, 0x7A1E1EF8, 5, context);
        LoadedRdbAsset table;
        try { table = repository.LoadRdbAsset("FieldEditor4", tableId); }
        catch (KeyNotFoundException exception)
        { throw new InvalidDataException($"Terrain bundle {context} group {objectId:X8} references missing slot table {GameResourceId.Rdb("FieldEditor4", tableId)}.", exception); }
        if (table.Bytes.Length == 0 || table.Bytes.Length % 8 != 0)
            throw new InvalidDataException($"Terrain texture slot table {table.ResourceUri} does not contain complete eight-byte entries.");
        var slots = new HashSet<uint>();
        var bindings = new List<TerrainTextureBinding>(table.Bytes.Length / 8);
        for (int offset = 0; offset < table.Bytes.Length; offset += 8)
        {
            uint slot = BinaryPrimitives.ReadUInt32LittleEndian(table.Bytes.AsSpan(offset, 4));
            uint proxyId = BinaryPrimitives.ReadUInt32LittleEndian(table.Bytes.AsSpan(offset + 4, 4));
            if (!slots.Add(slot)) throw new InvalidDataException($"Terrain texture table {table.ResourceUri} repeats slot {slot}.");
            DokObject proxy = Local(terrain.Objects.Document, proxyId, TextureProxyClass, table.ResourceUri);
            uint fileId = Scalar(proxy, 0x6C7321D2, 5, context);
            uint rawImageIndex = Scalar(proxy, 0x00200571, 4, context);
            if (!cache.TryGetValue(fileId, out LoadedTextureContainer? resource))
            {
                try { cache[fileId] = resource = repository.LoadTextureContainer("FieldEditor4", fileId); }
                catch (KeyNotFoundException exception)
                { throw new InvalidDataException($"Terrain table {table.ResourceUri} slot {slot} proxy {proxyId:X8} references missing texture {GameResourceId.Rdb("FieldEditor4", fileId)}.", exception); }
            }
            if (rawImageIndex >= resource.Container.Textures.Count)
                throw new InvalidDataException($"Terrain table {table.ResourceUri} slot {slot} proxy {proxyId:X8} references missing image {rawImageIndex} in {resource.Source.ResourceUri}.");
            bindings.Add(new(slot, proxy, resource, (int)rawImageIndex));
        }
        return new(definition, table, bindings);
    }

    private static uint Scalar(DokObject item, uint name, uint type, string context)
    {
        DokProperty[] properties = item.Properties.Where(property => property.NameHash == name).ToArray();
        if (properties.Length != 1 || properties[0].TypeCode != type || properties[0].ElementCount != 1 || properties[0].RawValue.Length != 4)
            throw new InvalidDataException($"Terrain resource {context} object {item.ObjectId:X8} requires a unique type-{type} scalar property {name:X8}.");
        return BinaryPrimitives.ReadUInt32LittleEndian(properties[0].RawValue);
    }

    private static DokObject Local(DokDocument document, uint id, uint expectedClass, string context)
    {
        DokObject[] matches = document.Objects.Where(item => item.ObjectId == id).ToArray();
        if (matches.Length != 1 || matches[0].Kind != DokObjectKind.Inline || matches[0].ClassId != expectedClass)
            throw new InvalidDataException($"Terrain resource {context} object {id:X8} is missing, ambiguous or has an unsupported class.");
        return matches[0];
    }
}
