namespace San14.Data;

public enum DeploymentPersonSortColumn
{
    Name, CommandCapacity, Leadership, Strength, Intelligence, Politics, Charisma,
    Responsibility, Health, CommandMembership, ServiceStatus, CreditRankStage,
    GovernorPriority, Selected
}

public readonly record struct DeploymentPersonSortRow(int OfficerId, bool Selected);
public sealed record DeploymentPersonOrderQuery(IReadOnlyList<int>? OfficerIds, string? UnresolvedInput)
{
    public bool Available => OfficerIds is not null && UnresolvedInput is null;
}
