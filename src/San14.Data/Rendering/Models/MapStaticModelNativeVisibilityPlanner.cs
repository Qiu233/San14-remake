using San14.Data;

namespace San14.Data;

/// <summary>Complete current inputs for one Model's ordinary per-view draw
/// publication. Flag bit 0 is not a general enabled boolean. Eligibility covers
/// the current per-view/preview/queue gates before the original box selector.</summary>
public sealed record MapModelNativeVisibilityPublication(string CurrentSourceIdentity,
    bool? PerViewDrawEligible, uint? CurrentNodeFlags44, uint? CurrentInstanceFrustumType,
    MapModelNativeBoundsPublication Bounds,
    IReadOnlyList<bool>? CompleteAdditionalCullingRejects);
public sealed record MapModelNativeViewPublication(string CurrentViewIdentity,
    uint? CurrentCommonViewFrustumType);

/// <summary>Exact original ModelWorldPQ mode0 OBB planner. Retains the entire
/// population partition and source order, sharing the Camera used for terrain
/// and model constants. Current native flags/gates and custom cullers are
/// explicit; unresolved parents/child unions/fallback bounds reject the frame.</summary>
public sealed class MapModelNativeVisibilityPlanner
{
    private readonly Func<MapStaticModelInstance, MapStaticModelCamera,
        MapModelNativeVisibilityPublication> _publication;
    private readonly Func<MapStaticModelCamera, MapModelNativeViewPublication> _view;
    public long PlannedFrames { get; private set; }
    public int LastPopulationCount { get; private set; }
    public int LastVisibleCount { get; private set; }

    public MapModelNativeVisibilityPlanner(
        Func<MapStaticModelInstance, MapStaticModelCamera, MapModelNativeVisibilityPublication> currentPublication,
        Func<MapStaticModelCamera, MapModelNativeViewPublication> currentView)
    { ArgumentNullException.ThrowIfNull(currentPublication); ArgumentNullException.ThrowIfNull(currentView);
        _publication = currentPublication; _view = currentView; }

    public MapStaticModelVisibleSet Select(MapStaticModelInstanceSet population,
        MapStaticModelCamera acceptedCamera)
    {
        ArgumentNullException.ThrowIfNull(population); ArgumentNullException.ThrowIfNull(acceptedCamera);
        if (!population.Complete) throw new InvalidDataException("Original Model visibility requires the complete resolved population.");
        var camera = MapStaticModelCamera.FromExplicitRows(acceptedCamera.WorldToProjectionRows, acceptedCamera.WorldToViewRows);
        var view = _view(camera) ?? throw new InvalidDataException("Current native Model View is unavailable.");
        if (string.IsNullOrWhiteSpace(view.CurrentViewIdentity) || view.CurrentCommonViewFrustumType is null)
            throw new InvalidDataException("Current native Model View requires identity and resolved CommonView frustum type.");
        var snapshots = population.Instances.Select(i => i.Snapshot()).ToArray();
        if (snapshots.Select(i => i.InstanceKey).Distinct(StringComparer.Ordinal).Count() != snapshots.Length)
            throw new InvalidDataException("Original Model visibility population repeats an instance key.");
        var visible = new List<MapStaticModelInstance>(); var outside = new List<string>(); var disabled = new List<string>();
        foreach (var instance in snapshots)
        {
            if (!instance.Enabled) { disabled.Add(instance.InstanceKey); continue; }
            var current = _publication(instance, camera) ?? throw new InvalidDataException("Current native Model publication is unavailable.");
            if (string.IsNullOrWhiteSpace(current.CurrentSourceIdentity) || current.CurrentSourceIdentity != instance.CurrentSourceIdentity ||
                current.PerViewDrawEligible is null || current.CurrentNodeFlags44 is null || current.CurrentInstanceFrustumType is null ||
                current.CompleteAdditionalCullingRejects is null)
                throw new InvalidDataException("Current native Model visibility identity, flags, per-view gates or complete cullers are unresolved.");
            if (!current.PerViewDrawEligible.Value) { disabled.Add(instance.InstanceKey); continue; }
            // DB900D's bit2 branch replaces bounds using current View extrema.
            // Its current inputs must be implemented before that branch is used.
            if ((current.CurrentNodeFlags44.Value & 4) != 0)
                throw new InvalidDataException("Current native Model View-extrema bound fallback is unresolved.");
            var bounds = MapModelNativeBoundsSources.FromSourceWorld(instance, current.Bounds);
            uint mode = MapModelNativeVisibilitySources.ResolveFrustumCullingType(
                current.CurrentInstanceFrustumType.Value, view.CurrentCommonViewFrustumType.Value);
            if (MapModelNativeVisibilitySources.IsCulled(camera.Frustum, bounds, mode, current.CompleteAdditionalCullingRejects))
                outside.Add(instance.InstanceKey);
            else visible.Add(instance);
        }
        PlannedFrames++; LastPopulationCount = snapshots.Length; LastVisibleCount = visible.Count;
        return new(camera, visible.AsReadOnly(), outside.AsReadOnly(), disabled.AsReadOnly());
    }

}
