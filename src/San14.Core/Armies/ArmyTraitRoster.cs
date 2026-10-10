namespace San14.Core;

/// <summary>Ordered army trait contributors, including repeated references.
/// A null entry is unknown; an empty roster is known empty. Source table
/// capacity, placeholder validity and effect decoding belong to the ruleset.</summary>
public sealed record ArmyTraitRoster
{
    public IReadOnlyList<int?> TraitIds { get; }
    public ArmyTraitRoster(IReadOnlyList<int?> traitIds) => TraitIds = Array.AsReadOnly(traitIds.ToArray());
}

public sealed partial record UnitState
{
    public ArmyTraitRoster? TraitRoster { get; init; }
}
