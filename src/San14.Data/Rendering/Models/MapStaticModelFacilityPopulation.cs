namespace San14.Data;

public static partial class MapStaticModelInstanceFactory
{
    /// <summary>Reuse the actual CUnitObject/CUnitSubObject constructors,
    /// category selection, clamped ground and complete primitive transforms.
    /// Only the native visibility node is attached here; pose is not rebuilt
    /// from StructureKind, a display name or adjacency.</summary>
    public static MapStaticModelInstanceSet FromCurrentFacilitySceneModels(MapStaticModelAssetCatalog assets,
        FacilitySceneModelFrame frame)
    {
        ArgumentNullException.ThrowIfNull(assets); ArgumentNullException.ThrowIfNull(frame);
        if (frame.Missing.Count != 0)
            throw new InvalidDataException("Current facility models are unresolved: " +
                string.Join("; ", frame.Missing.Select(item => item.InstanceKey + ": " + item.Reason)));
        var instances = new List<MapStaticModelInstance>(frame.Rows.Count);
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in frame.Rows)
        {
            if (row.Identity != row.Instance.InstanceKey || row.Identity != row.Instance.CurrentSourceIdentity ||
                !identities.Add(row.Identity) || row.Instance.SourceWorldState is null ||
                !ReferenceEquals(row.Instance.Asset, assets.LoadFacilityCategory(row.Category)))
                throw new InvalidDataException("Facility model does not belong to its actual current source catalog/identity.");
            instances.Add(row.Instance with { NativePublication = MapStaticModelPublication.FreshUnparented(row.Identity) });
        }
        return new(instances.AsReadOnly(), Array.Empty<MapStaticModelInstanceFailure>(), Array.Empty<int>());
    }
}
