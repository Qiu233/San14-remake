using System.Collections.Immutable;

namespace San14.Core;

/// <summary>Unspent doctrine points awarded by personnel credit promotions.
/// Vector positions represent the six doctrine identities, in order.</summary>
public sealed record ForceCreditReserveState(ImmutableDictionary<int, ImmutableArray<byte>> PointsByForce);

public sealed partial record GameState
{
    public ForceCreditReserveState? ForceCreditReserves { get; init; }
}
