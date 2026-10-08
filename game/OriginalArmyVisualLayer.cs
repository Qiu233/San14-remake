using Godot;
using San14.Core;
using San14.Data;

/// <summary>Explicit source category and caller pose for one model component.
/// Core formation IDs, missions and troop counts do not supply these values.</summary>
public sealed record OriginalArmyVisualBinding(int Category, Quaternion Rotation,
    Vector3 Scale, string SelectionSource);

/// <summary>A caller-owned stable component key and explicit world-space
/// offset from this unit's grounded map anchor. Native descriptor/instance
/// identities are optional provenance, never fabricated from list order.</summary>
public sealed record OriginalArmyComponentVisualBinding(string ComponentId,
    OriginalArmyVisualBinding Model, Vector3 WorldPositionOffset)
{
    public int? SourceDescriptorOffset { get; init; }
    public int? SourceInstanceIndex { get; init; }
}

public sealed record OriginalArmyVisualStatus(int UnitId, bool HasSourceModel,
    int? Category, string Reason)
{
    public string? ComponentId { get; init; }
}

/// <summary>
/// Owns on-demand original Army meshes and texture previews, separate from unit
/// signs and simulation state. Unknown model selection keeps the existing sign.
/// A resolved category identifies one model component; original formation layout,
/// world pose, native material execution and animation are not implied by it.
/// Recreate the owner to reload resource overrides.
/// </summary>
public partial class OriginalArmyVisualLayer : Node3D
{
    private sealed record Display(LoadedOriginalArmyModel Source,
        OriginalModelMeshResult Mesh, OriginalPreviewMaterialSet Materials);
    private sealed record Entry(MeshInstance3D Node, int Category);
    private readonly record struct ComponentKey(int UnitId,string ComponentId);
    private readonly Dictionary<int, Display> _displays = [];
    private readonly Dictionary<int, string> _unsupported = [];
    private readonly Dictionary<ComponentKey, Entry> _units = [];
    private OriginalGameRepository? _repository;
    private LoadedOriginalArmyCatalog? _catalog;
    private OriginalModelPreviewMaterials? _materials;
    private bool _disposed;
    public int ModelCount => _units.Count;
    public int LoadedCategoryCount => _displays.Count;
    public IReadOnlyList<OriginalArmyVisualStatus> LastStatuses { get; private set; } = [];

    public void Initialize(OriginalGameRepository repository, OriginalTexturePreviewPolicy policy)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(policy);
        if (_repository is not null) throw new InvalidOperationException("Army visual layer is already initialized.");
        _repository = repository;
        _materials = new OriginalModelPreviewMaterials(repository, policy);
        Name = "OriginalArmyVisuals";
        SetMeta("loads_original_assets_on_demand", true);
        SetMeta("original_army_complete_assembly_verified", false);
        SetMeta("original_animation_verified", false);
    }

    /// <summary>Refresh create, retirement, position and explicit orientation from
    /// one complete state. No Army payload is loaded for unbound units.</summary>
    public IReadOnlyList<OriginalArmyVisualStatus> UpdateUnits(MapWorldProjection projection,
        OriginalTerrainMeshResult terrain, GameState world,
        IReadOnlyDictionary<int, OriginalArmyVisualBinding>? bindings = null)
        => UpdateComponents(projection,terrain,world,bindings?.ToDictionary(item=>item.Key,
            item=>(IReadOnlyList<OriginalArmyComponentVisualBinding>)new[] {
                new OriginalArmyComponentVisualBinding("preview",item.Value,Vector3.Zero) }));

    /// <summary>Reconciles explicit independent components for the complete
    /// unit state. Shared original meshes/materials are reused, while stable
    /// component IDs control individual instance lifetimes. This does not lay
    /// out an army from descriptor counts or convert troop numbers to instances.</summary>
    public IReadOnlyList<OriginalArmyVisualStatus> UpdateComponents(MapWorldProjection projection,
        OriginalTerrainMeshResult terrain,GameState world,
        IReadOnlyDictionary<int,IReadOnlyList<OriginalArmyComponentVisualBinding>>? bindings = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_repository is null || _materials is null) throw new InvalidOperationException("Initialize the Army visual layer first.");
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(world);
        if (world.MapWidth != projection.Policy.Width || world.MapHeight != projection.Policy.Height ||
            !world.MapSha256.Equals(projection.Policy.MapSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Army state and world projection sources differ.");
        if (world.Units.Any(item => item.Key != item.Value.Id || item.Key < 0 ||
            (uint)item.Value.HexId >= (long)world.MapWidth * world.MapHeight))
            throw new InvalidDataException("Army visual state has a mismatched identity or invalid Hex.");
        if (bindings is not null && bindings.Keys.Any(id => !world.Units.ContainsKey(id)))
            throw new InvalidDataException("Army visual binding references a unit absent from this state.");
        // Validate all caller poses before retiring or changing any current node.
        if (bindings is not null) foreach (var components in bindings.Values)
        {
            if (components is null) throw new InvalidDataException("Army component binding list is unknown.");
            var keys=new HashSet<string>(StringComparer.Ordinal);
            foreach (var component in components)
            {
                if (component is null || string.IsNullOrWhiteSpace(component.ComponentId) || !keys.Add(component.ComponentId) ||
                    !component.WorldPositionOffset.IsFinite() || component.SourceDescriptorOffset is < 0 || component.SourceInstanceIndex is < 0)
                    throw new InvalidDataException("Army components require unique stable IDs, explicit finite offsets and nonnegative optional source identities.");
                ValidateBinding(component.Model);
            }
        }
        var statuses = new List<OriginalArmyVisualStatus>(world.Units.Count);
        var keep = new HashSet<ComponentKey>();
        foreach (UnitState unit in world.Units.Values.OrderBy(item => item.Id))
        {
            if (bindings is null || !bindings.TryGetValue(unit.Id, out var components) || components.Count==0)
            {
                statuses.Add(new(unit.Id, false, null,
                    "Native Army descriptor selection is unavailable; the unit sign remains visible."));
                continue;
            }
            ProjectedMapPoint point = projection.Project(unit.HexId);
            float ground = OriginalMapGrounding.SampleHeight(terrain, (float)point.X, (float)point.Z);
            var anchor = new Vector3((float)point.X, ground, (float)point.Z);
            if (!anchor.IsFinite()) throw new InvalidDataException($"Army {unit.Id} has a non-finite projected position.");
            foreach (var component in components)
            {
                OriginalArmyVisualBinding binding=component.Model;
                ComponentKey key=new(unit.Id,component.ComponentId);
                Display? display = GetDisplay(binding.Category, out string? unavailable);
                if (display is null)
                {
                    statuses.Add(new(unit.Id, false, binding.Category, unavailable!) {ComponentId=component.ComponentId});
                    continue;
                }
                var position=anchor+component.WorldPositionOffset;
                if (!position.IsFinite()) throw new InvalidDataException($"Army {unit.Id} has a non-finite projected position.");
                if (!_units.TryGetValue(key, out Entry? entry) || entry.Category != binding.Category)
                {
                    RemoveComponent(key);
                    var node = new MeshInstance3D { Name = $"OriginalArmyComponent_{unit.Id}_{_units.Count}", Mesh = display.Mesh.Mesh };
                    for (int i = 0; i < display.Materials.Materials.Count; i++)
                        node.SetSurfaceOverrideMaterial(i, display.Materials.Materials[i]);
                    SetSources(node, display);
                    AddChild(node);
                    entry = new(node, binding.Category);
                    _units.Add(key, entry);
                }
                entry.Node.Transform = new Transform3D(new Basis(binding.Rotation).Scaled(binding.Scale), position);
                entry.Node.SetMeta("unit_id", unit.Id);
                entry.Node.SetMeta("hex_id", unit.HexId);
                entry.Node.SetMeta("force_id", unit.ForceId);
                entry.Node.SetMeta("officer_id", unit.OfficerId);
                entry.Node.SetMeta("troops", unit.Troops);
                entry.Node.SetMeta("core_formation_id", unit.FormationId ?? -1);
                entry.Node.SetMeta("core_mission_kind", unit.Mission?.Kind.ToString() ?? "unknown");
                entry.Node.SetMeta("selection_source", binding.SelectionSource);
                entry.Node.SetMeta("component_id",component.ComponentId);
                entry.Node.SetMeta("source_descriptor_offset",component.SourceDescriptorOffset??-1);
                entry.Node.SetMeta("source_instance_index",component.SourceInstanceIndex??-1);
                entry.Node.SetMeta("explicit_world_position_offset",component.WorldPositionOffset);
                entry.Node.SetMeta("original_selection_from_core_verified", false);
                entry.Node.SetMeta("original_world_pose_verified", false);
                entry.Node.SetMeta("original_animation_verified", false);
                keep.Add(key);
                statuses.Add(new(unit.Id, true, binding.Category,
                    "Original model component and textures loaded; pose and material execution are explicit previews.")
                    {ComponentId=component.ComponentId});
            }
        }
        foreach (var key in _units.Keys.Where(key => !keep.Contains(key)).ToArray()) RemoveComponent(key);
        LastStatuses = statuses.AsReadOnly();
        SetMeta("source_model_component_count", _units.Count);
        SetMeta("source_unit_with_model_count",_units.Keys.Select(key=>key.UnitId).Distinct().Count());
        SetMeta("unresolved_unit_count", statuses.Where(status=>!status.HasSourceModel).Select(status=>status.UnitId).Distinct().Count());
        SetMeta("unresolved_component_count",statuses.Count(status=>!status.HasSourceModel));
        return LastStatuses;
    }

    public MeshInstance3D? FindUnitModel(int unitId) => FindComponentModel(unitId,"preview");
    public MeshInstance3D? FindComponentModel(int unitId,string componentId)
        => _units.GetValueOrDefault(new(unitId,componentId))?.Node;
    public IReadOnlyList<MeshInstance3D> FindUnitModels(int unitId)
        => _units.Where(item=>item.Key.UnitId==unitId).OrderBy(item=>item.Key.ComponentId,StringComparer.Ordinal)
            .Select(item=>item.Value.Node).ToArray();

    private Display? GetDisplay(int category, out string? unavailable)
    {
        if (_displays.TryGetValue(category, out Display? result)) { unavailable = null; return result; }
        if (_unsupported.TryGetValue(category, out unavailable)) return null;
        OriginalModelMeshResult? mesh = null;
        try
        {
            _catalog ??= OriginalCeArmyModels.LoadCatalog(_repository!);
            LoadedOriginalArmyModel loaded = OriginalCeArmyModels.LoadKnownCategory(_repository!, _catalog, category);
            mesh = OriginalModelMesh.BuildArmyBindPose(loaded.Transforms);
            OriginalPreviewMaterialSet materials = _materials!.Load(loaded.Resources, mesh);
            result = new(loaded, mesh, materials);
            _displays.Add(category, result);
            unavailable = null;
            return result;
        }
        catch (Exception error) when (error is InvalidDataException or KeyNotFoundException or
                                      IOException or ArgumentException or InvalidOperationException or OverflowException)
        {
            mesh?.Mesh.Dispose();
            unavailable = $"Original Army category {category} unavailable: {error.Message}";
            _unsupported.Add(category, unavailable);
            GD.PushWarning(unavailable);
            return null;
        }
    }

    private void RemoveComponent(ComponentKey key)
    {
        if (!_units.Remove(key, out Entry? entry)) return;
        RemoveChild(entry.Node);
        entry.Node.Free();
    }

    private static void ValidateBinding(OriginalArmyVisualBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (binding.Category < 0 || binding.Category >= OriginalCeArmyModels.KnownSourceCategories.Count ||
            !binding.Rotation.IsFinite() || !binding.Scale.IsFinite() ||
            MathF.Abs(binding.Rotation.LengthSquared() - 1f) > 0.0001f ||
            binding.Scale.X <= 0 || binding.Scale.Y <= 0 || binding.Scale.Z <= 0 ||
            string.IsNullOrWhiteSpace(binding.SelectionSource))
            throw new InvalidDataException("Army binding requires a known category, normalized quaternion, positive finite scale and source label.");
    }

    private static void SetSources(MeshInstance3D node, Display display)
    {
        node.SetMeta("source_category", display.Source.Category.Category);
        node.SetMeta("source_category_name", display.Source.Category.SourceName);
        node.SetMeta("catalog_uri", display.Source.Catalog.Catalog.Source.ResourceUri);
        node.SetMeta("catalog_sha256", display.Source.Catalog.Catalog.Source.DecodedSha256);
        node.SetMeta("dictionary_uri", display.Source.Catalog.Dictionary.Source.ResourceUri);
        node.SetMeta("dictionary_sha256", display.Source.Catalog.Dictionary.Source.DecodedSha256);
        node.SetMeta("definition_object_id", display.Source.Definition.Definition.ObjectId);
        node.SetMeta("model_object_id", display.Source.Definition.ModelObject.ObjectId);
        node.SetMeta("model_uri", display.Mesh.Source.ResourceUri);
        node.SetMeta("model_sha256", display.Mesh.Source.DecodedSha256);
        node.SetMeta("model_is_override", display.Mesh.Source.OverrideSource is not null);
        node.SetMeta("texture_table_uri", display.Source.Resources.TextureTable.ResourceUri);
        node.SetMeta("texture_table_sha256", display.Source.Resources.TextureTable.DecodedSha256);
        node.SetMeta("source_materials", display.Materials.Sources);
        node.SetMeta("source_category_resource_identity_verified", true);
        node.SetMeta("original_material_execution_verified", false);
        node.SetMeta("source_army_weighted_bind_pose", display.Mesh.Mesh.GetMeta("source_army_weighted_bind_pose"));
    }

    public void ReleaseResources()
    {
        if (_disposed) return;
        foreach (var key in _units.Keys.ToArray()) RemoveComponent(key);
        foreach (Display display in _displays.Values) display.Mesh.Mesh.Dispose();
        _materials?.Dispose();
        _displays.Clear(); _unsupported.Clear();
        _materials = null; _catalog = null; _repository = null;
        _disposed = true;
    }

    public override void _ExitTree() => ReleaseResources();
}
