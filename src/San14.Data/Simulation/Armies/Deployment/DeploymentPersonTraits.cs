namespace San14.Data;

public readonly record struct DeploymentPersonTraitValue(int PhysicalTraitId, string Name);

public sealed record DeploymentPersonTraitRow(int OfficerId,
    IReadOnlyList<DeploymentPersonBasicValue<DeploymentPersonTraitValue>> Traits);

public sealed record DeploymentPersonTraitQuery(
    IReadOnlyList<DeploymentPersonTraitRow>? Rows, string? UnresolvedInput)
{
    public bool Available => Rows is not null && UnresolvedInput is null;
}
