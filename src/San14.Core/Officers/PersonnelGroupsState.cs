using System.Collections.Immutable;

namespace San14.Core;

/// <summary>The ten ordered personnel groups. Unused native slots are not members.</summary>
public sealed record PersonnelGroupsState(ImmutableArray<ImmutableArray<int>> Groups);

public sealed partial record GameState
{
    public PersonnelGroupsState? PersonnelGroups { get; init; }
}
