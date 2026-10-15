using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public sealed class MapSeasonLightSelectorDeclaration
{
    internal MapSeasonLightSelectorDeclaration(uint objectId, uint gridWidth, uint gridHeight,
        uint cellSizeX, uint cellSizeZ, float originX, float originZ, uint mapObjectKey,
        IReadOnlyList<float> xPoints, IReadOnlyList<float> zPoints)
    {
        ObjectId = objectId; GridWidth = gridWidth; GridHeight = gridHeight;
        CellSizeX = cellSizeX; CellSizeZ = cellSizeZ; OriginX = originX; OriginZ = originZ;
        MapObjectKey = mapObjectKey; XPoints = xPoints; ZPoints = zPoints;
    }
    public uint ObjectId { get; }
    public uint GridWidth { get; }
    public uint GridHeight { get; }
    public uint CellSizeX { get; }
    public uint CellSizeZ { get; }
    public float OriginX { get; }
    public float OriginZ { get; }
    public uint MapObjectKey { get; }
    public IReadOnlyList<float> XPoints { get; }
    public IReadOnlyList<float> ZPoints { get; }
}

/// <summary>One declared root and its provider's selector followed by four
/// ordered children. This is source topology, without registration or selection.</summary>
public sealed class MapSeasonDirectionalLightProvider
{
    internal MapSeasonDirectionalLightProvider(DirectionalLightDeclaration root,
        uint providerListObjectId, MapSeasonLightSelectorDeclaration selector,
        IReadOnlyList<DirectionalLightDeclaration> children)
    { Root = root; ProviderListObjectId = providerListObjectId; Selector = selector; Children = children; }
    public DirectionalLightDeclaration Root { get; }
    public uint ProviderListObjectId { get; }
    public MapSeasonLightSelectorDeclaration Selector { get; }
    public IReadOnlyList<DirectionalLightDeclaration> Children { get; }
}

/// <summary>One imported seasonal light graph. Consumers borrow its typed declarations.</summary>
public sealed class MapSeasonDirectionalLightSourceSet
{
    private readonly IReadOnlyDictionary<uint, DirectionalLightSettings> settings;

    internal MapSeasonDirectionalLightSourceSet(MapSeasonActionDatabase database,
        TerrainSeasonResourceIdentity source, uint outerListObjectId,
        IReadOnlyList<MapSeasonDirectionalLightProvider> roots,
        IReadOnlyDictionary<uint, DirectionalLightSettings> settings)
    {
        Database = database; Source = source; OuterListObjectId = outerListObjectId;
        Roots = roots; this.settings = settings;
    }

    public MapSeasonActionDatabase Database { get; }
    public TerrainSeasonResourceIdentity Source { get; }
    public uint OuterListObjectId { get; }
    public IReadOnlyList<MapSeasonDirectionalLightProvider> Roots { get; }
    public DirectionalLightSettings GetSettings(uint objectId) =>
        settings.TryGetValue(objectId, out var value) ? value :
        throw new InvalidDataException($"Directional object {objectId:X8} is outside this seasonal graph.");
}

/// <summary>Read the actual selected seasonal database's ListContainer graph.
/// SourceGroupMask is an unshifted uint. Declarations and selectors are imported
/// once; construction and current selection consume that captured generation.</summary>
public static class MapSeasonDirectionalLightSources
{
    public const uint OuterListObjectId = 0x30C0B07F;
    public const uint ListContainerClassId = 0x2EB79141;
    public const uint SelectorClassId = 0x13696D79;

    public static MapSeasonDirectionalLightSourceSet Load(GameResourceRepository repository, int sourceMonth)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var action = MapSeasonResourceConsumer.LoadActionSource(repository, sourceMonth);
        return Read(MapSeasonResourceConsumer.LoadActionDatabase(repository, action));
    }

    public static MapSeasonDirectionalLightSourceSet Read(MapSeasonActionDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        LoadedRdbAsset asset = database.Objects.Source;
        if (asset.FileId != database.Action.DatabaseFileId || asset.ResourceUri != database.Action.ResourceUri)
            throw new InvalidDataException("Seasonal light graph belongs to a different selected database.");
        var identity = TerrainSeasonResources.Identity(asset);
        var settings = new Dictionary<uint, DirectionalLightSettings>();
        DirectionalLightSettings Import(DokObject declaration)
        {
            if (!settings.TryGetValue(declaration.ObjectId, out var value))
            {
                value = DirectionalLightSettings.Import(declaration);
                settings.Add(declaration.ObjectId, value);
            }
            return value;
        }
        var outer = Object(database, OuterListObjectId, ListContainerClassId);
        uint[] roots = ListKeys(outer);
        var providers = roots.Select(rootKey =>
        {
            var root = Object(database, rootKey, DirectionalLightSource.SourceClass);
            var rootSettings = Import(root);
            var declaration = rootSettings.ToDeclaration(root.ObjectId);
            uint providerKey = rootSettings.ProviderObjectKey;
            if (providerKey == 0) throw new InvalidDataException("Season Directional root has no declared provider list.");
            uint[] children = ListKeys(Object(database, providerKey, ListContainerClassId));
            if (children.Length != 5)
                throw new NotSupportedException("Directional provider selection supports one selector followed by four children.");
            var selector = ReadSelector(Object(database, children[0], SelectorClassId));
            var lights = children.Skip(1).Select(childKey =>
            {
                var child = Object(database, childKey, DirectionalLightSource.SourceClass);
                var childSettings = Import(child);
                if (childSettings.ProviderObjectKey != 0)
                    throw new NotSupportedException("Nested Directional provider lists need another explicit source consumer.");
                return childSettings.ToDeclaration(child.ObjectId);
            }).ToArray();
            return new MapSeasonDirectionalLightProvider(declaration, providerKey, selector, Array.AsReadOnly(lights));
        }).ToArray();
        return new(database, identity, OuterListObjectId, Array.AsReadOnly(providers), settings);
    }

    private static uint[] ListKeys(DokObject source)
    {
        uint[] keys = Words(Field(source, 0xFF26EE41, 5));
        if (keys.Length == 0 || keys.Any(key => key == 0) || keys.Distinct().Count() != keys.Length)
            throw new InvalidDataException("Season light lists require distinct nonzero local source keys.");
        return keys;
    }

    private static MapSeasonLightSelectorDeclaration ReadSelector(DokObject source)
    {
        uint[] first = Words(Field(source, 0xF357B5DC, 5, 2));
        uint[] second = Words(Field(source, 0x2672FAC7, 5, 2));
        float[] scalars = Floats(Field(source, 0xF840EF92, 8, 2));
        uint key = Word(source, 0xB0B5B508);
        float[] axis1 = Floats(Field(source, 0x642A6605, 8));
        float[] axis2 = Floats(Field(source, 0x642A6643, 8));
        return new(source.ObjectId, first[0], first[1], second[0], second[1], scalars[0], scalars[1], key,
            Array.AsReadOnly(axis1), Array.AsReadOnly(axis2));
    }

    private static DokObject Object(MapSeasonActionDatabase database, uint key, uint classId)
    {
        DokObject[] found = database.Objects.Document.Objects.Where(o => o.ObjectId == key).ToArray();
        if (key == 0 || found.Length != 1 || found[0].Kind != DokObjectKind.Inline || found[0].ClassId != classId)
            throw new InvalidDataException($"Season light graph needs unique inline object {key:X8}/class{classId:X8}.");
        return found[0];
    }

    private static DokProperty Field(DokObject source, uint hash, uint type, int? count = null)
    {
        DokProperty[] matches = source.Properties.Where(property => property.NameHash == hash).ToArray();
        if (matches.Length != 1)
            throw new InvalidDataException($"Season light property {hash:X8} is missing or ambiguous.");
        DokProperty property = matches[0];
        if (property.TypeCode != type || property.ElementCount < 0 ||
            (count is int expected && property.ElementCount != expected) || property.ElementSize != 4 ||
            property.RawValue.Length != (long)property.ElementCount * 4)
            throw new InvalidDataException($"Season light property {hash:X8} has an invalid typed descriptor.");
        return property;
    }
    private static uint Word(DokObject source, uint hash) =>
        BinaryPrimitives.ReadUInt32LittleEndian(Field(source, hash, 5, 1).RawValue);
    private static uint[] Words(DokProperty source) => Enumerable.Range(0, source.ElementCount)
        .Select(i => BinaryPrimitives.ReadUInt32LittleEndian(source.RawValue.AsSpan(i * 4, 4))).ToArray();
    private static float[] Floats(DokProperty source)
    {
        float[] values = Enumerable.Range(0, source.ElementCount)
            .Select(i => BinaryPrimitives.ReadSingleLittleEndian(source.RawValue.AsSpan(i * 4, 4))).ToArray();
        if (values.Any(v => !float.IsFinite(v))) throw new InvalidDataException("Season selector consumed float is nonfinite.");
        return values;
    }
}
