namespace San14.Data;

public sealed record MapStaticModelInstanceFailure(string InstanceKey, string Reason);
public sealed record MapStaticModelInstanceSet(IReadOnlyList<MapStaticModelInstance> Instances,
    IReadOnlyList<MapStaticModelInstanceFailure> Unresolved, IReadOnlyList<int> EffectInstanceIndices)
{
    public bool Complete => Unresolved.Count == 0;
    public IReadOnlyList<string> ExcludedInstanceKeys { get; init; } = Array.Empty<string>();
}

/// <summary>Assembles the original scene population or current city categories.
/// World matrix providers are explicit. Source placement values remain attached
/// to instances and are not converted using a preview quaternion convention.</summary>
public static partial class MapStaticModelInstanceFactory
{
    public static MapStaticModelInstanceSet FromScenePlacements(MapStaticModelAssetCatalog assets,
        ScenePlacements placements, Func<ScenePlacement, MapStaticModelTransform?> sourceWorldTransform,
        Func<ScenePlacement, MapStaticModelAsset, IReadOnlyDictionary<int, MapStaticModelTransform>?>? sourceDrawTransforms = null)
    {
        ArgumentNullException.ThrowIfNull(assets); ArgumentNullException.ThrowIfNull(placements);
        ArgumentNullException.ThrowIfNull(sourceWorldTransform);
        if (placements.Scene.Scene.Source.ResourceUri != assets.Scene.Scene.Source.ResourceUri ||
            placements.Scene.Scene.Source.DecodedSha256 != assets.Scene.Scene.Source.DecodedSha256)
            throw new InvalidDataException("Native map placements belong to a different source scene revision.");
        var instances = new List<MapStaticModelInstance>(); var unresolved = new List<MapStaticModelInstanceFailure>();
        var effects = new List<int>();
        foreach (ScenePlacement placement in placements.Instances)
        {
            if (placement.Model is null) {effects.Add(placement.Source.Index); continue;}
            string key = $"scene:{placements.Scene.Scene.Source.ResourceUri}/{placement.Source.Index}/{placement.Source.Object.ObjectId:X8}";
            try
            {
                MapStaticModelTransform world = sourceWorldTransform(placement) ??
                    throw new InvalidDataException("Original WorldPQ position/normal matrix source is unresolved.");
                var asset = assets.LoadSceneInstance(placement.Source.Index);
                var transforms = sourceDrawTransforms?.Invoke(placement, asset);
                var instance = transforms is null
                    ? MapStaticModelInstances.WithIdentitySourceLocalTransforms(key, asset, world)
                    : new MapStaticModelInstance(key, asset, world, transforms).Snapshot();
                instances.Add(instance with {SourcePlacement = placement});
            }
            catch (InvalidDataException error) {unresolved.Add(new(key, error.Message));}
        }
        return new(instances.AsReadOnly(), unresolved.AsReadOnly(), effects.AsReadOnly());
    }

    public static MapStaticModelInstanceSet FromCurrentCities(MapStaticModelAssetCatalog assets,
        CityVisualAssetCatalog cityAssets, IEnumerable<CityVisualSelection> currentSelections,
        Func<CityVisualSelection, MapStaticModelTransform?> sourceWorldTransform,
        Func<CityVisualSelection, MapStaticModelAsset, IReadOnlyDictionary<int, MapStaticModelTransform>?>? sourceDrawTransforms = null)
    {
        ArgumentNullException.ThrowIfNull(assets); ArgumentNullException.ThrowIfNull(cityAssets);
        ArgumentNullException.ThrowIfNull(currentSelections); ArgumentNullException.ThrowIfNull(sourceWorldTransform);
        var instances = new List<MapStaticModelInstance>(); var unresolved = new List<MapStaticModelInstanceFailure>();
        var selections = currentSelections.ToArray();
        if (selections.Select(s => s.Slot).Distinct().Count() != selections.Length)
            throw new InvalidDataException("Native map city population contains duplicate physical city slots.");
        foreach (CityVisualSelection selection in selections)
        {
            string key = "city:" + selection.Slot;
            try
            {
                MapStaticModelTransform world = sourceWorldTransform(selection) ??
                    throw new InvalidDataException("Original current city world position/normal matrix source is unresolved.");
                var asset = assets.LoadCity(cityAssets, selection);
                var transforms = sourceDrawTransforms?.Invoke(selection, asset);
                var instance = transforms is null
                    ? MapStaticModelInstances.WithIdentitySourceLocalTransforms(key, asset, world)
                    : new MapStaticModelInstance(key, asset, world, transforms).Snapshot();
                instances.Add(instance with {CitySelection = selection});
            }
            catch (InvalidDataException error) {unresolved.Add(new(key, error.Message));}
        }
        return new(instances.AsReadOnly(), unresolved.AsReadOnly(), Array.Empty<int>());
    }
}
