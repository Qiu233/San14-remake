using System.Security.Cryptography;
using San14.Data;

namespace San14.Data;

/// <summary>Complete current inputs for one Model's ordinary per-view draw
/// publication. Flag bit 0 is not a general enabled boolean. Eligibility covers
/// the current per-view/preview/queue gates before the original box selector.</summary>
public sealed record OriginalMapModelNativeVisibilityPublication(string CurrentSourceIdentity,
    bool? PerViewDrawEligible, uint? CurrentNodeFlags44, uint? CurrentInstanceFrustumType,
    OriginalMapModelNativeBoundsPublication Bounds,
    IReadOnlyList<bool>? CompleteAdditionalCullingRejects);
public sealed record OriginalMapModelNativeViewPublication(string CurrentViewIdentity,
    uint? CurrentCommonViewFrustumType);

/// <summary>Exact original ModelWorldPQ mode0 OBB planner. Retains the entire
/// population partition and source order, sharing the Camera used for terrain
/// and model constants. Current native flags/gates and custom cullers are
/// explicit; unresolved parents/child unions/fallback bounds reject the frame.</summary>
public sealed class OriginalMapModelNativeVisibilityPlanner
{
    private readonly Func<OriginalMapStaticModelInstance, OriginalMapStaticModelCamera,
        OriginalMapModelNativeVisibilityPublication> _publication;
    private readonly Func<OriginalMapStaticModelCamera, OriginalMapModelNativeViewPublication> _view;
    private readonly Dictionary<OriginalMapStaticModelAsset, byte[]> _modelSources = new(ReferenceEqualityComparer.Instance);
    public long PlannedFrames { get; private set; }
    public int LastPopulationCount { get; private set; }
    public int LastVisibleCount { get; private set; }

    public OriginalMapModelNativeVisibilityPlanner(
        Func<OriginalMapStaticModelInstance, OriginalMapStaticModelCamera, OriginalMapModelNativeVisibilityPublication> currentPublication,
        Func<OriginalMapStaticModelCamera, OriginalMapModelNativeViewPublication> currentView)
    { ArgumentNullException.ThrowIfNull(currentPublication); ArgumentNullException.ThrowIfNull(currentView);
        _publication = currentPublication; _view = currentView; }

    public OriginalMapStaticModelVisibleSet Select(OriginalMapStaticModelInstanceSet population,
        OriginalMapStaticModelCamera acceptedCamera)
    {
        ArgumentNullException.ThrowIfNull(population); ArgumentNullException.ThrowIfNull(acceptedCamera);
        if (!population.Complete) throw new InvalidDataException("Original Model visibility requires the complete resolved population.");
        var camera = OriginalMapStaticModelCamera.FromExplicitRows(acceptedCamera.WorldToProjectionRows, acceptedCamera.WorldToViewRows);
        var view = _view(camera) ?? throw new InvalidDataException("Current native Model View is unavailable.");
        if (string.IsNullOrWhiteSpace(view.CurrentViewIdentity) || view.CurrentCommonViewFrustumType is null)
            throw new InvalidDataException("Current native Model View requires identity and resolved CommonView frustum type.");
        var snapshots = population.Instances.Select(i => i.Snapshot()).ToArray();
        if (snapshots.Select(i => i.InstanceKey).Distinct(StringComparer.Ordinal).Count() != snapshots.Length)
            throw new InvalidDataException("Original Model visibility population repeats an instance key.");
        var verified = new HashSet<OriginalMapStaticModelAsset>(ReferenceEqualityComparer.Instance);
        var visible = new List<OriginalMapStaticModelInstance>(); var outside = new List<string>(); var disabled = new List<string>();
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
            if (verified.Add(instance.Asset)) ValidateSource(instance.Asset);
            var bounds = OriginalMapModelNativeBoundsSources.FromSourceWorld(instance, current.Bounds);
            uint mode = OriginalMapModelNativeVisibilitySources.ResolveFrustumCullingType(
                current.CurrentInstanceFrustumType.Value, view.CurrentCommonViewFrustumType.Value);
            if (OriginalMapModelNativeVisibilitySources.IsCulled(camera.Frustum, bounds, mode, current.CompleteAdditionalCullingRejects))
                outside.Add(instance.InstanceKey);
            else visible.Add(instance);
        }
        PlannedFrames++; LastPopulationCount = snapshots.Length; LastVisibleCount = visible.Count;
        return new(camera, visible.AsReadOnly(), outside.AsReadOnly(), disabled.AsReadOnly());
    }

    private void ValidateSource(OriginalMapStaticModelAsset asset)
    {
        var source = asset.RawTransforms.Source;
        if (!_modelSources.TryGetValue(asset, out var owned))
        {
            owned = source.Bytes.ToArray();
            if (!Convert.ToHexString(SHA256.HashData(owned)).Equals(source.DecodedSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Original Model native bounds differ from their declared source revision.");
            _modelSources.Add(asset, owned);
        }
        if (!source.Bytes.AsSpan().SequenceEqual(owned))
            throw new InvalidDataException("Caller changed an immutable Model native bound source.");
    }
    public void Invalidate() => _modelSources.Clear();
}
