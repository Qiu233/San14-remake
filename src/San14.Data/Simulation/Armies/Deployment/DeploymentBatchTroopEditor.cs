namespace San14.Data;

/// <summary>The shared permission and calculator values for ordinary Make's
/// batch troop controls. Disabled controls do not need commander capacities.</summary>
public sealed record DeploymentBatchTroopEditor(bool? Enabled, int? InitialValue,
    int? Minimum, int? Maximum, string? UnresolvedInput)
{
    public bool Available => UnresolvedInput is null;
}
