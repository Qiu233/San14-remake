namespace San14.Data;

/// <summary>Original geometry, raw local transforms and texture references for one category.</summary>
public sealed record CityVisualAsset(int Category, SceneModelReference Reference,
    LoadedModelTransforms Model, LoadedModelTextureResources TextureResources,
    string SceneUri, string SceneSha256, string CatalogUri, string CatalogSha256);

/// <summary>
/// A scene load owns this small on-demand cache. Reload the scene dependency
/// graph and recreate this catalog after changing overrides. Runtime state
/// changes do not re-import the same asset.
/// </summary>
public sealed class CityVisualAssetCatalog
{
    private readonly GameResourceRepository _repository;
    private readonly LoadedSceneResources _scene;
    private readonly CeCityVariantTable _variants;
    private readonly Dictionary<int, CityVisualAsset> _assets = [];
    public int LoadedCategoryCount => _assets.Count;

    public CityVisualAssetCatalog(GameResourceRepository repository,
        LoadedSceneResources scene, CeCityVariantTable variants)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(variants);
        if (scene.Scene.Source.FileId != CeCityModels.FieldSceneId)
            throw new InvalidDataException("City visual assets require the verified CE field scene.");
        (_repository, _scene, _variants) = (repository, scene, variants);
    }

    public CityVisualAsset Load(CityVisualSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (!selection.ImplementationVersion.Equals(_variants.ImplementationVersion, StringComparison.Ordinal) ||
            selection.Slot is < 1 or > 51 ||
            selection.Variant != _variants.VariantForCitySlot(selection.Slot) ||
            selection.Category != CeCityModels.CategoryFor(selection.Variant,
                selection.SizeCode, selection.DamageTier))
            throw new InvalidDataException("City model selection does not belong to the active source contract.");
        if (_assets.TryGetValue(selection.Category, out CityVisualAsset? cached)) return cached;
        LoadedModelTextureResources textures = CeCityModels.LoadTextureResourcesByCategory(
            _repository, _scene, selection.Category);
        LoadedModelTransforms model = _repository.LoadModelTransforms("FieldEditor4", textures.Model.Source.FileId);
        if (model.Source.ResourceUri != textures.Model.Source.ResourceUri ||
            model.Source.DecodedSha256 != textures.Model.Source.DecodedSha256)
            throw new InvalidDataException("City model source changed while its raw transforms were loading.");
        var asset = new CityVisualAsset(selection.Category,
            new(textures.Dictionary, textures.ModelObject, model.Source.FileId), model, textures,
            _scene.Scene.Source.ResourceUri, _scene.Scene.Source.DecodedSha256,
            _scene.ModelCatalog.Source.ResourceUri, _scene.ModelCatalog.Source.DecodedSha256);
        _assets.Add(selection.Category, asset);
        return asset;
    }

    /// <summary>Clears loaded models; the already loaded scene graph is retained.</summary>
    public void Invalidate() => _assets.Clear();
}
