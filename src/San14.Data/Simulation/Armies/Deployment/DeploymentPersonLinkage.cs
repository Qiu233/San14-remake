namespace San14.Data;

public sealed record DeploymentPersonLinkageRow(
    int OfficerId, IReadOnlyList<DeploymentPersonBasicValue<byte>> Tiers);
public sealed record DeploymentPersonLinkageQuery(
    IReadOnlyList<int?>? SelectedOfficerIds,
    IReadOnlyList<DeploymentPersonLinkageRow>? Rows, string? UnresolvedInput)
{
    public bool Available => SelectedOfficerIds is not null && Rows is not null && UnresolvedInput is null;
}
