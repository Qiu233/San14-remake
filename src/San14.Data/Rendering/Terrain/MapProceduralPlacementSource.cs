using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public sealed record ProceduralBiome(int Index, IReadOnlyList<uint> ComponentIndices,
    uint DensityMapMode, uint RandomSeed, uint RawTail2);
public sealed record ProceduralComponent(int Index, IReadOnlyList<uint> ElementIndices,
    uint RawTail0, uint RawTail1);
public sealed record ProceduralElement(int Index, uint Type, uint Flags,
    uint DisplaySetNameHash, uint RandomSeed, IReadOnlyList<float> Parameters,
    IReadOnlyList<float> LodDistances);
public sealed record ProceduralTemplate(int Index, uint NameHash, bool IsInstancing,
    SceneInstance Declaration);
public sealed record ProceduralPlacementDeclaration(LoadedObjects OwnerSource,
    DokObject Owner, LoadedObjects Source, DokObject Population,
    uint FieldSizeX, uint FieldSizeZ, uint FieldOffsetX, uint FieldOffsetZ,
    uint CellsX, uint CellsZ, uint BlocksX, uint BlocksZ, uint UpdateDistance,
    uint FreeDistance, uint ViewDistance, uint TerrainType, uint GlobalFlags,
    uint AbsolutelyRenderDistanceH, uint AbsolutelyRenderDistanceV,
    IReadOnlyList<ProceduralBiome> Biomes,
    IReadOnlyList<ProceduralComponent> Components,
    IReadOnlyList<ProceduralElement> Elements,
    IReadOnlyList<ProceduralTemplate> Templates,
    ReadOnlyMemory<byte> DensityMapBytes, ReadOnlyMemory<byte> ResourceMapBytes,
    IReadOnlyList<uint> ModelInstanceLimitWords)
{
    public float CellSize => FieldSizeX / CellsX;
    public uint CellsPerBlockX => CellsX / BlocksX;
    public uint CellsPerBlockZ => CellsZ / BlocksZ;
    public string SourceEvidence => "CE:109B50-class1A83479F-registration;" +
        "ProceduralPlacement-factory141695D58/v58=BA6100;" +
        "CPU141695948/v30=BB2110->BC8840->BC6540->BB8710";
}

/// <summary>Finite original declaration import. This preserves the procedural
/// owner, biome map, element seeds and source DisplaySets. DisplaySets are
/// templates at the origin; this reader does not publish them as world instances.
/// Generation, terrain callbacks, current camera admission and GPU bindings are
/// supplied by their actual producers. Call at scene load/reload and retain.</summary>
public static class MapProceduralPlacementSource
{
    public const uint LandscapeClass = 0x8E1ECB72;
    public const uint PopulationClass = 0x1A83479F;
    public const uint PopulationPropertyHash = 0xD8E22E1D;

    public static ProceduralPlacementDeclaration ReadCeSceneDeclaration(LoadedSceneResources scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        uint landscapeId = Scalar(scene.RootObject, 0xCF878F8D);
        var owners = scene.Dependencies.SelectMany(d => d.Resource.Document.Objects
            .Where(o => o.ObjectId == landscapeId && o.Kind == DokObjectKind.Inline && o.ClassId == LandscapeClass)
            .Select(o => (Source: d.Resource, Object: o)))
            .Where(o => Scalar(o.Object, PopulationPropertyHash) != 0).ToArray();
        if (owners.Length != 1) throw new InvalidDataException("CE scene requires its one declared procedural landscape owner.");
        var owner = owners[0];
        uint populationId = Scalar(owner.Object, PopulationPropertyHash);
        var populations = scene.Dependencies.SelectMany(d => d.Resource.Document.Objects
            .Where(o => o.ObjectId == populationId && o.Kind == DokObjectKind.Inline && o.ClassId == PopulationClass)
            .Select(o => (Source: d.Resource, Object: o))).ToArray();
        if (populations.Length != 1) throw new InvalidDataException("Procedural key must resolve uniquely in actual scene dependencies.");
        var population = populations[0];
        var o = population.Object;
        uint[] sizes = UInts(o, 0x7C5D29E5), offsets = UInts(o, 0x7285AF13),
            cells = UInts(o, 0x6F1AB58B), blocks = UInts(o, 0x8DA10134);
        if (new[] { sizes, offsets, cells, blocks }.Any(a => a.Length != 2) ||
            sizes.Any(a => a == 0) || cells.Any(a => a == 0) || blocks.Any(a => a == 0) ||
            cells[0] % blocks[0] != 0 || cells[1] % blocks[1] != 0)
            throw new InvalidDataException("Procedural field/cell/block declarations have incompatible dimensions.");
        var biomes = ParseVariable(UInts(o, 0xE9567EEB), 3).Select((a, i) =>
            new ProceduralBiome(i, Array.AsReadOnly(a.Items), a.Tail[0], a.Tail[1], a.Tail[2])).ToArray();
        var components = ParseVariable(UInts(o, 0x7C441A6E), 2).Select((a, i) =>
            new ProceduralComponent(i, Array.AsReadOnly(a.Items), a.Tail[0], a.Tail[1])).ToArray();
        float[] parameters = Floats(o, 0x2803BB0F), lod = Floats(o, 0x0FD0F003);
        uint[] types = UInts(o, 0x8A1AE243), flags = UInts(o, 0x5C9EE311),
            names = UInts(o, 0xE3C9C8F0), seeds = UInts(o, 0xE065F0E9),
            setNames = UInts(o, 0x8E53AEA0);
        byte[] instancing = Field(o, 0x711A5F43, 1).RawValue;
        int n = types.Length;
        if (flags.Length != n || names.Length != n || seeds.Length != n ||
            parameters.Length != n * 9 || lod.Length != n * 4 || instancing.Length != setNames.Length)
            throw new InvalidDataException("Procedural element or template banks have incompatible source counts.");
        if (biomes.SelectMany(b => b.ComponentIndices).Any(i => i >= components.Length) ||
            components.SelectMany(c => c.ElementIndices).Any(i => i >= n) ||
            names.Any(hash => !setNames.Contains(hash)))
            throw new InvalidDataException("Procedural bank reference exceeds its original source directory.");
        var elements = Enumerable.Range(0, n).Select(i => new ProceduralElement(i,
            types[i], flags[i], names[i], seeds[i], Array.AsReadOnly(parameters[(i * 9)..(i * 9 + 9)]),
            Array.AsReadOnly(lod[(i * 4)..(i * 4 + 4)]))).ToArray();
        var templates = new ProceduralTemplate[setNames.Length];
        for (int i = 0; i < templates.Length; i++)
        {
            var declaration = population.Source.Document.Objects.Single(p => p.ObjectId == setNames[i]);
            if (declaration.Kind != DokObjectKind.Reference || declaration.Reference is not { } reference)
                throw new InvalidDataException("Procedural DisplaySet is not an original reference declaration.");
            var definitionSource = scene.ModelCatalog.Source.FileId == reference.ResourceId ? scene.ModelCatalog :
                scene.Dependencies.Single(d => d.Resource.Source.FileId == reference.ResourceId).Resource;
            var definition = definitionSource.Document.Objects.Single(p => p.ObjectId == reference.ObjectId);
            if (definition.Kind != DokObjectKind.Inline || definition.ClassId != ScenePlacementReader.ModelWorldPqClass)
                throw new InvalidDataException("Procedural DisplaySet definition is outside the finite ModelWorldPQ source family.");
            templates[i] = new(i, setNames[i], instancing[i] != 0,
                new(i, declaration, population.Source, definition, definitionSource));
        }
        var density = Field(o, 0x17ED6FB4, 8);
        var resource = Field(o, 0x4A0AA0F2, 1);
        long cellCount = (long)cells[0] * cells[1];
        if (density.ElementCount != cellCount || resource.ElementCount != cellCount ||
            resource.RawValue.Any(value => value != byte.MaxValue && value >= biomes.Length))
            throw new InvalidDataException("Procedural cell maps differ from their source dimensions or biome directory.");
        // Additional maps and ScaleMap follow independent runtime capabilities.
        // They remain source fields; this finite declaration does not invent them.
        return new(owner.Source, owner.Object, population.Source, o, sizes[0], sizes[1], offsets[0], offsets[1],
            cells[0], cells[1], blocks[0], blocks[1], Scalar(o, 0x79C02FFE), Scalar(o, 0xA2DD60A1),
            Scalar(o, 0xA3DAE7DA), Scalar(o, 0x4B80CFB1), Scalar(o, 0xDD5651BC),
            Scalar(o, 0xDD271667), Scalar(o, 0xF709A219), Array.AsReadOnly(biomes), Array.AsReadOnly(components),
            Array.AsReadOnly(elements), Array.AsReadOnly(templates), density.RawValue.ToArray(),
            resource.RawValue.ToArray(), Array.AsReadOnly(UInts(o, 0xD25C3AA6)));
    }

    private static (uint[] Items, uint[] Tail)[] ParseVariable(uint[] words, int tailCount)
    {
        var result = new List<(uint[], uint[])>();
        int offset = 0;
        while (offset < words.Length)
        {
            uint count = words[offset++];
            if (count > 64 || count + tailCount > words.Length - offset)
                throw new InvalidDataException("Procedural variable bank record exceeds its original boundary.");
            int n = (int)count;
            result.Add((words[offset..(offset + n)], words[(offset + n)..(offset + n + tailCount)]));
            offset += n + tailCount;
        }
        return result.ToArray();
    }
    private static DokProperty Field(DokObject obj, uint hash, uint type)
    {
        var p = obj.Properties.Single(p => p.NameHash == hash);
        if (p.TypeCode != type || p.RawValue.Length != (long)p.ElementCount * p.ElementSize)
            throw new InvalidDataException($"Procedural source {obj.ObjectId:X8}/{hash:X8} has incompatible field layout.");
        return p;
    }
    private static uint[] UInts(DokObject obj, uint hash)
    {
        var p = Field(obj, hash, 5);
        return Enumerable.Range(0, p.ElementCount).Select(i => BinaryPrimitives.ReadUInt32LittleEndian(p.RawValue.AsSpan(i * 4, 4))).ToArray();
    }
    private static float[] Floats(DokObject obj, uint hash)
    {
        var p = Field(obj, hash, 8);
        return Enumerable.Range(0, p.ElementCount).Select(i => BinaryPrimitives.ReadSingleLittleEndian(p.RawValue.AsSpan(i * 4, 4))).ToArray();
    }
    private static uint Scalar(DokObject obj, uint hash)
    {
        var values = UInts(obj, hash);
        if (values.Length != 1) throw new InvalidDataException("Procedural scalar requires one original DWORD.");
        return values[0];
    }
}
