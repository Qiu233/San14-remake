namespace San14.Core;

public sealed record ExpandGovernanceDepartmentOrder(int ForceId, int Group) : GameOrder(ForceId);

/// <summary>The currently allocatable doctrine slots and selected department.
/// Unknown resource or saved inputs never become a zero-cost authorization.</summary>
public sealed record GovernanceExpansionEvaluation(bool? Allowed, int? RemainingPoints,
    int? Capacity, int? MaximumCapacity, int? Expansion, string? Reason, string? UnresolvedInput)
{
    public bool Available => UnresolvedInput is null;
}

public interface IGovernanceExpansionRules
{
    GovernanceExpansionEvaluation EvaluateGovernanceExpansion(GameState state, ForceState force,
        int group, CommandContext context);
    CommandResult ApplyGovernanceExpansion(GameState state, ForceState force,
        ExpandGovernanceDepartmentOrder order, CommandContext context);
}
