using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public sealed record SceneModelReference(LoadedObjects Dictionary, DokObject ModelObject, uint ModelFileId);
public sealed record ScenePlacement(SceneInstance Source, SceneTransformValues InstanceTransform,
    SceneTransformValues DefinitionTransform, SceneModelReference? Model);
public sealed record ScenePlacements(LoadedSceneResources Scene, IReadOnlyList<ScenePlacement> Instances);

/// <summary>
/// Reads WorldPQ source fields without applying inheritance or choosing a quaternion
/// convention. Resolves model FileIds only through this scene's declared dictionaries.
/// No model, texture or height payload is loaded by this reader.
/// </summary>
public static class ScenePlacementReader
{
    public const uint ModelWorldPqClass = 0xC4B9B28D;
    public const uint EffectWorldPqClass = 0xE736B039;
    private const uint ModelClass = 0xD40B3C8F;

    public static ScenePlacements Read(LoadedSceneResources scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        var models = new Dictionary<uint, List<(LoadedObjects Resource, DokObject Object)>>();
        var objectCounts = new Dictionary<LoadedObjects, Dictionary<uint, int>>(ReferenceEqualityComparer.Instance);
        foreach (SceneDependency dependency in scene.Dependencies)
        {
            objectCounts[dependency.Resource] = dependency.Resource.Document.Objects.GroupBy(item => item.ObjectId)
                .ToDictionary(group => group.Key, group => group.Count());
            foreach (DokObject item in dependency.Resource.Document.Objects)
                if (item.Kind == DokObjectKind.Inline && item.ClassId == ModelClass)
                {
                    if (!models.TryGetValue(item.ObjectId, out var matches)) models[item.ObjectId] = matches = [];
                    matches.Add((dependency.Resource, item));
                }
        }
        var definitions = new Dictionary<(string Uri, string Hash, uint Id), (SceneTransformValues Transform, SceneModelReference? Model)>();
        var placements = new List<ScenePlacement>(scene.Instances.Count);
        foreach (SceneInstance instance in scene.Instances)
        {
            string context = $"{scene.Scene.Source.ResourceUri}, instance {instance.Index} object {instance.Object.ObjectId:X8}";
            try
            {
                if (instance.Definition.Kind != DokObjectKind.Inline ||
                    instance.Definition.ClassId is not (ModelWorldPqClass or EffectWorldPqClass))
                    throw new InvalidDataException("Definition is not a supported inline Model/Effect WorldPQ object.");
                if (instance.Object.Kind != DokObjectKind.Reference || instance.Object.Reference is not { } reference ||
                    reference.ResourceId != instance.DefinitionDocument.Source.FileId || reference.ObjectId != instance.Definition.ObjectId)
                    throw new InvalidDataException("Instance reference does not match its definition source.");
                var key = (instance.DefinitionDocument.Source.ResourceUri, instance.DefinitionDocument.Source.DecodedSha256,
                    instance.Definition.ObjectId);
                if (!definitions.TryGetValue(key, out var definition))
                {
                    SceneTransformValues transform = WorldTransformDeclarationReader.Read(instance.DefinitionDocument, instance.Definition);
                    SceneModelReference? model = null;
                    if (instance.Definition.ClassId == ModelWorldPqClass)
                    {
                        uint modelObjectId = Scalar(instance.Definition, 0x0B6E1578, instance.DefinitionDocument);
                        if (!models.TryGetValue(modelObjectId, out var matches) || matches.Count != 1)
                            throw new InvalidDataException($"Definition {instance.DefinitionDocument.Source.ResourceUri}/{instance.Definition.ObjectId:X8} property 0B6E1578 requires one typed dependency model {modelObjectId:X8}; found {matches?.Count ?? 0}.");
                        var match = matches[0];
                        if (objectCounts[match.Resource][modelObjectId] != 1)
                            throw new InvalidDataException($"Model object {modelObjectId:X8} is ambiguous in {match.Resource.Source.ResourceUri}.");
                        model = new(match.Resource, match.Object, Scalar(match.Object, 0x8AB68B3F, match.Resource));
                    }
                    definitions.Add(key, definition = (transform, model));
                }
                placements.Add(new(instance, WorldTransformDeclarationReader.Read(instance.InstanceDocument, instance.Object),
                    definition.Transform, definition.Model));
            }
            catch (InvalidDataException error)
            { throw new InvalidDataException($"Scene placements {context}: {error.Message}", error); }
        }
        return new(scene, placements.AsReadOnly());
    }

    private static DokProperty? Property(DokObject item, uint hash, LoadedObjects source, bool optional)
    {
        DokProperty[] matches = item.Properties.Where(property => property.NameHash == hash).ToArray();
        if (matches.Length == 0 && optional) return null;
        if (matches.Length != 1)
            throw Error(source, item, hash, "requires a unique property");
        return matches[0];
    }

    private static uint Scalar(DokObject item, uint hash, LoadedObjects source)
    {
        DokProperty property = Property(item, hash, source, optional: false)!;
        if (property.TypeCode != 5 || property.ElementCount != 1 || property.ElementSize != 4 || property.RawValue.Length != 4)
            throw Error(source, item, hash, "requires one type-5 scalar");
        return BinaryPrimitives.ReadUInt32LittleEndian(property.RawValue);
    }

    private static InvalidDataException Error(LoadedObjects source, DokObject item, uint hash, string detail) =>
        new($"{source.Source.ResourceUri} object {item.ObjectId:X8} property {hash:X8} {detail}.");
}
