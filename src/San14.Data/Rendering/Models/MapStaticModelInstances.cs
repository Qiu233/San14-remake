using San14.Data.Formats;

namespace San14.Data;

/// <summary>One explicit instance retains model-space world bounds and complete
/// per-draw matrices. Raw local geometry is uploaded unchanged by the native
/// layer. A missing draw transform is an unresolved input, never identity.</summary>
public sealed record MapStaticModelInstance(string InstanceKey, MapStaticModelAsset Asset,
    MapStaticModelTransform BoundsTransform,
    IReadOnlyDictionary<int, MapStaticModelTransform> DrawTransforms)
{
    public ScenePlacement? SourcePlacement { get; init; }
    public CityVisualSelection? CitySelection { get; init; }
    public UnitWorldTransformState? SourceWorldState { get; init; }
    public string? CurrentSourceIdentity { get; init; }
    public MapStaticModelPublication? NativePublication { get; init; }
    public bool Enabled { get; init; } = true;

    public MapStaticModelInstance Snapshot()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(InstanceKey); ArgumentNullException.ThrowIfNull(Asset);
        ArgumentNullException.ThrowIfNull(BoundsTransform); ArgumentNullException.ThrowIfNull(DrawTransforms);
        int[] indices = Asset.Draws.Select(d => d.Index).Order().ToArray();
        if (!indices.SequenceEqual(DrawTransforms.Keys.Order()))
            throw new InvalidDataException($"Native map model instance {InstanceKey} requires a complete transform for each original draw.");
        return this with {BoundsTransform = BoundsTransform.Snapshot(),
            DrawTransforms = DrawTransforms.ToDictionary(p => p.Key, p => p.Value.Snapshot())};
    }
}

public sealed record MapStaticModelVisibleSet(MapStaticModelCamera Camera,
    IReadOnlyList<MapStaticModelInstance> Visible, IReadOnlyList<string> OutsideFrustum,
    IReadOnlyList<string> Disabled);

public static class MapStaticModelInstances
{
    /// <summary>Shared world rows are valid for raw rigid draws only after every
    /// referenced source local matrix is identity. Nonidentity local transforms,
    /// indexed palettes and skinned geometry require complete explicit per-draw
    /// rows. This check consumes raw G1M fields, not an ArrayMesh preview.</summary>
    public static MapStaticModelInstance WithIdentitySourceLocalTransforms(string instanceKey,
        MapStaticModelAsset asset, MapStaticModelTransform worldTransform)
    {
        ArgumentNullException.ThrowIfNull(asset); ArgumentNullException.ThrowIfNull(worldTransform);
        foreach (G1DrawRecord draw in asset.Draws)
        {
            if (draw.UnknownValue8 >= asset.RawTransforms.Transforms.JointPalettes.Count)
                throw new InvalidDataException($"Native map draw {draw.Index} has an unresolved source local palette.");
            var palette = asset.RawTransforms.Transforms.JointPalettes[(int)draw.UnknownValue8];
            if (palette.Entries.Count != 1 || asset.RawTransforms.Geometry.Layouts[draw.LayoutIndex].Attributes.Any(a => (a.SemanticCode & 255) == 2))
                throw new InvalidDataException($"Native map draw {draw.Index} requires explicit indexed local matrices.");
            G1JointPaletteEntry entry = palette.Entries[0];
            var transforms = asset.RawTransforms.Transforms;
            var local = transforms.ReferenceMatrices[entry.MatrixIndex].Matrix * transforms.JointModelMatrices[entry.JointIndex];
            if (local != System.Numerics.Matrix4x4.Identity)
                throw new InvalidDataException($"Native map draw {draw.Index} has a nonidentity raw source local transform; complete draw rows are required.");
        }
        var snapshot = worldTransform.Snapshot();
        return new MapStaticModelInstance(instanceKey, asset, snapshot,
            asset.Draws.ToDictionary(d => d.Index, _ => snapshot.Snapshot())).Snapshot();
    }

    /// <summary>The camera is supplied for every selection. Bounds are the
    /// original model header bounds transformed by this instance's source rows;
    /// selection never reads a Godot node or uses camera defaults.</summary>
    public static MapStaticModelVisibleSet SelectVisible(IEnumerable<MapStaticModelInstance> instances,
        MapStaticModelCamera explicitCamera)
    {
        ArgumentNullException.ThrowIfNull(instances); ArgumentNullException.ThrowIfNull(explicitCamera);
        // Rebuild planes from the same projection consumed by the shader, so a
        // stale caller plane set cannot select a different visible population.
        var camera = MapStaticModelCamera.FromExplicitRows(explicitCamera.WorldToProjectionRows,
            explicitCamera.WorldToViewRows);
        MapStaticModelInstance[] snapshot = instances.Select(i => i.Snapshot()).ToArray();
        if (snapshot.Select(i => i.InstanceKey).Distinct(StringComparer.Ordinal).Count() != snapshot.Length)
            throw new InvalidDataException("Native map model frame contains duplicate instance keys.");
        var visible = new List<MapStaticModelInstance>(); var outside = new List<string>(); var disabled = new List<string>();
        foreach (var instance in snapshot)
        {
            if (!instance.Enabled) {disabled.Add(instance.InstanceKey); continue;}
            var bounds = MapStaticModelTransforms.WorldBounds(instance.Asset, instance.BoundsTransform);
            if (TerrainFrustumMath.IntersectsAabb(camera.Frustum, bounds.Minimum, bounds.Maximum)) visible.Add(instance);
            else outside.Add(instance.InstanceKey);
        }
        return new(camera, visible.AsReadOnly(), outside.AsReadOnly(), disabled.AsReadOnly());
    }
}
