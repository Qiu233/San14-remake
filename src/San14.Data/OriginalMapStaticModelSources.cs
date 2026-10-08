using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalMapStaticModelAsset(string CategoryIdentity, SceneModelReference Reference,
    LoadedModelTransforms RawTransforms, LoadedModelTextureResources TextureResources,
    IReadOnlyList<G1DrawRecord> Draws, Vector3 LocalMinimum, Vector3 LocalMaximum,
    string SceneUri, string SceneSha256)
{
    public string SourceKey => $"{CategoryIdentity}|{Reference.Dictionary.Source.ResourceUri}|{Reference.Dictionary.Source.DecodedSha256}|{Reference.ModelObject.ObjectId:X8}|{RawTransforms.Source.ResourceUri}|{RawTransforms.Source.DecodedSha256}";
}

/// <summary>Scene-owned source assets. Dynamic category changes reuse geometry,
/// raw local transforms and texture references. Recreate after a scene/override
/// reload; no Godot mesh, grounding, image conversion or frame state is stored.</summary>
public sealed class OriginalMapStaticModelAssetCatalog
{
    private readonly OriginalGameRepository _repository;
    public LoadedSceneResources Scene { get; }
    private readonly Dictionary<string, OriginalMapStaticModelAsset> _assets = new(StringComparer.Ordinal);
    public int LoadedAssetCount => _assets.Count;
    public OriginalGameRepository Repository => _repository;

    public OriginalMapStaticModelAssetCatalog(OriginalGameRepository repository, LoadedSceneResources scene)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(scene);
        (_repository, Scene) = (repository, scene);
    }

    public OriginalMapStaticModelAsset LoadCity(CityVisualAssetCatalog catalog, CityVisualSelection selection)
    {
        ArgumentNullException.ThrowIfNull(catalog); ArgumentNullException.ThrowIfNull(selection);
        string key = "city-category:" + selection.Category;
        CityVisualAsset city = catalog.Load(selection);
        if (city.SceneUri != Scene.Scene.Source.ResourceUri || city.SceneSha256 != Scene.Scene.Source.DecodedSha256)
            throw new InvalidDataException("Native city category asset belongs to a different scene revision.");
        if (_assets.TryGetValue(key, out var cached)) return cached;
        return Add(key, city.Reference, city.Model, city.TextureResources);
    }

    public OriginalMapStaticModelAsset LoadSceneInstance(int instanceIndex)
    {
        if ((uint)instanceIndex >= Scene.Instances.Count) throw new ArgumentOutOfRangeException(nameof(instanceIndex));
        SceneInstance instance = Scene.Instances[instanceIndex];
        // Asset identity belongs to the definition, so repeated source instances
        // share every immutable source load while retaining separate placements.
        string key = $"scene-definition:{instance.DefinitionDocument.Source.ResourceUri}/{instance.DefinitionDocument.Source.DecodedSha256}/{instance.Definition.ObjectId:X8}";
        if (_assets.TryGetValue(key, out var cached)) return cached;
        LoadedModelTextureResources textures = OriginalModelTextureResources.LoadForInstance(_repository, Scene, instanceIndex);
        return Add(key, new(textures.Dictionary, textures.ModelObject, textures.Model.Source.FileId),
            LoadRaw(textures), textures);
    }

    public OriginalMapStaticModelAsset LoadReference(SceneModelReference reference, string categoryIdentity)
    {
        ArgumentNullException.ThrowIfNull(reference); ArgumentException.ThrowIfNullOrWhiteSpace(categoryIdentity);
        if (!Scene.Dependencies.Any(d => d.Resource.Source.ResourceUri == reference.Dictionary.Source.ResourceUri &&
                d.Resource.Source.DecodedSha256 == reference.Dictionary.Source.DecodedSha256))
            throw new InvalidDataException("Native map model dictionary is outside the scene's declared dependencies.");
        string key = $"reference:{categoryIdentity}:{reference.Dictionary.Source.ResourceUri}/{reference.Dictionary.Source.DecodedSha256}/{reference.ModelObject.ObjectId:X8}/{reference.ModelFileId:X8}";
        if (_assets.TryGetValue(key, out var cached)) return cached;
        LoadedModelTextureResources textures = OriginalModelTextureResources.Load(_repository, reference.Dictionary,
            reference.ModelObject.ObjectId);
        if (textures.Model.Source.FileId != reference.ModelFileId)
            throw new InvalidDataException("Native map reference FileId differs from its original dictionary model.");
        return Add(key, reference, LoadRaw(textures), textures);
    }

    public OriginalMapStaticModelAsset LoadGate(int category)
        => LoadCatalogReference(OriginalCeGateModels.ResolveByCategory(_repository, Scene, category), "gate-category:" + category);

    public OriginalMapStaticModelAsset LoadFacility(int objectDefinitionSlot, bool completed)
    {
        var pair = OriginalCeFacilityModels.Resolve(_repository, Scene, objectDefinitionSlot);
        return LoadCatalogReference(completed ? pair.Completed : pair.Unfinished,
            $"facility-slot:{objectDefinitionSlot}/state:{(completed ? "completed" : "unfinished")}");
    }

    private OriginalMapStaticModelAsset LoadCatalogReference(SceneModelReference reference, string categoryIdentity)
    {
        string key = $"catalog-reference:{categoryIdentity}:{reference.Dictionary.Source.ResourceUri}/{reference.Dictionary.Source.DecodedSha256}/{reference.ModelObject.ObjectId:X8}/{reference.ModelFileId:X8}";
        if (_assets.TryGetValue(key, out var cached)) return cached;
        var definitions = Scene.ModelCatalog.Document.Objects.Where(obj => obj.Kind == DokObjectKind.Inline &&
            obj.ClassId == OriginalScenePlacements.ModelWorldPqClass && obj.Properties.Any(p => p.NameHash == 0x0B6E1578 &&
                p.TypeCode == 5 && p.ElementCount == 1 && p.RawValue.Length == 4 &&
                BinaryPrimitives.ReadUInt32LittleEndian(p.RawValue) == reference.ModelObject.ObjectId)).ToArray();
        if (definitions.Length != 1)
            throw new InvalidDataException("Native gate/facility category has no unique original WorldPQ catalog definition.");
        var textures = OriginalModelTextureResources.LoadForDefinition(_repository, reference.Dictionary,
            Scene.ModelCatalog, definitions[0].ObjectId);
        if (textures.Model.Source.FileId != reference.ModelFileId)
            throw new InvalidDataException("Native catalog category differs from its scene model reference.");
        return Add(key, reference, LoadRaw(textures), textures);
    }

    public void Invalidate() => _assets.Clear();

    private LoadedModelTransforms LoadRaw(LoadedModelTextureResources textures)
    {
        LoadedModelTransforms raw = _repository.LoadModelTransforms("FieldEditor4", textures.Model.Source.FileId);
        if (raw.Source.ResourceUri != textures.Model.Source.ResourceUri ||
            raw.Source.DecodedSha256 != textures.Model.Source.DecodedSha256)
            throw new InvalidDataException("Native map model changed while loading its raw transforms.");
        return raw;
    }

    private OriginalMapStaticModelAsset Add(string key, SceneModelReference reference,
        LoadedModelTransforms raw, LoadedModelTextureResources textures)
    {
        if (!Convert.ToHexString(SHA256.HashData(raw.Source.Bytes)).Equals(raw.Source.DecodedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Native map model source hash changed.");
        var geometry = G1ModelGeometryReader.Read(raw.Source.Bytes, raw.Source.Bytes.LongLength);
        var transforms = G1ModelTransformReader.Read(raw.Source.Bytes, geometry);
        // Parsed input records may have mutable arrays; use the canonical source
        // payload for cache publication instead of trusting a stale parse.
        raw = new(geometry, transforms, raw.Source);
        G1DrawRecord[] draws = G1ModelDrawReader.Read(geometry).Draws.ToArray();
        if (draws.Length == 0) throw new InvalidDataException("Native map model contains no source draws.");
        ReadOnlySpan<byte> header = geometry.RawGeometryHeader;
        var minimum = new Vector3(BinaryPrimitives.ReadSingleLittleEndian(header[20..]),
            BinaryPrimitives.ReadSingleLittleEndian(header[24..]), BinaryPrimitives.ReadSingleLittleEndian(header[28..]));
        var maximum = new Vector3(BinaryPrimitives.ReadSingleLittleEndian(header[32..]),
            BinaryPrimitives.ReadSingleLittleEndian(header[36..]), BinaryPrimitives.ReadSingleLittleEndian(header[40..]));
        OriginalMapStaticModelTransforms.ValidateBounds(minimum, maximum);
        var asset = new OriginalMapStaticModelAsset(key, reference, raw, textures, Array.AsReadOnly(draws),
            minimum, maximum, Scene.Scene.Source.ResourceUri, Scene.Scene.Source.DecodedSha256);
        _assets.Add(key, asset); return asset;
    }
}
