namespace San14.Core;

/// <summary>A department is an ordered list, independently of the legacy
/// playable screen's six slot identifiers.</summary>
public sealed record GovernanceDepartmentConfiguration(int Group, IReadOnlyList<int> OfficerIds);
public sealed record GovernanceRoster(int? AdvisorId, IReadOnlyList<GovernanceDepartmentConfiguration> Groups);
public sealed record SetGovernanceRosterOrder(int ForceId, GovernanceRoster Roster,
    bool AcceptLevelOverflow = false) : GameOrder(ForceId);
public sealed record CompleteGovernanceNarrationOrder(int ForceId, long BatchSequence) : GameOrder(ForceId);

/// <summary>The saved transition word is exposed without inventing a turn or
/// day countdown. Zero is the existing policy consumer's active condition.</summary>
public sealed record GovernanceRosterMember(int OfficerId, int PendingValue)
{
    public bool Active => PendingValue == 0;
}

public sealed record GovernanceDepartmentView(int Group, int NameMessageId,
    int? Capacity, int? Expansion, IReadOnlyList<GovernanceRosterMember> Members,
    string? UnresolvedInput);
public sealed record GovernanceCandidate(int OfficerId, int? Group, int? PolicyCost,
    int? AdvisorIntelligence, bool? DepartmentEligible, bool? AdvisorEligible,
    string? UnresolvedInput);
public sealed record GovernanceRosterView(int ForceId, GovernanceRosterMember? Advisor,
    IReadOnlyList<GovernanceDepartmentView> Groups, IReadOnlyList<GovernanceCandidate> Candidates,
    int? PolicyCost, int? CostLimit, string? UnresolvedInput)
{
    public bool Available => UnresolvedInput is null;
}

public sealed record GovernanceRosterEvaluation(bool? Allowed, int? CommandBooks,
    int? ChargedDistrictId, int? PolicyCost, int? CostLimit, bool? ExceedsLevelLimit,
    string? Reason, string? UnresolvedInput)
{
    public bool Available => UnresolvedInput is null;
}

/// <summary>One operation provider owns both pure preview and actual submission.
/// Queries borrow the committed state; they do not execute orders or clone it.</summary>
public interface IGovernanceRosterRules
{
    GovernanceRosterView QueryGovernanceRoster(GameState state, int forceId);
    GovernanceRosterEvaluation EvaluateGovernanceRoster(GameState state, ForceState force,
        GovernanceRoster target, CommandContext context);
    CommandResult ApplyGovernanceRoster(GameState state, ForceState force,
        SetGovernanceRosterOrder order, CommandContext context);
    CommandResult CompleteGovernanceNarration(GameState state, ForceState force,
        CompleteGovernanceNarrationOrder order, CommandContext context);
}
