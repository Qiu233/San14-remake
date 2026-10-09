using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public sealed record SceneDependency(int Index, uint RawValueBD56FCC7, uint RawValue1424AD27, LoadedObjects Resource);
public sealed record SceneInstance(int Index, DokObject Object, LoadedObjects InstanceDocument,
    DokObject Definition, LoadedObjects DefinitionDocument);
public sealed record LoadedSceneResources(LoadedObjects Scene, DokObject RootObject, DokObject InstanceListObject,
    IReadOnlyList<SceneDependency> Dependencies, LoadedObjects ModelCatalog,
    IReadOnlyList<SceneInstance> Instances, LoadedObjects TerrainBundle);

/// <summary>
/// Resolves the verified scene dependency and instance-reference graph on demand.
/// DOK attributes and dependency flags remain raw; instance/definition properties are
/// kept separate without guessing inheritance, placement transforms or edition selection.
/// No textures, meshes or height samples are loaded by this graph reader.
/// </summary>
public static class SceneResources
{
    private const uint RootClass = 0xE1D40600, InstanceListClass = 0x1CA58BA0, TerrainClass = 0x774DDC07;

    public static LoadedSceneResources Load(GameResourceRepository repository, string archive, uint sceneId)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var cache = new Dictionary<uint, LoadedObjects>();
        LoadedObjects scene = Read(sceneId, $"scene {GameResourceId.Rdb(archive, sceneId)}");
        string context = scene.Source.ResourceUri;
        DokObject[] roots = scene.Document.Objects.Where(item => item.Kind == DokObjectKind.Inline && item.ClassId == RootClass).ToArray();
        if (roots.Length != 1) throw Error(context, $"requires one root class {RootClass:X8}; found {roots.Length}");
        DokObject root = roots[0];
        uint[] dependencyIds = Values(root, 0x9A932BBA, context);
        uint[] flags = Values(root, 0xBD56FCC7, context), loadFlags = Values(root, 0x1424AD27, context);
        if (dependencyIds.Length == 0 || flags.Length != dependencyIds.Length || loadFlags.Length != dependencyIds.Length)
            throw Error(context, "dependency and raw flag arrays have incompatible lengths");
        uint listId = Scalar(root, 0xD3D01478, context);
        DokObject list = Unique(scene, listId, context);
        if (list.Kind != DokObjectKind.Inline || list.ClassId != InstanceListClass)
            throw Error(context, $"instance list {listId:X8} has an unsupported class");
        uint[] instanceIds = Values(list, 0xE21FF09F, context);
        var dependencies = new List<SceneDependency>(dependencyIds.Length);
        var seenDependencies = new HashSet<uint>();
        for (int i = 0; i < dependencyIds.Length; i++)
        {
            uint fileId = dependencyIds[i];
            if (fileId == sceneId || !seenDependencies.Add(fileId))
                throw Error(context, $"dependency {i} repeats file {fileId:X8} or references the scene itself");
            dependencies.Add(new(i, flags[i], loadFlags[i], Read(fileId, $"{context} dependency {i}")));
        }
        LoadedObjects[] terrains = dependencies.Select(item => item.Resource).Where(resource =>
            resource.Document.Objects.Any(item => item.Kind == DokObjectKind.Inline && item.ClassId == TerrainClass)).ToArray();
        if (terrains.Length != 1 || terrains[0].Document.Objects.Count(item => item.Kind == DokObjectKind.Inline && item.ClassId == TerrainClass) != 1)
            throw Error(context, "requires one dependency containing a unique terrain root");
        LoadedObjects catalog = Read(Scalar(root, 0x22E64208, context), $"{context} catalog property 22E64208");
        // An ID is resolved only within this scene's explicit dependency files.
        // Unrelated global objects cannot satisfy a missing or ambiguous instance.
        var requested = instanceIds.ToHashSet();
        var candidates = new Dictionary<uint, List<(DokObject Object, LoadedObjects Document)>>();
        foreach (SceneDependency dependency in dependencies)
            foreach (DokObject item in dependency.Resource.Document.Objects)
                if (requested.Contains(item.ObjectId))
                {
                    if (!candidates.TryGetValue(item.ObjectId, out var matches)) candidates[item.ObjectId] = matches = new();
                    matches.Add((item, dependency.Resource));
                }
        var instances = new List<SceneInstance>(instanceIds.Length);
        for (int i = 0; i < instanceIds.Length; i++)
        {
            uint id = instanceIds[i];
            if (!candidates.TryGetValue(id, out var matches) || matches.Count != 1)
                throw Error(context, $"instance {i} object {id:X8} is missing or ambiguous in the declared dependencies");
            var match = matches[0];
            if (match.Object.Kind != DokObjectKind.Reference || match.Object.Reference is not { } reference)
                throw Error(context, $"instance {i} object {id:X8} is not a supported reference object");
            string instanceContext = $"{context} instance {i} object {id:X8} in {match.Document.Source.ResourceUri}";
            LoadedObjects definitionDocument = Read(reference.ResourceId, instanceContext);
            DokObject definition = Unique(definitionDocument, reference.ObjectId, instanceContext);
            if (definition.Kind != DokObjectKind.Inline)
                throw Error(instanceContext, $"definition {reference.ObjectId:X8} in {definitionDocument.Source.ResourceUri} is not inline; reference inheritance is unresolved");
            instances.Add(new(i, match.Object, match.Document, definition, definitionDocument));
        }
        return new(scene, root, list, dependencies.AsReadOnly(), catalog, instances.AsReadOnly(), terrains[0]);

        LoadedObjects Read(uint fileId, string owner)
        {
            if (cache.TryGetValue(fileId, out LoadedObjects? found)) return found;
            string uri = GameResourceId.Rdb(archive, fileId).ToString();
            try { return cache[fileId] = repository.LoadObjects(archive, fileId); }
            catch (Exception error) when (error is KeyNotFoundException or InvalidDataException)
            { throw new InvalidDataException($"Scene reference {owner} failed to load {uri}: {error.Message}", error); }
        }
    }

    private static DokObject Unique(LoadedObjects source, uint id, string context)
    {
        DokObject[] matches = source.Document.Objects.Where(item => item.ObjectId == id).ToArray();
        if (matches.Length != 1) throw Error(context, $"object {id:X8} is missing or ambiguous in {source.Source.ResourceUri}");
        return matches[0];
    }
    private static uint Scalar(DokObject item, uint property, string context)
    {
        uint[] values = Values(item, property, context);
        if (values.Length != 1) throw Error(context, $"object {item.ObjectId:X8} property {property:X8} requires one scalar");
        return values[0];
    }
    private static uint[] Values(DokObject item, uint property, string context)
    {
        DokProperty[] matches = item.Properties.Where(value => value.NameHash == property).ToArray();
        if (matches.Length != 1 || matches[0].TypeCode != 5 || matches[0].ElementCount < 0 ||
            matches[0].RawValue.Length != (long)matches[0].ElementCount * 4)
            throw Error(context, $"object {item.ObjectId:X8} requires a unique type-5 property {property:X8}");
        var values = new uint[matches[0].ElementCount];
        for (int i = 0; i < values.Length; i++) values[i] = BinaryPrimitives.ReadUInt32LittleEndian(matches[0].RawValue.AsSpan(i * 4, 4));
        return values;
    }
    private static InvalidDataException Error(string context, string message) => new($"Scene {context}: {message}.");
}
