using San14.Core;

namespace San14.Data;

public sealed record TransportArmyRouteMobilityReceipt(float Value, int FormationPhysical,
    int ForcePhysical, int CommanderPhysical, byte CurrentHexSurface10, byte ExplicitHexSurface10,
    int Coefficient, int FormationPercent, int UnconditionalTrait94, float TraitFactor,
    float BaselineTerrainFactor, float FormationTerrainFactor, float ForceTerrainFactor,
    float EnvironmentFactor, int? PolicyIndex, int PolicyValue, bool AdvancedScalingApplied,
    ForcePolicyScratchReceipt? FormationPolicyQuery);

public sealed record TransportArmyRouteMobilityResult(float? Value,
    TransportArmyRouteMobilityReceipt? Receipt, string? UnresolvedOwner)
{ public bool Complete => Value.HasValue; }

/// <summary>Compatibility entry for the already supported transport caller.</summary>
public static class TransportArmyRouteMobilitySource
{
    public static TransportArmyRouteMobilityResult Read(GameState state, SourceWorldContext current,
        UnitState army, int formationPhysical, byte explicitHexSurface10) =>
        army.NativeTacticRuntime?.Status10 != 2 ? new(null, null, "current.transport_route_mobility.branch.non_type2") :
        ArmyRouteMobilitySource.Read(state, current, army, formationPhysical, explicitHexSurface10);
}
