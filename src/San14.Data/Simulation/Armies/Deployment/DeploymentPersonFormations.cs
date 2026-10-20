namespace San14.Data;

public sealed record DeploymentPersonFormationCapability(
    int ResourceFormationId, DeploymentPersonBasicValue<bool> Capability);
public sealed record DeploymentPersonFormationRow(
    int OfficerId, IReadOnlyList<DeploymentPersonFormationCapability> Capabilities);
public sealed record DeploymentPersonFormationQuery(
    IReadOnlyList<DeploymentPersonFormationRow>? Rows, string? UnresolvedInput)
{
    public bool Available => Rows is not null && UnresolvedInput is null;
}
