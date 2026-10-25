using System.Buffers.Binary;
using System.Collections.ObjectModel;
using San14.Data.Formats;

namespace San14.Data;

public sealed record MapEffectDeclaration(LoadedObjects Definitions, DokObject Object);
public sealed record MapEffectTextureSlot(int Slot, MapEffectDeclaration Declaration);
public sealed record MapEffectShaderBank(int Index, NamedModelShader? Declaration);

/// <summary>Installed effect declarations and encoded resources owned by one map
/// resource generation. Discard and reload this owner when its resource generation
/// changes. Loading declarations does not create an effect instance or GPU state.</summary>
public sealed class LoadedMapEffectResources
{
    private readonly Dictionary<int, StaticTextureBinding> textures = [];
    private readonly Dictionary<uint, LoadedTextureContainer> containers = [];
    private readonly Dictionary<int, LoadedShaderContainer> shaders = [];

    internal LoadedMapEffectResources(int kind, LoadedMapEffectInfo info, G1EffectInfoRow infoRow,
        MapEffectDeclaration declaration,
        MapEffectDeclaration displayset, LoadedG1Effect effect, LoadedG1EffectModel model,
        MapEffectDeclaration textureGroup, LoadedRdbAsset textureTable,
        Dictionary<int, MapEffectTextureSlot> textureSlots, NamedModelShader shader,
        IReadOnlyList<MapEffectShaderBank> shaderBanks)
    {
        Kind = kind; Info = info; InfoRow = infoRow;
        Declaration = declaration; Displayset = displayset; Effect = effect;
        Model = model; TextureGroup = textureGroup; TextureTable = textureTable;
        TextureSlots = new ReadOnlyDictionary<int, MapEffectTextureSlot>(textureSlots);
        Shader = shader; ShaderBanks = shaderBanks;
    }

    public int Kind { get; }
    public LoadedMapEffectInfo Info { get; }
    public G1EffectInfoRow InfoRow { get; }
    public MapEffectDeclaration Declaration { get; }
    public MapEffectDeclaration Displayset { get; }
    public LoadedG1Effect Effect { get; }
    public LoadedG1EffectModel Model { get; }
    public MapEffectDeclaration TextureGroup { get; }
    public LoadedRdbAsset TextureTable { get; }
    public IReadOnlyDictionary<int, MapEffectTextureSlot> TextureSlots { get; }
    public NamedModelShader Shader { get; }
    /// <summary>Index zero is the own program; dependency indices follow it.
    /// Literal zero dependencies retain null entries.</summary>
    public IReadOnlyList<MapEffectShaderBank> ShaderBanks { get; }

    /// <summary>Load the selected slot's encoded G1T image. Repeated requests
    /// reuse this owner's binding and shared texture container, without decoding pixels.</summary>
    public StaticTextureBinding LoadTextureSlot(GameResourceRepository repository, int slot)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if (textures.TryGetValue(slot, out var found)) return found;
        if (!TextureSlots.TryGetValue(slot, out var selected))
            throw new KeyNotFoundException($"Effect {Kind} has no declared texture slot {slot}.");
        var declaration = selected.Declaration;
        var binding = StaticTextureResources.Load(declaration.Definitions,
            [declaration.Object.ObjectId], id =>
            {
                if (!containers.TryGetValue(id, out var resource))
                    containers.Add(id, resource = repository.LoadTextureContainer("RRPreview", id));
                return resource;
            }).Single();
        textures.Add(slot, binding);
        return binding;
    }

    /// <summary>Load a caller-selected shader bank. Null dependencies stay null;
    /// effect material/pass/feature selection is the draw consumer's responsibility.</summary>
    public LoadedShaderContainer? LoadShaderBank(GameResourceRepository repository, int index)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if ((uint)index >= (uint)ShaderBanks.Count) throw new ArgumentOutOfRangeException(nameof(index));
        if (ShaderBanks[index].Declaration is not { } declaration) return null;
        if (!shaders.TryGetValue(index, out var loaded))
            shaders.Add(index, loaded = ModelShaderResources.LoadDeclaredG1S(repository, declaration));
        return loaded;
    }
}

/// <summary>Resolve territory, foothold and ordinary tactic effects through their installed
/// CE1, Displayset, texture group, slot table and Effect2 declarations. Resource
/// identifiers here select original format objects; they are not captured draws.</summary>
public static class MapEffectResources
{
    private static readonly string[] Archives =
        ["system", "KIDSSystemResource", "MaterialEditor", "FieldEditor4", "CharacterEditor", "ScreenLayout", "RRPreview"];

    public static LoadedMapEffectResources Load(GameResourceRepository repository, int kind,
        RenderResourceContext? context = null, LoadedMapEffectInfo? info = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        string name = kind switch
        {
            4 => "map_unit_effect_007", 5 => "map_unit_effect_008",
            7 => "map_unit_effect_010", 8 => "map_unit_effect_011",
            9 => "map_unit_effect_013",
            // The manager constructor declares names by kind. The complete
            // name table is sparse; these are its ordinary result entries.
            16 => "map_unit_effect_043", 17 => "map_unit_effect_044",
            18 => "map_unit_effect_045", 19 => "map_unit_effect_046",
            20 => "map_unit_effect_047", 21 => "map_unit_effect_048",
            22 => "map_unit_effect_049", 23 => "map_unit_effect_050",
            24 => "map_unit_effect_051", 25 => "map_unit_effect_052",
            26 => "map_unit_effect_053", 27 => "map_unit_effect_054",
            28 => "map_unit_effect_055", 29 => "map_unit_effect_056",
            30 => "map_unit_effect_057", 31 => "map_unit_effect_058",
            32 => "map_unit_effect_059", 33 => "map_unit_effect_060",
            34 => "map_unit_effect_061", 35 => "map_unit_effect_062",
            36 => "map_unit_effect_063", 37 => "map_unit_effect_064",
            65 => "map_unit_effect_099", 66 => "map_unit_effect_100",
            67 => "map_unit_effect_106",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported map effect kind.")
        };
        // A map owner may share this table across its effect kinds. Overrides
        // select the namespace through the actual row, rather than a frozen flag.
        info ??= repository.LoadEffectInfo();
        var infoRow = info.ReadRow(kind);
        if (infoRow.UsesFe4)
            throw new NotSupportedException($"Effect {kind} selects the FE4 namespace, whose declaration binding is not implemented here.");
        uint key = ResourceNameHash.Compute($"CE1Effect\uFF3B{name}\uFF3D");
        var definitions = new Dictionary<uint, LoadedObjects>();
        var effect = Require(key, 0xE736B039, 0x97485E9B);
        var displayset = Require(Word(effect, 0x0B6E1578), 0xEC5647E9, 0x5ECE222A);
        var textureGroup = Require(Word(displayset, 0xF92C5190), 0x3059B9C3, 0xD14D76EF);
        var table = repository.LoadRdbAsset("RRPreview", Word(textureGroup, 0x7A1E1EF8));
        if (table.Bytes.Length == 0 || table.Bytes.Length % 8 != 0)
            throw new InvalidDataException($"Effect texture table {table.ResourceUri} requires complete eight-byte rows.");
        var slots = new Dictionary<int, MapEffectTextureSlot>();
        for (int offset = 0; offset < table.Bytes.Length; offset += 8)
        {
            int slot = BinaryPrimitives.ReadInt32LittleEndian(table.Bytes.AsSpan(offset, 4));
            uint objectId = BinaryPrimitives.ReadUInt32LittleEndian(table.Bytes.AsSpan(offset + 4, 4));
            var proxy = Require(objectId, 0xFF7DBFD4, 0xD14D76EF);
            if (slot < 0 || !slots.TryAdd(slot, new(slot, proxy)))
                throw new InvalidDataException($"Effect texture table {table.ResourceUri} has an invalid or repeated slot {slot}.");
        }
        var shaderObject = Resolve(Word(displayset, 0xD78969E0), 0x718CCB8B);
        var shader = ModelShaderResources.ReadDeclaredShader(shaderObject.Definitions, shaderObject.Object.ObjectId);
        var banks = new List<MapEffectShaderBank> { new(0, shader) };
        foreach (uint dependency in shader.DependencyObjectHashes)
        {
            NamedModelShader? provider = null;
            if (dependency != 0)
            {
                var source = Resolve(dependency, 0x718CCB8B);
                provider = ModelShaderResources.ReadDeclaredShader(source.Definitions, source.Object.ObjectId);
            }
            banks.Add(new(banks.Count, provider));
        }
        return new(kind, info, infoRow, effect, displayset,
            repository.LoadEffect("RRPreview", Word(displayset, 0x96D20025)),
            repository.LoadEffectModel("RRPreview", Word(displayset, 0x8D69C872)),
            textureGroup, table, slots, shader, banks.AsReadOnly());

        MapEffectDeclaration Require(uint objectId, uint classId, uint library)
        {
            var result = Resolve(objectId, library);
            if (result.Object.ClassId != classId)
                throw new NotSupportedException($"Effect object {objectId:X8} has unsupported class {result.Object.ClassId:X8}; expected {classId:X8}.");
            return result;
        }

        MapEffectDeclaration Resolve(uint objectId, uint library)
        {
            LoadedObjects source;
            DokObject obj;
            if (context is not null)
            {
                var descriptor = context.Lookup(objectId) ??
                    throw new InvalidDataException($"Current render resources have no effect object {objectId:X8}.");
                source = descriptor.Source; obj = descriptor.Object;
            }
            else
            {
                source = LoadDefinitions(library);
                obj = Unique(source, objectId);
            }
            var visited = new HashSet<(string, uint)>();
            while (obj.Kind == DokObjectKind.Reference)
            {
                if (!visited.Add((source.Source.ResourceUri, obj.ObjectId)))
                    throw new InvalidDataException("Cyclic effect resource reference.");
                if (obj.Properties.Count != 0 || obj.Reference is not { } reference)
                    throw new NotSupportedException("Effect resource reference property overlays require their merge implementation.");
                source = LoadDefinitions(reference.ResourceId);
                obj = Unique(source, reference.ObjectId);
            }
            return new(source, obj);
        }

        LoadedObjects LoadDefinitions(uint id)
        {
            if (!definitions.TryGetValue(id, out var loaded))
            {
                loaded = id switch
                {
                    0x97485E9B or 0x5ECE222A or 0xD14D76EF => repository.LoadObjects("KIDSSystemResource", id),
                    0x718CCB8B => repository.LoadObjects("system", id),
                    _ => repository.LoadObjects(id, Archives)
                };
                definitions.Add(id, loaded);
            }
            return loaded;
        }
    }

    private static DokObject Unique(LoadedObjects source, uint objectId)
    {
        var matches = source.Document.Objects.Where(obj => obj.ObjectId == objectId).ToArray();
        if (objectId == 0 || matches.Length != 1)
            throw new InvalidDataException($"{source.Source.ResourceUri} requires one effect object {objectId:X8}.");
        return matches[0];
    }

    private static uint Word(MapEffectDeclaration source, uint name)
    {
        var fields = source.Object.Properties.Where(field => field.NameHash == name).ToArray();
        if (fields.Length != 1 || fields[0] is not { TypeCode: 5, ElementCount: 1, ElementSize: 4 } ||
            fields[0].RawValue.Length != 4)
            throw new InvalidDataException($"Effect object {source.Object.ObjectId:X8} requires a scalar reference {name:X8}.");
        return BinaryPrimitives.ReadUInt32LittleEndian(fields[0].RawValue);
    }
}
