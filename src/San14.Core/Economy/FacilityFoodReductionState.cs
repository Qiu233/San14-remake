using System.Collections.Immutable;

namespace San14.Core;

/// <summary>The scheduled facility calculation's current food reduction by physical map Hex.</summary>
public sealed record FacilityFoodReductionState(ImmutableArray<byte> PercentByHex);

public sealed partial record GameState
{
    public FacilityFoodReductionState? FacilityFoodReduction { get; init; }
}
