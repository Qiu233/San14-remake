using San14.Data;

namespace San14.Data;

/// <summary>Constructs the proven fresh node flag/frustum/bound prefix from
/// actual definitions. Per-view eligibility and current custom cullers remain
/// explicit publications. The complete constructor's final slot writer is
/// available through OriginalMapModelNativeCurrentVisibilitySources.</summary>
public static class MapModelNativeFreshVisibilitySources
{
    public static MapModelNativeVisibilityPlanner Create(MapStaticModelInstanceSet freshPopulation,
        LoadedObjects explicitlyPublishedCommonViewSource, uint commonViewObjectId, string currentViewIdentity,
        Func<MapStaticModelInstance, MapStaticModelCamera, bool?> currentPerViewEligibility,
        Func<MapStaticModelInstance, MapStaticModelCamera, IReadOnlyList<bool>?> completeCurrentAdditionalCullingRejects)
    {
        ArgumentNullException.ThrowIfNull(freshPopulation); ArgumentNullException.ThrowIfNull(currentPerViewEligibility);
        ArgumentNullException.ThrowIfNull(completeCurrentAdditionalCullingRejects); ArgumentException.ThrowIfNullOrWhiteSpace(currentViewIdentity);
        if (!freshPopulation.Complete) throw new InvalidDataException("Fresh original Model visibility requires complete native population preparation.");
        var common = MapModelNativeVisibilitySources.ReadCommonViewTemplate(explicitlyPublishedCommonViewSource, commonViewObjectId);
        var currentNodes = new Dictionary<string, (string Identity, MapStaticModelAsset Asset,
            MapModelNativeFreshVisibilityPrefix Prefix)>(StringComparer.Ordinal);
        foreach (var instance in freshPopulation.Instances)
        {
            var publication = instance.NativePublication;
            if (publication is not {FreshConstructorBindPrefix: true, ParentBound: false, PrimaryDescriptorPresent: true} ||
                publication.CurrentIdentity != instance.CurrentSourceIdentity || string.IsNullOrWhiteSpace(instance.CurrentSourceIdentity))
                throw new InvalidDataException("Fresh visibility cannot replace a current/unknown/parented Model publication.");
            var prefix = MapModelNativeVisibilitySources.PrepareFreshSingleNode(instance,
                new(instance.CurrentSourceIdentity!, false, true, true));
            currentNodes.Add(instance.InstanceKey, (instance.CurrentSourceIdentity!, instance.Asset, prefix));
        }
        return new((instance, camera) =>
        {
            if (!currentNodes.TryGetValue(instance.InstanceKey, out var node) || node.Identity != instance.CurrentSourceIdentity ||
                !ReferenceEquals(node.Asset, instance.Asset) || instance.NativePublication is not {FreshConstructorBindPrefix: true, ParentBound: false})
                throw new InvalidDataException("Fresh visibility planner was reused for a different current node/category/publication.");
            return new(node.Identity, currentPerViewEligibility(instance, camera), node.Prefix.ConstructorFlags44,
                node.Prefix.ConstructorFrustumCullingType33C, node.Prefix.BoundsPublication,
                completeCurrentAdditionalCullingRejects(instance, camera));
        }, _ => new(currentViewIdentity, common.Value));
    }
}
