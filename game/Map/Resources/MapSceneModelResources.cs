using System.Buffers.Binary;
using System.Collections.ObjectModel;
using Godot;
using San14.Core;
using San14.Data;

/// <summary>Owns a scene's materials, building assets and city display history.
/// The view borrows displays after loading and releases its nodes before this owner.</summary>
internal sealed class MapSceneModelResources : IDisposable
{
    private const int GateAssetCategory = 55;
    private const int OpeningGateCount = 10;
    private readonly GameResourceRepository _repository;
    private readonly LoadedSceneResources _scene;
    private readonly ScenarioCityPreview? _scenario;
    private readonly string _locale;
    private readonly MapModelMeshCache _meshes;
    private readonly ModelPreviewMaterials _materials;
    private readonly Dictionary<uint, ModelMeshResult> _placementMeshes = [];
    private readonly Dictionary<string, FacilityModelDisplays> _facilities = [];
    private readonly IReadOnlyDictionary<string, FacilityModelDisplays> _facilityView;
    private FacilityModelDisplay? _gate;
    private CeCityVariantTable? _cityVariants;
    private LoadedCityOpeningPostLoadSource? _cityOpening;
    private CityVisualAdapter? _cities;

    public int TextureCount => _materials.TextureCount;
    public int PlacementMeshCount => _placementMeshes.Count;

    public MapSceneModelResources(GameResourceRepository repository, LoadedSceneResources scene,
        ScenarioCityPreview? scenario, string locale, TexturePreviewPolicy texturePolicy, MapModelMeshCache meshes)
    {
        (_repository, _scene, _scenario, _locale, _meshes) = (repository, scene, scenario, locale, meshes);
        _facilityView = new ReadOnlyDictionary<string, FacilityModelDisplays>(_facilities);
        _materials = new(repository, texturePolicy);
    }

    public FacilityModelDisplay LoadPlacement(SceneModelReference reference)
    {
        if (!_placementMeshes.TryGetValue(reference.ModelFileId, out var mesh))
            _placementMeshes.Add(reference.ModelFileId, mesh = LoadMesh(reference));
        return new(reference, mesh, _materials.Load(reference, mesh));
    }

    private ModelMeshResult LoadMesh(SceneModelReference reference) =>
        _meshes.Get(_repository.LoadModelTransforms("FieldEditor4", reference.ModelFileId));

    private FacilityModelDisplay LoadDisplay(SceneModelReference reference)
    {
        var mesh = LoadMesh(reference);
        return new(reference, mesh, _materials.Load(reference, mesh));
    }

    public IReadOnlyDictionary<string, FacilityModelDisplays>? ResolveFacilities(GameState world)
    {
        if (_scene.Scene.Source.FileId != CeFacilityModels.FieldSceneId || world.Structures is null ||
            !world.Structures.Values.Any(item => PlayableConstructionRules.IsSupportedKind(item.KindId)))
            return null;
        foreach (string kind in world.Structures.Values.Select(item => item.KindId)
            .Where(PlayableConstructionRules.IsSupportedKind).Distinct())
        {
            if (_facilities.ContainsKey(kind)) continue;
            var references = CeFacilityModels.Resolve(_repository, _scene,
                PlayableConstructionRules.Describe(kind).ObjectSlot);
            _facilities.Add(kind, new(LoadDisplay(references.Unfinished), LoadDisplay(references.Completed),
                _scene.Scene.Source.ResourceUri, _scene.Scene.Source.DecodedSha256,
                _scene.ModelCatalog.Source.ResourceUri, _scene.ModelCatalog.Source.DecodedSha256));
        }
        return _facilityView;
    }

    public CityVisualPlan? ResolveCities(GameState world, Func<GameState, SourceWorldContext?>? currentQueries)
    {
        if (_scene.Scene.Source.FileId != CeCityModels.FieldSceneId || _scenario?.Number != 1) return null;
        _cityVariants ??= CeCityVariantTable.LoadCurrent(_locale);
        _cities ??= new(_repository, _scene, _cityVariants, _materials);
        if (_cityOpening is null && world.NewGameOptions is { } options)
        {
            var metadata = _repository.EffectiveScenarios.Single(item =>
                item.SourcePath == _scenario.SourcePath && item.Locale == _scenario.Locale);
            _cityOpening = CityOpeningPostLoadSource.LoadSourceCreatedOrdinary(_repository,
                _repository.LoadScenario(metadata), _repository.LoadCeMap(), options, _cityVariants);
        }
        if (_cityOpening is { } opening)
        {
            var seed = new List<CityVisualSelection>();
            foreach (var record in opening.Cities.Values)
            {
                var state = new CityVisualState(record.Slot, record.HexId, record.SizeCode,
                    record.ForcePhysicalSlot == 0 ? null : record.ForcePhysicalSlot, record.Durability);
                var selected = opening.SelectSourceCreatedOpening(_cityVariants, state);
                if (selected.Selection is { } selection) seed.Add(selection);
            }
            _cities.SeedKnownSourceSelections(seed);
        }
        var damage = new Dictionary<int, CityVisualDamageInput>();
        if (currentQueries?.Invoke(world) is { } current)
        {
            foreach (var city in world.Cities.Values)
            {
                try
                {
                    byte tier = BasePostLoad.ReadFlag66(new(BaseKind.City, city.Id), current);
                    damage.Add(city.Id, CityVisualDamageInput.FromTier(tier,
                        new(CityVisualEvidence.CurrentDamageUri, world.MapSha256,
                            "actual-current-city-damage-tier")));
                }
                catch (InvalidDataException) { /* No current descriptor: retain the prior model explicitly. */ }
            }
        }
        return _cities.Resolve(world, null, damage.Count == 0 ? null : damage, retainLastKnownModel: true);
    }

    public IReadOnlyDictionary<int, GateModelDisplay>? ResolveGates(GameState world)
    {
        if (_scene.Scene.Source.FileId != CeFacilityModels.FieldSceneId || _scenario is null ||
            _scenario.Locale is not ("SC" or "TC") ||
            world.ScenarioKey != GameResourceId.File(_scenario.SourcePath).ToString()) return null;
        var gates = _scenario.Gates.Where(item => item.Slot != 0).OrderBy(item => item.Slot).ToArray();
        if (gates.Length != OpeningGateCount) throw new InvalidDataException("CE opening gate count differs from the verified source.");
        _gate ??= LoadDisplay(CeGateModels.ResolveByCategory(_repository, _scene, GateAssetCategory));
        string ruleVersion = CeHexWorldCoordinates.GetRuleVersion(_locale);
        var displays = new Dictionary<int, GateModelDisplay>();
        foreach (var source in gates)
        {
            if (!world.Gates.TryGetValue(source.LocationSlot, out var gate)) continue;
            var areas = _scenario.Areas.Where(item => item.Name == source.Name && item.HexId == source.HexId).ToArray();
            if (areas.Length != 1 || gate.HexId != source.HexId || gate.Name != source.Name)
                throw new InvalidDataException($"CE opening gate {source.Slot} has no unique source area.");
            var area = areas[0];
            byte[] raw = Convert.FromHexString(area.StateRemainingBytesHex);
            if (raw.Length != 4) throw new InvalidDataException($"CE opening gate {source.Slot} rotation row changed.");
            int degrees = BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(1, 2));
            if (degrees >= 360) throw new InvalidDataException($"CE opening gate {source.Slot} has an unsupported angle.");
            displays.Add(gate.Id, new(_gate, source.Slot, area.Slot, degrees, GateAssetCategory,
                new Quaternion(Vector3.Up, degrees * MathF.PI / 180f), true, ruleVersion,
                _scene.Scene.Source.ResourceUri, _scene.Scene.Source.DecodedSha256,
                _scene.ModelCatalog.Source.ResourceUri, _scene.ModelCatalog.Source.DecodedSha256));
        }
        return displays.Count == 0 ? null : displays;
    }

    // City geometry has its own history and must retire before scene materials.
    // The view calls this at its existing node-detachment boundary.
    public void ReleaseCityDisplays()
    {
        _cities?.Dispose();
        _cities = null;
        _cityOpening = null;
    }

    public void Dispose()
    {
        ReleaseCityDisplays();
        _materials.Dispose();
        _placementMeshes.Clear();
        _facilities.Clear();
        _gate = null;
        _cityVariants = null;
    }
}
