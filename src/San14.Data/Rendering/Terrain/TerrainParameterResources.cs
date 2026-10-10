using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public sealed record TerrainParameterColumn(DokProperty Property, IReadOnlyList<uint> Float32Bits);
/// <summary>LandscapePalette CPU loader fields; not a GPU constant-buffer layout.</summary>
public sealed record TerrainPaletteEntry(int ParameterIndex, uint MaterialNameHash,
    uint BlendSharpnessBits, uint ScaleUBits, uint ScaleVBits,
    uint TessScaleBits, uint TessBiasBits);
/// <summary>Original Landscape CPU lookup packet; the index is not a proven GPU texture layer.</summary>
public sealed record TerrainDetailPaletteEntry(TerrainPaletteEntry Fields, uint DetailTextureNameIndex);
public sealed record TerrainIdentifierMatch(int ParameterIndex, uint Identifier, IReadOnlyList<int> IdentifierListIndices);
public sealed record LoadedTerrainParameterResources(LoadedSceneResources Scene, LoadedObjects Wrapper,
    DokObject WrapperRoot, DokObject ParameterObject, DokObject TerrainRoot, DokObject TextureArrayInfo,
    DokProperty IdentifierProperty, DokProperty ParameterIdentifierProperty, uint DeclaredParameterCount,
    IReadOnlyList<uint> Identifiers, IReadOnlyList<uint> ParameterIdentifiers,
    IReadOnlyList<TerrainParameterColumn> Columns, IReadOnlyList<TerrainIdentifierMatch> IdentifierMatches,
    IReadOnlyList<int> IdentifiersWithoutParameters, IReadOnlyList<int> ParametersWithoutIdentifiers)
{
    public IReadOnlyList<TerrainPaletteEntry> PaletteEntries { get; init; } = [];
    public IReadOnlyList<TerrainDetailPaletteEntry> DetailPaletteEntries { get; init; } = [];
    public DokProperty? PaletteReferenceProperty { get; init; }
}

/// <summary>
/// Reads the explicit LandscapeComposition palette reference in the scene's
/// declared dependency scope. The original first-name lookup is distinct from texture layers.
/// Float bit patterns and both source orders are retained. Named CPU palette fields
/// follow the original descriptor/loader; texture layers and shader uploads remain unresolved.
/// </summary>
public static class TerrainParameterResources
{
    private const uint LodDistanceScaleProperty = 0x0575B6F4;
    private static readonly uint[] ParameterNames = [0x859D80F7, 0xA69BBD47, 0x9AE52E66, 0xD2A48180, 0x03BB19B1];

    /// <summary>Decode the required adaptive-geometry setting when that geometry is initialized.</summary>
    public static float ReadLodDistanceScale(LoadedTerrainParameterResources parameters)
    {
        DokProperty[] scales = parameters.TerrainRoot.Properties.Where(p => p.NameHash == LodDistanceScaleProperty).ToArray();
        if (scales.Length != 1 || scales[0].TypeCode != 8 || scales[0].ElementCount != 1 || scales[0].RawValue.Length != 4)
            throw new InvalidDataException("Original Composition requires explicit LODDistanceScale.");
        float scale = BinaryPrimitives.ReadSingleLittleEndian(scales[0].RawValue);
        if (!float.IsFinite(scale) || scale < 0)
            throw new InvalidDataException("Unsupported original LOD distance scale.");
        return scale;
    }

    public static LoadedTerrainParameterResources Read(LoadedSceneResources scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        LoadedObjects terrain = scene.TerrainBundle;
        string context = scene.Scene.Source.ResourceUri;
        DokObject terrainRoot = OneClass(terrain, 0x774DDC07, context);
        // Resolve within declared sources, never by scanning the installation/global ObjectId space.
        var targetMatches = scene.Dependencies.SelectMany(dependency => dependency.Resource.Document.Objects
            .Where(item => item.ObjectId == terrainRoot.ObjectId)
            .Select(item => (Source: dependency.Resource, Object: item))).ToArray();
        if (targetMatches.Length != 1 || targetMatches[0].Source.Source.ResourceUri != terrain.Source.ResourceUri ||
            targetMatches[0].Source.Source.DecodedSha256 != terrain.Source.DecodedSha256)
            throw Error(context, $"terrain target {terrainRoot.ObjectId:X8} is missing, ambiguous or from a different source");
        var wrappers = new List<(LoadedObjects Source, DokObject Root)>();
        foreach (SceneDependency dependency in scene.Dependencies)
            foreach (DokObject item in dependency.Resource.Document.Objects)
                if (item.Kind == DokObjectKind.Inline && item.ClassId == 0x8E1ECB72 &&
                    Scalar(item, 0x445D803E, dependency.Resource.Source.ResourceUri) == terrainRoot.ObjectId)
                    wrappers.Add((dependency.Resource, item));
        if (wrappers.Count != 1)
            throw Error(context, $"expected one wrapper targeting terrain {terrainRoot.ObjectId:X8}; found {wrappers.Count}");
        (LoadedObjects wrapper, DokObject wrapperRoot) = wrappers[0];
        DokProperty paletteReference = Property(terrainRoot, 0xF4553A8D, 5, terrain.Source.ResourceUri);
        uint paletteId = Scalar(terrainRoot, 0xF4553A8D, terrain.Source.ResourceUri);
        DokObject[] parameterMatches = wrapper.Document.Objects.Where(item => item.ObjectId == paletteId &&
            item.Kind == DokObjectKind.Inline && item.ClassId == 0xC388EA58).ToArray();
        if (parameterMatches.Length != 1)
            throw Error(context, $"palette reference {paletteId:X8} requires one C388EA58 object in declared wrapper {wrapper.Source.ResourceUri}");
        DokObject parameters = parameterMatches[0];

        uint listId = Scalar(terrainRoot, 0x45B41815, terrain.Source.ResourceUri);
        DokObject[] lists = terrain.Document.Objects.Where(item => item.ObjectId == listId).ToArray();
        if (lists.Length != 1 || lists[0].Kind != DokObjectKind.Inline || lists[0].ClassId != 0x09F32924)
            throw Error(terrain.Source.ResourceUri, $"texture identifier object {listId:X8} is missing, ambiguous or unsupported");
        DokObject list = lists[0];
        DokProperty identifierProperty = Property(list, 0xF35CAD58, 5, terrain.Source.ResourceUri);
        uint[] identifiers = Words(identifierProperty);
        string parameterContext = $"{wrapper.Source.ResourceUri} object {parameters.ObjectId:X8}";
        uint count = Scalar(parameters, 0xF7A82AEB, parameterContext);
        DokProperty parameterIds = Property(parameters, 0x497CB2C7, 5, parameterContext);
        uint[] parameterIdentifiers = Words(parameterIds);
        if (count != parameterIdentifiers.Length)
            throw Error(parameterContext, "declared parameter count differs from identifier array length");
        var columns = new List<TerrainParameterColumn>(ParameterNames.Length);
        foreach (uint name in ParameterNames)
        {
            DokProperty property = Property(parameters, name, 8, parameterContext);
            if (property.ElementCount != count)
                throw Error(parameterContext, $"parameter property {name:X8} count differs from declared count");
            columns.Add(new(property, Words(property)));
        }
        // Preserve the original column order too; required names identify columns, not output ordering.
        columns.Sort((left, right) => left.Property.Index.CompareTo(right.Property.Index));
        var byName = columns.ToDictionary(column => column.Property.NameHash);
        var entries = new TerrainPaletteEntry[parameterIdentifiers.Length];
        for (int i = 0; i < entries.Length; i++)
            entries[i] = new(i, parameterIdentifiers[i],
                byName[0x859D80F7].Float32Bits[i], byName[0xA69BBD47].Float32Bits[i],
                byName[0x9AE52E66].Float32Bits[i], byName[0xD2A48180].Float32Bits[i],
                byName[0x03BB19B1].Float32Bits[i]);
        var positions = new Dictionary<uint, List<int>>();
        for (int i = 0; i < identifiers.Length; i++)
        {
            if (!positions.TryGetValue(identifiers[i], out List<int>? indices)) positions[identifiers[i]] = indices = new();
            indices.Add(i);
        }
        // Share one position array per distinct identifier. Repeated rows must not
        // cause quadratic allocation or traversal for valid override resources.
        var frozenPositions = positions.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
        var matches = new List<TerrainIdentifierMatch>(parameterIdentifiers.Length);
        var referenced = new HashSet<uint>();
        var unmatched = new List<int>();
        for (int i = 0; i < parameterIdentifiers.Length; i++)
        {
            int[] indices = frozenPositions.TryGetValue(parameterIdentifiers[i], out int[]? found) ? found : [];
            if (indices.Length > 0) referenced.Add(parameterIdentifiers[i]);
            if (indices.Length == 0) unmatched.Add(i);
            matches.Add(new(i, parameterIdentifiers[i], indices));
        }
        var detailEntries = entries.Select(entry => new TerrainDetailPaletteEntry(entry,
            frozenPositions.TryGetValue(entry.MaterialNameHash, out int[]? found)
                ? (uint)found[0] : uint.MaxValue)).ToArray();
        return new(scene, wrapper, wrapperRoot, parameters, terrainRoot, list, identifierProperty, parameterIds,
            count, identifiers, parameterIdentifiers, columns, matches,
            Enumerable.Range(0, identifiers.Length).Where(i => !referenced.Contains(identifiers[i])).ToArray(), unmatched)
        {
            PaletteEntries = Array.AsReadOnly(entries),
            DetailPaletteEntries = Array.AsReadOnly(detailEntries),
            PaletteReferenceProperty = paletteReference
        };
    }

    private static DokObject OneClass(LoadedObjects source, uint classId, string context)
    {
        DokObject[] items = source.Document.Objects.Where(item => item.Kind == DokObjectKind.Inline && item.ClassId == classId).ToArray();
        if (items.Length != 1) throw Error(context, $"{source.Source.ResourceUri} requires one inline class {classId:X8}; found {items.Length}");
        return items[0];
    }
    private static uint Scalar(DokObject item, uint name, string context)
    {
        DokProperty property = Property(item, name, 5, context);
        if (property.ElementCount != 1) throw Error(context, $"object {item.ObjectId:X8} property {name:X8} must be scalar");
        return BinaryPrimitives.ReadUInt32LittleEndian(property.RawValue);
    }
    private static DokProperty Property(DokObject item, uint name, uint type, string context)
    {
        DokProperty[] properties = item.Properties.Where(property => property.NameHash == name).ToArray();
        if (properties.Length != 1 || properties[0].TypeCode != type || properties[0].ElementCount < 0 ||
            properties[0].RawValue.Length != (long)properties[0].ElementCount * 4)
            throw Error(context, $"object {item.ObjectId:X8} requires one complete type-{type} property {name:X8}");
        return properties[0];
    }
    private static uint[] Words(DokProperty property)
    {
        uint[] values = new uint[property.ElementCount];
        for (int i = 0; i < values.Length; i++) values[i] = BinaryPrimitives.ReadUInt32LittleEndian(property.RawValue.AsSpan(i * 4, 4));
        return values;
    }
    private static InvalidDataException Error(string context, string message) => new($"Terrain parameter resource {context}: {message}.");
}
