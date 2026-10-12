using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The six source-created City/Gate offset prototypes and the actual
/// category-2 animation resources. Current population borrows these same
/// immutable tables; exported files are output, never the CPU authority.</summary>
public sealed class CityGateAccessoryResources
{
    private static readonly (uint DefinitionId, int Count)[] Prototypes =
    [
        (0xFCCE7230, 16), (0xC5CF6971, 12), (0x8ED060B2, 20),
        (0x57D157F3, 4), (0x20D24F34, 4), (0x51DA50EF, 2),
    ];
    private readonly IReadOnlyList<IReadOnlyList<Vector4>> offsets;

    private CityGateAccessoryResources(IReadOnlyList<Vector4>[] offsets,
        LoadedCityGateAccessoryAnimation animation)
    {
        this.offsets = Array.AsReadOnly(offsets);
        Animation = animation;
    }

    public LoadedCityGateAccessoryAnimation Animation { get; }
    public int StyleCount => offsets.Count;
    public IReadOnlyList<Vector4> this[int style] => GetOffsets(style);

    public IReadOnlyList<Vector4> GetOffsets(int style)
    {
        if ((uint)style >= offsets.Count) throw new ArgumentOutOfRangeException(nameof(style));
        return offsets[style];
    }

    public static CityGateAccessoryResources Load(GameResourceRepository repository,
        LoadedSceneResources scene)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(scene);
        var offsets = new IReadOnlyList<Vector4>[Prototypes.Length];
        var models = new Dictionary<uint, LoadedModelTransforms>();
        for (int style = 0; style < Prototypes.Length; style++)
        {
            var prototype = Prototypes[style];
            var definitions = scene.ModelCatalog.Document.Objects
                .Where(value => value.ObjectId == prototype.DefinitionId).ToArray();
            if (definitions.Length != 1 || definitions[0].Kind != DokObjectKind.Inline ||
                definitions[0].ClassId != ScenePlacementReader.ModelWorldPqClass)
                throw new InvalidDataException($"City/Gate offset style {style} requires one inline WorldPQ prototype {prototype.DefinitionId:X8}.");
            var definition = definitions[0];
            uint modelKey = Reference(definition, 0x0B6E1578);
            var matches = scene.Dependencies.SelectMany(dependency => dependency.Resource.Document.Objects
                .Where(value => value.ObjectId == modelKey)
                .Select(value => (Resource: dependency.Resource, Object: value))).ToArray();
            if (matches.Length != 1 || matches[0].Object.Kind != DokObjectKind.Inline ||
                matches[0].Object.ClassId != 0xD40B3C8F)
                throw new InvalidDataException($"City/Gate offset style {style} requires one typed Model {modelKey:X8} in the scene's declared dependencies.");
            var match = matches[0];
            uint fileId = Reference(match.Object, 0x8AB68B3F);
            if (!models.TryGetValue(fileId, out var model))
                models.Add(fileId, model = repository.LoadModelTransforms("FieldEditor4", fileId));
            var source = LoadedCityGateAccessoryJointOffsets.CreateFreshSourceDefinition(
                scene.ModelCatalog, definition, new(match.Resource, match.Object, fileId), model);
            offsets[style] = source.CreateManagerOffsets(prototype.Count);
        }
        return new(offsets, LoadedCityGateAccessoryAnimation.Load(repository));
    }

    /// <summary>Export the existing Native manager-offset interface beneath the
    /// actual accessory input root. No resource loads or pose updates occur here.</summary>
    public void WriteNativeInputs(string accessoryInputRoot)
    {
        string directory = Path.Combine(accessoryInputRoot, "manager-offsets");
        Directory.CreateDirectory(directory);
        for (int style = 0; style < offsets.Count; style++)
        {
            var rows = offsets[style];
            byte[] bytes = new byte[rows.Count * 16];
            for (int index = 0; index < rows.Count; index++)
            {
                var value = rows[index];
                int at = index * 16;
                BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(at), value.X);
                BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(at + 4), value.Y);
                BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(at + 8), value.Z);
                BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(at + 12), value.W);
            }
            File.WriteAllBytes(Path.Combine(directory, $"style-{style}.bin"), bytes);
        }
    }

    private static uint Reference(DokObject source, uint hash)
    {
        var fields = source.Properties.Where(value => value.NameHash == hash).ToArray();
        if (fields.Length != 1 || fields[0].TypeCode != 5 || fields[0].ElementCount != 1 ||
            fields[0].ElementSize != 4 || fields[0].RawValue.Length != 4)
            throw new InvalidDataException($"City/Gate offset object {source.ObjectId:X8} requires scalar resource reference {hash:X8}.");
        return BinaryPrimitives.ReadUInt32LittleEndian(fields[0].RawValue);
    }
}
