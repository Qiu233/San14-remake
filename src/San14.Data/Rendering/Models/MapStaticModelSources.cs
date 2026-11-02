using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

public sealed record MapStaticModelAsset(string CategoryIdentity, SceneModelReference Reference,
    LoadedModelTransforms RawTransforms, LoadedModelTextureResources TextureResources,
    IReadOnlyList<G1DrawRecord> Draws, Vector3 LocalMinimum, Vector3 LocalMaximum,
    string SceneUri, string SceneSha256)
{
    public D3D11SdfModelRegistration? SdfRegistration { get; init; }
    public string SourceKey => $"{CategoryIdentity}|{Reference.Dictionary.Source.ResourceUri}|{Reference.Dictionary.Source.DecodedSha256}|{Reference.ModelObject.ObjectId:X8}|{RawTransforms.Source.ResourceUri}|{RawTransforms.Source.DecodedSha256}";
}

/// <summary>Scene-owned source assets. Dynamic category changes reuse geometry,
/// raw local transforms and texture references. Recreate after a scene/override
/// reload; no Godot mesh, grounding, image conversion or frame state is stored.</summary>
public sealed class MapStaticModelAssetCatalog
{
    private readonly GameResourceRepository _repository;
    private readonly D3D11SdfModelDeclarationPreparation? _sdfDeclarations;
    public LoadedSceneResources Scene { get; }
    private readonly Dictionary<string, MapStaticModelAsset> _assets = new(StringComparer.Ordinal);
    public int LoadedAssetCount => _assets.Count;
    public GameResourceRepository Repository => _repository;

    public MapStaticModelAssetCatalog(GameResourceRepository repository, LoadedSceneResources scene,
        D3D11SdfModelDeclarationPreparation? sdfDeclarations = null)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(scene);
        (_repository, Scene, _sdfDeclarations) = (repository, scene, sdfDeclarations);
    }

    /// <summary>Borrow immutable source assets while creating independent
    /// registration and lookup lifetimes for this actual map opening.</summary>
    internal static MapStaticModelAssetCatalog CreateFromSources(GameResourceRepository repository,
        D3D11ModelInputSources sources, D3D11SdfModelDeclarationPreparation sdfDeclarations)
    {
        ArgumentNullException.ThrowIfNull(sources); ArgumentNullException.ThrowIfNull(sdfDeclarations);
        var catalog = new MapStaticModelAssetCatalog(repository, sources.Scene, sdfDeclarations);
        foreach (var source in sources.Assets)
        {
            if (source.SceneUri != sources.Scene.Scene.Source.ResourceUri ||
                source.SceneSha256 != sources.Scene.Scene.Source.DecodedSha256 || source.SdfRegistration is not null)
                throw new InvalidDataException("Field model source asset has another scene or registration lifetime.");
            catalog._assets.Add(source.CategoryIdentity, source with
            { SdfRegistration = sdfDeclarations.RegisterFreshOwnedModel(source.Reference) });
        }
        return catalog;
    }

    public MapStaticModelAsset LoadCity(CityVisualAssetCatalog catalog, CityVisualSelection selection)
    {
        ArgumentNullException.ThrowIfNull(catalog); ArgumentNullException.ThrowIfNull(selection);
        string key = "city-category:" + selection.Category;
        CityVisualAsset city = catalog.Load(selection);
        if (city.SceneUri != Scene.Scene.Source.ResourceUri || city.SceneSha256 != Scene.Scene.Source.DecodedSha256)
            throw new InvalidDataException("Native city category asset belongs to a different scene revision.");
        if (_assets.TryGetValue(key, out var cached)) return cached;
        return Add(key, city.Reference, city.Model, city.TextureResources);
    }

    /// <summary>Prepare a declared city category without inventing a current
    /// City selection or a campaign owner. Identity is shared with LoadCity.</summary>
    public MapStaticModelAsset LoadCityCategory(int category)
    {
        string key = "city-category:" + category;
        if (_assets.TryGetValue(key, out var cached)) return cached;
        var resources = CeCityModels.LoadTextureResourcesByCategory(_repository, Scene, category);
        return Add(key, new(resources.Dictionary, resources.ModelObject, resources.Model.Source.FileId),
            LoadRaw(resources), resources);
    }

    /// <summary>Import the actual port/prefecture definition and preserve its
    /// WorldPQ material and transform fields in the shared category asset.</summary>
    public MapStaticModelAsset LoadCapitalCategory(int category)
    {
        uint definition = category switch
        {
            59 => 0x17464C92u,
            60 => 0x13D7C146u,
            _ => throw new ArgumentOutOfRangeException(nameof(category))
        };
        string key = "capital-category:" + category;
        if (_assets.TryGetValue(key, out var cached)) return cached;
        var dictionary = Scene.Dependencies.Single(d => d.Resource.Source.FileId == 0xE755DAEE).Resource;
        var resources = ModelTextureResources.LoadForDefinition(_repository, dictionary, Scene.ModelCatalog, definition);
        return Add(key, new(dictionary, resources.ModelObject, resources.Model.Source.FileId), LoadRaw(resources), resources);
    }

    public MapStaticModelAsset LoadSceneInstance(int instanceIndex)
    {
        if ((uint)instanceIndex >= Scene.Instances.Count) throw new ArgumentOutOfRangeException(nameof(instanceIndex));
        return LoadSceneInstance(Scene.Instances[instanceIndex]);
    }

    /// <summary>Same scene-owned definition cache for an actual current Root
    /// child, including children outside the separate VisibilitySet.</summary>
    public MapStaticModelAsset LoadSceneInstance(SceneInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        // Asset identity belongs to the definition, so repeated source instances
        // share every immutable source load while retaining separate placements.
        string key = $"scene-definition:{instance.DefinitionDocument.Source.ResourceUri}/{instance.DefinitionDocument.Source.DecodedSha256}/{instance.Definition.ObjectId:X8}";
        if (_assets.TryGetValue(key, out var cached)) return cached;
        LoadedModelTextureResources textures = ModelTextureResources.LoadForInstance(_repository, Scene, instance);
        return Add(key, new(textures.Dictionary, textures.ModelObject, textures.Model.Source.FileId),
            LoadRaw(textures), textures);
    }

    /// <summary>Actual kind2 Scenery definition and its declared dependency.
    /// Retain the catalog definition for the real ModelWorld constructor;
    /// loading only a model reference would lose that source state.</summary>
    public MapStaticModelAsset LoadSceneryCategory(int category)
    {
        var rule=SceneryModelResourceRules.All.Single(r=>r.Category==category);
        string key="scenery-category:"+category;
        if(_assets.TryGetValue(key,out var cached))return cached;
        var reference=SceneryModelResourceRules.Resolve(Scene,rule);
        var resources=ModelTextureResources.LoadForDefinition(_repository,reference.Dictionary,Scene.ModelCatalog,rule.DefinitionId);
        return Add(key,reference,LoadRaw(resources),resources);
    }

    public MapStaticModelAsset LoadReference(SceneModelReference reference, string categoryIdentity)
    {
        ArgumentNullException.ThrowIfNull(reference); ArgumentException.ThrowIfNullOrWhiteSpace(categoryIdentity);
        if (!Scene.Dependencies.Any(d => d.Resource.Source.ResourceUri == reference.Dictionary.Source.ResourceUri &&
                d.Resource.Source.DecodedSha256 == reference.Dictionary.Source.DecodedSha256))
            throw new InvalidDataException("Native map model dictionary is outside the scene's declared dependencies.");
        string key = $"reference:{categoryIdentity}:{reference.Dictionary.Source.ResourceUri}/{reference.Dictionary.Source.DecodedSha256}/{reference.ModelObject.ObjectId:X8}/{reference.ModelFileId:X8}";
        if (_assets.TryGetValue(key, out var cached)) return cached;
        LoadedModelTextureResources textures = ModelTextureResources.Load(_repository, reference.Dictionary,
            reference.ModelObject.ObjectId);
        if (textures.Model.Source.FileId != reference.ModelFileId)
            throw new InvalidDataException("Native map reference FileId differs from its original dictionary model.");
        return Add(key, reference, LoadRaw(textures), textures);
    }

    public MapStaticModelAsset LoadGate(int category)
    {
        string key = "gate-category:" + category;
        if (_assets.TryGetValue(key, out var cached)) return cached;
        var resources = CeGateModels.LoadTextureResourcesByCategory(_repository, Scene, category);
        var reference = new SceneModelReference(resources.Dictionary, resources.ModelObject, resources.Model.Source.FileId);
        return Add(key, reference, LoadRaw(resources), resources);
    }

    public MapStaticModelAsset LoadFacility(int objectDefinitionSlot, bool completed)
    {
        string key = $"facility-slot:{objectDefinitionSlot}/state:{(completed ? "completed" : "unfinished")}";
        if (_assets.TryGetValue(key, out var cached)) return cached;
        var resources = CeFacilityModels.LoadTextureResources(_repository, Scene, objectDefinitionSlot, completed);
        return Add(key, new(resources.Dictionary, resources.ModelObject, resources.Model.Source.FileId),
            LoadRaw(resources), resources);
    }

    public MapStaticModelAsset LoadFacilityCategory(int category)
    {
        string key = "facility-category:" + category;
        if (_assets.TryGetValue(key, out var cached)) return cached;
        var resources = CeFacilityModels.LoadTextureResourcesByCategory(_repository, Scene, category);
        return Add(key, new(resources.Dictionary, resources.ModelObject, resources.Model.Source.FileId),
            LoadRaw(resources), resources);
    }

    public void Invalidate() => _assets.Clear();

    private static LoadedModelTransforms LoadRaw(LoadedModelTextureResources textures)
        => new(textures.Model.Geometry,
            G1ModelTransformReader.Read(textures.Model.Source.Bytes, textures.Model.Geometry), textures.Model.Source);

    private MapStaticModelAsset Add(string key, SceneModelReference reference,
        LoadedModelTransforms raw, LoadedModelTextureResources textures)
    {
        G1ModelGeometry geometry = raw.Geometry;
        G1DrawRecord[] draws = G1ModelDrawReader.Read(geometry).Draws.ToArray();
        if (draws.Length == 0) throw new InvalidDataException("Native map model contains no source draws.");
        ReadOnlySpan<byte> header = geometry.RawGeometryHeader;
        var minimum = new Vector3(BinaryPrimitives.ReadSingleLittleEndian(header[20..]),
            BinaryPrimitives.ReadSingleLittleEndian(header[24..]), BinaryPrimitives.ReadSingleLittleEndian(header[28..]));
        var maximum = new Vector3(BinaryPrimitives.ReadSingleLittleEndian(header[32..]),
            BinaryPrimitives.ReadSingleLittleEndian(header[36..]), BinaryPrimitives.ReadSingleLittleEndian(header[40..]));
        MapStaticModelTransforms.ValidateBounds(minimum, maximum);
        var asset = new MapStaticModelAsset(key, reference, raw, textures, Array.AsReadOnly(draws),
            minimum, maximum, Scene.Scene.Source.ResourceUri, Scene.Scene.Source.DecodedSha256)
        { SdfRegistration = _sdfDeclarations?.RegisterFreshOwnedModel(reference) };
        _assets.Add(key, asset); return asset;
    }
}
