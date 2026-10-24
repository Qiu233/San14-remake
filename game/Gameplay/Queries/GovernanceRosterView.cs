namespace San14.Client.Gameplay;

internal sealed record GovernanceRosterOfficer(int Id, string Name, string? PolicyName,
    int? PolicyCost, int? PendingValue, bool Selectable = true);
internal sealed record GovernanceRosterDepartment(int Group, string Name, int? Capacity, int? Expansion,
    IReadOnlyList<GovernanceRosterOfficer> Members, IReadOnlyList<GovernanceRosterOfficer> Candidates);
internal sealed record GovernanceRosterPageModel(string ForceName, GovernanceRosterOfficer? Advisor,
    IReadOnlyList<GovernanceRosterOfficer> AdvisorCandidates, IReadOnlyList<GovernanceRosterDepartment> Departments,
    bool CanEdit);
internal sealed record GovernanceDepartmentSelection(int Group, IReadOnlyList<int> OfficerIds);
internal sealed record GovernanceRosterSelection(int? AdvisorId, IReadOnlyList<GovernanceDepartmentSelection> Departments);
internal sealed record GovernanceRosterEvaluationView(bool? Allowed, int? CommandBooks, int? PolicyCost,
    int? PolicyCostLimit, bool? ExceedsLevelLimit, string Status);
