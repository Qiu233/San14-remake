using System.Collections.Immutable;

namespace San14.Core;

public sealed record ArmyCautionGroupPlanningState(float? EstimatedStrength, bool? Status,
    int? VirtualTargetLocationId)
{
    public static ArmyCautionGroupPlanningState Reset { get; } = new(0f, false, 0);
}

public sealed partial record ArmyCautionsState
{
    /// <summary>Independent current group planning facts. Missing owners or
    /// rows remain unknown. Actual constructor/reset writers install Reset;
    /// a query never infers it from an empty member list.</summary>
    public ImmutableDictionary<int, ArmyCautionGroupPlanningState>? GroupPlanning { get; init; }
}
