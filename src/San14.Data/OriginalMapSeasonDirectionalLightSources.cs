using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalMapSeasonLightSelectorDeclaration(uint ObjectId,
    IReadOnlyList<uint> FirstWords, IReadOnlyList<uint> SecondWords,
    IReadOnlyList<float> Scalars, uint ObjectKey, IReadOnlyList<float> FirstAxis,
    IReadOnlyList<float> SecondAxis, IReadOnlyList<int> Indices);

/// <summary>One declared root and its provider's selector followed by four
/// ordered children. This is source topology, without registration or selection.</summary>
public sealed record OriginalMapSeasonDirectionalLightProvider(
    OriginalDirectionalLightDeclaration Root, uint ProviderListObjectId,
    OriginalMapSeasonLightSelectorDeclaration Selector,
    IReadOnlyList<OriginalDirectionalLightDeclaration> Children);

public sealed record OriginalMapSeasonDirectionalLightSourceSet(
    OriginalMapSeasonActionDatabase Database, OriginalTerrainSeasonResourceIdentity Source,
    uint OuterListObjectId, IReadOnlyList<OriginalMapSeasonDirectionalLightProvider> Roots);

/// <summary>Read the actual selected seasonal database's ListContainer graph.
/// SourceGroupMask is an unshifted uint. Only the Directional declaration's
/// quaternion/color rows are decoded here; modifiers and actor constructors
/// belong to their own source consumers.</summary>
public static class OriginalMapSeasonDirectionalLightSources
{
    public const uint OuterListObjectId = 0x30C0B07F;
    public const uint ListContainerClassId = 0x2EB79141;
    public const uint SelectorClassId = 0x13696D79;

    public static OriginalMapSeasonDirectionalLightSourceSet Load(OriginalGameRepository repository, int sourceMonth)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var action = OriginalMapSeasonResourceConsumer.LoadActionSource(repository, sourceMonth);
        return Read(OriginalMapSeasonResourceConsumer.LoadActionDatabase(repository, action));
    }

    public static OriginalMapSeasonDirectionalLightSourceSet Read(OriginalMapSeasonActionDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        // Reparse checked source bytes and retain an independent source snapshot.
        var asset = database.Objects.Source with { Bytes = database.Objects.Source.Bytes.ToArray() };
        database = OriginalMapSeasonResourceConsumer.ReadActionDatabase(database.Action,
            new(DokReader.Read(asset.Bytes), asset));
        var identity = OriginalTerrainSeasonResources.Identity(asset);
        var outer = Object(database, OuterListObjectId, ListContainerClassId);
        uint[] roots = ListKeys(outer);
        var providers = roots.Select(rootKey =>
        {
            var root = Object(database, rootKey, OriginalDirectionalLightSource.SourceClass);
            var declaration = Directional(root);
            uint providerKey = Word(root, 57, 0xA7DFA945);
            if (providerKey == 0) throw new InvalidDataException("Season Directional root has no declared provider list.");
            uint[] children = ListKeys(Object(database, providerKey, ListContainerClassId));
            var selector = ReadSelector(Object(database, children[0], SelectorClassId));
            var lights = children.Skip(1).Select(childKey =>
            {
                var child = Object(database, childKey, OriginalDirectionalLightSource.SourceClass);
                if (Word(child, 57, 0xA7DFA945) != 0)
                    throw new NotSupportedException("Nested Directional provider lists need another explicit source consumer.");
                return Directional(child);
            }).ToArray();
            return new OriginalMapSeasonDirectionalLightProvider(declaration, providerKey, selector, Array.AsReadOnly(lights));
        }).ToArray();
        return new(database, identity, OuterListObjectId, Array.AsReadOnly(providers));
    }

    private static OriginalDirectionalLightDeclaration Directional(DokObject source)
    {
        if (source.Properties.Count != 561)
            throw new InvalidDataException("Season Directional declaration requires the original 561-property prototype.");
        uint group = Word(source, 8, 0xC0C5A5B5);
        var declaration = OriginalDirectionalLightSource.Read(source);
        if (declaration.SourceGroupMask != group)
            throw new InvalidDataException("Season Directional source group property does not match its typed declaration.");
        return declaration;
    }

    private static uint[] ListKeys(DokObject source)
    {
        if (source.Properties.Count != 3)
            throw new InvalidDataException("Season ListContainer requires its three declared source properties.");
        _ = Word(source, 0, 0xFC63FB31);
        _ = Word(source, 1, 0x04E203A7);
        uint[] keys = Words(Field(source, 2, 0xFF26EE41, 5, 5));
        if (keys.Any(k => k == 0) || keys.Distinct().Count() != keys.Length)
            throw new InvalidDataException("Season light list requires five distinct nonzero local source keys.");
        return keys;
    }

    private static OriginalMapSeasonLightSelectorDeclaration ReadSelector(DokObject source)
    {
        if (source.Properties.Count != 7)
            throw new InvalidDataException("Season light selector requires its seven declared properties.");
        uint[] first = Words(Field(source, 0, 0xF357B5DC, 5, 2));
        uint[] second = Words(Field(source, 1, 0x2672FAC7, 5, 2));
        float[] scalars = Floats(Field(source, 2, 0xF840EF92, 8, 2));
        uint key = Word(source, 3, 0xB0B5B508);
        float[] axis1 = Floats(Field(source, 4, 0x642A6605, 8, source.Properties[4].ElementCount));
        float[] axis2 = Floats(Field(source, 5, 0x642A6643, 8, source.Properties[5].ElementCount));
        int[] indices = Words(Field(source, 6, 0x15247A48, 4, 3)).Select(v => unchecked((int)v)).ToArray();
        return new(source.ObjectId, Array.AsReadOnly(first), Array.AsReadOnly(second), Array.AsReadOnly(scalars), key,
            Array.AsReadOnly(axis1), Array.AsReadOnly(axis2), Array.AsReadOnly(indices));
    }

    private static DokObject Object(OriginalMapSeasonActionDatabase database, uint key, uint classId)
    {
        DokObject[] found = database.Objects.Document.Objects.Where(o => o.ObjectId == key).ToArray();
        if (key == 0 || found.Length != 1 || found[0].Kind != DokObjectKind.Inline || found[0].ClassId != classId)
            throw new InvalidDataException($"Season light graph needs unique inline object {key:X8}/class{classId:X8}.");
        return found[0];
    }

    private static DokProperty Field(DokObject source, int ordinal, uint hash, uint type, int count)
    {
        if (ordinal < 0 || ordinal >= source.Properties.Count)
            throw new InvalidDataException("Season light source property is missing.");
        var property = source.Properties[ordinal];
        if (property.Index != ordinal || property.NameHash != hash || property.TypeCode != type ||
            property.ElementCount != count || property.ElementSize != 4 || property.RawValue.Length != (long)count * 4 ||
            source.Properties.Count(p => p.NameHash == hash) != 1)
            throw new InvalidDataException($"Season light property {ordinal}/{hash:X8} has a different typed source descriptor.");
        return property;
    }
    private static uint Word(DokObject source, int ordinal, uint hash) =>
        BinaryPrimitives.ReadUInt32LittleEndian(Field(source, ordinal, hash, 5, 1).RawValue);
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
