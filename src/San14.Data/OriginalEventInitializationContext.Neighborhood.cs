namespace San14.Data;

public sealed partial class OriginalEventInitializationContext : IOriginalBaseNeighborhoodState
{
    public OriginalHexReference? FindBaseHex(OriginalBaseReference reference) =>
        OriginalCurrentGateMapQueries.FindHex(reference, this);

    public int? ReadBaseNativeKind(OriginalBaseReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        _ = BasePhysical(reference);
        return reference.Kind switch
        {
            OriginalBaseKind.City => 5,
            OriginalBaseKind.Gate => 6,
            _ => throw new InvalidDataException("Current base vtable kind is unsupported.")
        };
    }

    public IReadOnlyList<OriginalBaseNeighborhoodResult> InitializeBaseNeighborhoods() =>
        OriginalBaseNeighborhoodInitialization.Apply(this);

    /// <summary>Full game-level 3306E0 order; explicit current pool inputs are required.</summary>
    public IReadOnlyList<OriginalBaseNeighborhoodResult> InitializeAreaCitiesAndBaseNeighborhoods(int staticFlag)
    {
        InitializeStaticAreaCities(staticFlag);
        return InitializeBaseNeighborhoods();
    }
}
