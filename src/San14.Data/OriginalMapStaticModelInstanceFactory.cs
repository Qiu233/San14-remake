namespace San14.Data;

public sealed record OriginalMapStaticModelInstanceFailure(string InstanceKey, string Reason);
public sealed record OriginalMapStaticModelInstanceSet(IReadOnlyList<OriginalMapStaticModelInstance> Instances,
    IReadOnlyList<OriginalMapStaticModelInstanceFailure> Unresolved, IReadOnlyList<int> EffectInstanceIndices)
{
    public bool Complete => Unresolved.Count == 0;
    public IReadOnlyList<string> ExcludedInstanceKeys { get; init; } = Array.Empty<string>();
}

/// <summary>Assembles the original scene population or current city categories.
/// World matrix providers are explicit. Source placement values remain attached
/// to instances and are not converted using a preview quaternion convention.</summary>
public static partial class OriginalMapStaticModelInstanceFactory
{
    public static OriginalMapStaticModelInstanceSet FromScenePlacements(OriginalMapStaticModelAssetCatalog assets,
        ScenePlacements placements, Func<ScenePlacement, OriginalMapStaticModelTransform?> sourceWorldTransform,
        Func<ScenePlacement, OriginalMapStaticModelAsset, IReadOnlyDictionary<int, OriginalMapStaticModelTransform>?>? sourceDrawTransforms = null)
    {
        ArgumentNullException.ThrowIfNull(assets); ArgumentNullException.ThrowIfNull(placements);
        ArgumentNullException.ThrowIfNull(sourceWorldTransform);
        if (placements.Scene.Scene.Source.ResourceUri != assets.Scene.Scene.Source.ResourceUri ||
            placements.Scene.Scene.Source.DecodedSha256 != assets.Scene.Scene.Source.DecodedSha256)
            throw new InvalidDataException("Native map placements belong to a different source scene revision.");
        var instances = new List<OriginalMapStaticModelInstance>(); var unresolved = new List<OriginalMapStaticModelInstanceFailure>();
        var effects = new List<int>();
        foreach (ScenePlacement placement in placements.Instances)
        {
            if (placement.Model is null) {effects.Add(placement.Source.Index); continue;}
            string key = $"scene:{placements.Scene.Scene.Source.ResourceUri}/{placement.Source.Index}/{placement.Source.Object.ObjectId:X8}";
            try
            {
                OriginalMapStaticModelTransform world = sourceWorldTransform(placement) ??
                    throw new InvalidDataException("Original WorldPQ position/normal matrix source is unresolved.");
                var asset = assets.LoadSceneInstance(placement.Source.Index);
                var transforms = sourceDrawTransforms?.Invoke(placement, asset);
                var instance = transforms is null
                    ? OriginalMapStaticModelInstances.WithIdentitySourceLocalTransforms(key, asset, world)
                    : new OriginalMapStaticModelInstance(key, asset, world, transforms).Snapshot();
                instances.Add(instance with {SourcePlacement = placement});
            }
            catch (InvalidDataException error) {unresolved.Add(new(key, error.Message));}
        }
        return new(instances.AsReadOnly(), unresolved.AsReadOnly(), effects.AsReadOnly());
    }

    public static OriginalMapStaticModelInstanceSet FromCurrentCities(OriginalMapStaticModelAssetCatalog assets,
        CityVisualAssetCatalog cityAssets, IEnumerable<CityVisualSelection> currentSelections,
        Func<CityVisualSelection, OriginalMapStaticModelTransform?> sourceWorldTransform,
        Func<CityVisualSelection, OriginalMapStaticModelAsset, IReadOnlyDictionary<int, OriginalMapStaticModelTransform>?>? sourceDrawTransforms = null)
    {
        ArgumentNullException.ThrowIfNull(assets); ArgumentNullException.ThrowIfNull(cityAssets);
        ArgumentNullException.ThrowIfNull(currentSelections); ArgumentNullException.ThrowIfNull(sourceWorldTransform);
        var instances = new List<OriginalMapStaticModelInstance>(); var unresolved = new List<OriginalMapStaticModelInstanceFailure>();
        var selections = currentSelections.ToArray();
        if (selections.Select(s => s.Slot).Distinct().Count() != selections.Length)
            throw new InvalidDataException("Native map city population contains duplicate physical city slots.");
        foreach (CityVisualSelection selection in selections)
        {
            string key = "city:" + selection.Slot;
            try
            {
                OriginalMapStaticModelTransform world = sourceWorldTransform(selection) ??
                    throw new InvalidDataException("Original current city world position/normal matrix source is unresolved.");
                var asset = assets.LoadCity(cityAssets, selection);
                var transforms = sourceDrawTransforms?.Invoke(selection, asset);
                var instance = transforms is null
                    ? OriginalMapStaticModelInstances.WithIdentitySourceLocalTransforms(key, asset, world)
                    : new OriginalMapStaticModelInstance(key, asset, world, transforms).Snapshot();
                instances.Add(instance with {CitySelection = selection});
            }
            catch (InvalidDataException error) {unresolved.Add(new(key, error.Message));}
        }
        return new(instances.AsReadOnly(), unresolved.AsReadOnly(), Array.Empty<int>());
    }
}
