using San14.Core;

namespace San14.Data;

/// <summary>Adapts current source status and location ownership at the reward checkpoint.
/// The objective itself belongs to the force domain, not the source-layout bank.</summary>
internal sealed class ForceCombatObjectiveCurrent(GameState state, int forceId,
    SourceWorldContext definitions, INormalArmyForceResultDefinitionQueries registry,
    Func<ForceCombatRewardParameters?> parameters, bool? knownAttackerValidity = null) : IForceCombatObjectiveQueries
{
    private NativeArmyForceState? Status => state.NativeArmySimulation?.Forces.GetValueOrDefault(forceId);
    private ForceCombatObjective? Objective => state.Forces.GetValueOrDefault(forceId)?.CombatObjective;
    public bool? ReadAttackerValid() => knownAttackerValidity ?? definitions.ReadForceNativeValidity(new(forceId));
    public bool? ReadSelectedForceTrackingEnabled() => state.NativeArmySimulation?.World.Flags165C is byte flags
        ? flags > 0 : null;
    public bool? ReadAttackerTracksSelectedForce() => Status?.Flags12 is byte flags ? (flags & 6) == 2 : null;
    public int? ReadSelectedForceId() => state.NativeArmySimulation?.World.SelectedForce1656;
    public bool? ReadObjectiveEnabled() => Objective?.Enabled;
    public int? ReadObjectiveProgress() => Objective?.Progress;
    public int? ReadObjectiveTargetLocationId() => Objective?.TargetLocationId;
    public int? ResolveObjectiveTargetForceId(int locationId) => registry.ReadResultDefinitionIdentity(locationId);
    public ForceCombatRewardParameters? ReadRewardParameters() => parameters();
}
