using System.Collections.Immutable;

namespace San14.Core;

/// <summary>Assignments prepared by territorial expansion and consumed by its later occupation stage.</summary>
public sealed record TerritoryExpansionState(ImmutableDictionary<int, int> PendingAssignments);

public sealed partial record GameState
{
    public TerritoryExpansionState? TerritoryExpansion { get; init; }
}
