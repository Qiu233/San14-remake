namespace San14.Data;

public sealed partial class SourceWorldContext : IBaseNeighborhoodState
{
    public HexReference? FindBaseHex(BaseReference reference) =>
        CurrentGateMapQueries.FindHex(reference, this);

    public int? ReadBaseNativeKind(BaseReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        _ = BasePhysical(reference);
        return reference.Kind switch
        {
            BaseKind.City => 5,
            BaseKind.Gate => 6,
            _ => throw new InvalidDataException("Current base vtable kind is unsupported.")
        };
    }

    public IReadOnlyList<BaseNeighborhoodResult> InitializeBaseNeighborhoods() =>
        BaseNeighborhoodInitialization.Apply(this);

    /// <summary>Full game-level 3306E0 order; explicit current pool inputs are required.</summary>
    public IReadOnlyList<BaseNeighborhoodResult> InitializeAreaCitiesAndBaseNeighborhoods(int staticFlag)
    {
        InitializeStaticAreaCities(staticFlag);
        return InitializeBaseNeighborhoods();
    }
}
