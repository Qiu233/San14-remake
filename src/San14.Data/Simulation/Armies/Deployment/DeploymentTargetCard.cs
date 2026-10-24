namespace San14.Data;

/// <summary>One ordinary newly created target-page card. HasCurrentHex=false
/// is a known null position, not a missing input. A null surface selection in
/// that case must not select terrain0 or a fallback formation.</summary>
public sealed record DeploymentTargetCard(int OfficerId, bool? HasCurrentHex,
    int? CurrentHex, bool? UsesNavalFormation, int? GrainPalette,
    string? PositionUnresolvedInput, string? SurfaceUnresolvedInput,
    string? GrainUnresolvedInput)
{
    public bool? IsPersonValid { get; init; }
    public string? PersonUnresolvedInput { get; init; }
    public ArmyMapIconSelection? FormationIcon { get; init; }
    public string? FormationUnresolvedInput { get; init; }
    // The actual new-row constructor sets this before the target is chosen.
    public bool? UsesHomeGrain { get; init; } = true;
}
