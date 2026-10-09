using System.Collections.ObjectModel;
using Godot;
using San14.Core;
using San14.Data;

public sealed record OriginalCityVisualFailure(int CityId, string Reason, bool KeptPreviousModel);
public sealed record OriginalCityVisualPlan(IReadOnlyDictionary<int, CityModelDisplay> Displays,
    IReadOnlyDictionary<int, CityVisualSelection> Selections,
    IReadOnlyList<OriginalCityVisualFailure> Unresolved);

/// <summary>
/// Bridges current city state to original category assets. It owns no simulation
/// state or commands. Native renderers can consume AssetCatalog without Godot.
/// </summary>
public sealed class OriginalCityVisualAdapter : IDisposable
{
    private readonly OriginalCeCityVariantTable _variants;
    private readonly OriginalModelPreviewMaterials _materials;
    private readonly Dictionary<int, FacilityModelDisplay> _displays = [];
    private readonly Dictionary<int, (CityVisualSelection Selection, CityModelDisplay Display)> _previous = [];
    private readonly Dictionary<int, MeshInstance3D> _nodes = [];
    private bool _disposed;
    public CityVisualAssetCatalog AssetCatalog { get; }

    public OriginalCityVisualAdapter(OriginalGameRepository repository,
        LoadedSceneResources scene, OriginalCeCityVariantTable variants,
        OriginalModelPreviewMaterials materials)
    {
        ArgumentNullException.ThrowIfNull(materials);
        (_variants, _materials) = (variants, materials);
        AssetCatalog = new(repository, scene, variants);
    }

    public OriginalCityVisualPlan Resolve(GameState world, CityVisualOpeningEvidence? opening = null,
        IReadOnlyDictionary<int, CityVisualDamageInput>? currentDamage = null,
        bool retainLastKnownModel = true)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(world);
        var displays = new Dictionary<int, CityModelDisplay>();
        var selections = new Dictionary<int, CityVisualSelection>();
        var unresolved = new List<OriginalCityVisualFailure>();
        bool openingFrame = world.PeriodNumber == 0 && world.Date.Year == 184 &&
            world.Date.Month == 2 && world.Date.Day == 1;
        foreach (CityState city in world.Cities.Values.OrderBy(city => city.Id))
        {
            CityVisualDamageInput? input = null;
            currentDamage?.TryGetValue(city.Id, out input);
            CityVisualSelectionResult result = CityVisualSelector.Select(_variants,
                CityVisualState.FromCity(city), currentDamage is null ? null : input,
                opening, openingFrame);
            if (result.Selection is not CityVisualSelection selection)
            {
                _previous.TryGetValue(city.Id, out var previous);
                bool retained = retainLastKnownModel && city.HexId is int && city.Size is not null &&
                    previous.Display is not null;
                if (retained)
                {
                    // A stale descriptor is explicit. Retaining it prevents a
                    // model disappearing while current capacity is unresolved.
                    displays.Add(city.Id, previous.Display! with
                    {
                        SelectionBasis = "retained-previous-model-current-damage-unresolved",
                        CurrentCategoryObserved = false
                    });
                }
                unresolved.Add(new(city.Id, result.UnavailableReason!, retained));
                continue;
            }
            displays.Add(city.Id, LoadDisplay(selection));
            selections.Add(city.Id, selection);
        }
        // Publish history only after every on-demand asset load succeeded.
        foreach ((int slot, CityVisualSelection selection) in selections)
            _previous[slot] = (selection, displays[slot]);
        foreach (int absent in _previous.Keys.Where(slot => !world.Cities.ContainsKey(slot)).ToArray())
            _previous.Remove(absent);
        return new(new ReadOnlyDictionary<int, CityModelDisplay>(displays),
            new ReadOnlyDictionary<int, CityVisualSelection>(selections), unresolved.AsReadOnly());
    }

    /// <summary>Seed an explicitly known source display for a cold saved session.
    /// It only becomes a retained fallback, never a verified current descriptor.</summary>
    public void SeedKnownSourceSelections(IEnumerable<CityVisualSelection> sourceSelections)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        var pending=new Dictionary<int,(CityVisualSelection Selection,CityModelDisplay Display)>();
        foreach(CityVisualSelection source in sourceSelections)
        {
            if(_previous.ContainsKey(source.Slot))continue;
            if(source.ImplementationVersion!=_variants.ImplementationVersion||
               source.Variant!=_variants.VariantForCitySlot(source.Slot)||
               source.Category!=OriginalCeCityModels.CategoryFor(source.Variant,source.SizeCode,source.DamageTier))
                throw new InvalidDataException("Seeded city display differs from the selected source variant table.");
            var selection=source with{SelectionBasis="seeded-original-opening-display-descriptor",CurrentCategoryObserved=false};
            pending.Add(source.Slot,(selection,LoadDisplay(selection)));
        }
        foreach(var pair in pending)_previous.Add(pair.Key,pair.Value);
    }

    private CityModelDisplay LoadDisplay(CityVisualSelection selection)
    {
        CityVisualAsset asset=AssetCatalog.Load(selection);
        if(!_displays.TryGetValue(selection.Category,out FacilityModelDisplay? display))
        {
            OriginalModelMeshResult mesh=OriginalModelMesh.Build(asset.Model);
            try{display=new(asset.Reference,mesh,_materials.Load(asset.Reference,mesh));}
            catch{mesh.Mesh.Dispose();throw;}
            _displays.Add(selection.Category,display);
        }
        return new(display,selection.Category,selection.Variant,selection.DamageTier,selection.ImplementationVersion,
            asset.SceneUri,asset.SceneSha256,asset.CatalogUri,asset.CatalogSha256,selection.Evidence.Uri,selection.Evidence.Sha256)
        {SelectionBasis=selection.SelectionBasis,RuntimeCaptureSha256=selection.RuntimeCaptureSha256,
         CurrentCategoryObserved=selection.CurrentCategoryObserved};
    }

    /// <summary>
    /// Updates existing nodes without rebuilding mesh/texture data. Grounding and
    /// preview materials keep the same explicit limitations as the playable map.
    /// </summary>
    public void ApplyModels(Node3D container, MapWorldProjection projection,
        OriginalTerrainMeshResult terrain, GameState world, OriginalCityVisualPlan plan)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(container);
        if (world.MapWidth != projection.Policy.Width || world.MapHeight != projection.Policy.Height ||
            !world.MapSha256.Equals(projection.Policy.MapSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("City visual projection and playable map differ.");
        // Resolve all positions before mutating visible nodes.
        var positions = new Dictionary<int, Vector3>();
        foreach (int slot in plan.Displays.Keys)
        {
            if (!world.Cities.TryGetValue(slot, out CityState? city) || city.HexId is not int hex)
                throw new InvalidDataException($"City visual plan {slot} has no current world position.");
            ProjectedMapPoint point = projection.Project(hex);
            var position = new Vector3((float)point.X,
                OriginalMapGrounding.SampleHeight(terrain, (float)point.X, (float)point.Z), (float)point.Z);
            if (!position.IsFinite()) throw new InvalidDataException($"City {slot} has a nonfinite model position.");
            positions.Add(slot, position);
        }
        foreach (int absent in _nodes.Keys.Where(slot => !plan.Displays.ContainsKey(slot)).ToArray())
        {
            MeshInstance3D node = _nodes[absent];
            if (GodotObject.IsInstanceValid(node)) node.QueueFree();
            _nodes.Remove(absent);
        }
        foreach ((int slot, CityModelDisplay display) in plan.Displays)
        {
            if (!_nodes.TryGetValue(slot, out MeshInstance3D? node) || !GodotObject.IsInstanceValid(node))
            {
                node = new MeshInstance3D { Name = $"SourceDynamicCityModel_{slot}" };
                container.AddChild(node);
                _nodes[slot] = node;
            }
            else if (node.GetParent() != container)
                throw new InvalidOperationException("City visual adapter cannot span multiple scene containers.");
            node.Mesh = display.Assets.Mesh.Mesh;
            for (int surface = 0; surface < display.Assets.Materials.Materials.Count; surface++)
                node.SetSurfaceOverrideMaterial(surface, display.Assets.Materials.Materials[surface]);
            node.Position = positions[slot];
            node.SetMeta("city_id", slot);
            node.SetMeta("force_id", world.Cities[slot].ForceId ?? 0);
            node.SetMeta("source_city_category", display.Category);
            node.SetMeta("source_city_variant", display.Variant);
            node.SetMeta("source_city_damage_tier", display.DamageTier);
            node.SetMeta("city_category_selection_basis", display.SelectionBasis);
            node.SetMeta("original_city_current_category_observed", display.CurrentCategoryObserved);
            node.SetMeta("city_variant_rule_version", display.ImplementationVersion);
            node.SetMeta("scene_resource_uri", display.SceneUri);
            node.SetMeta("scene_decoded_sha256", display.SceneSha256);
            node.SetMeta("catalog_resource_uri", display.CatalogUri);
            node.SetMeta("catalog_decoded_sha256", display.CatalogSha256);
            node.SetMeta("model_resource_uri", display.Assets.Mesh.Source.ResourceUri);
            node.SetMeta("model_decoded_sha256", display.Assets.Mesh.Source.DecodedSha256);
            node.SetMeta("dictionary_resource_uri", display.Assets.Reference.Dictionary.Source.ResourceUri);
            node.SetMeta("dictionary_decoded_sha256", display.Assets.Reference.Dictionary.Source.DecodedSha256);
            node.SetMeta("source_model_object_id", display.Assets.Reference.ModelObject.ObjectId);
            node.SetMeta("source_materials", display.Assets.Materials.Sources);
            node.SetMeta("city_visual_evidence_uri", display.EvidenceUri);
            node.SetMeta("city_visual_evidence_sha256", display.EvidenceSha256);
            if (display.RuntimeCaptureSha256 is string capture)
                node.SetMeta("opening_runtime_capture_sha256", capture);
            else if (node.HasMeta("opening_runtime_capture_sha256"))
                node.RemoveMeta("opening_runtime_capture_sha256");
            node.SetMeta("original_city_ground_height_verified", false);
            node.SetMeta("original_city_runtime_rotation_verified", false);
            node.SetMeta("original_city_seasonal_appearance_verified", false);
            node.SetMeta("original_city_transform_and_material_verified", false);
        }
    }

    /// <summary>
    /// Clears nodes and state history. Recreate the adapter and material owner
    /// when changing scene, locale or overrides, so their source caches are fresh.
    /// </summary>
    public void Reset()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ReleaseOwnedResources();
    }

    private void ReleaseOwnedResources()
    {
        foreach (MeshInstance3D node in _nodes.Values)
            if (GodotObject.IsInstanceValid(node)) node.QueueFree();
        _nodes.Clear();
        _previous.Clear();
        foreach (FacilityModelDisplay display in _displays.Values)
            display.Mesh.Mesh.Dispose();
        _displays.Clear();
        AssetCatalog.Invalidate();
    }

    /// <summary>Release external nodes using a plan before disposing this owner.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        ReleaseOwnedResources();
        _disposed = true;
    }
}
