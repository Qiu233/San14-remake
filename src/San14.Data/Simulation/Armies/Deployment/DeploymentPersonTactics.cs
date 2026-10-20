namespace San14.Data;

public sealed record DeploymentPersonTacticRow(
    int OfficerId, IReadOnlyList<DeploymentPersonBasicValue<int>> Tactics);
public sealed record DeploymentPersonTacticQuery(
    IReadOnlyList<DeploymentPersonTacticRow>? Rows, string? UnresolvedInput)
{
    public bool Available => Rows is not null && UnresolvedInput is null;
}
