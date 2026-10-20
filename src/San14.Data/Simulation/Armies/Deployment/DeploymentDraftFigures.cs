using San14.Core;

namespace San14.Data;

public enum DeploymentTroopMarker { Default, AtSecondaryThreshold, BelowPrimaryThreshold }
public sealed record DeploymentCombatAxes(int FieldAttack, int SiegeTroopAttack,
    int WallAttack, int Mobility, int Defense);
public sealed record DeploymentDraftFigures(int OfficerId, DeploymentTroopMarker? TroopMarker,
    DeploymentCombatAxes? WithoutConditionalModifiers, DeploymentCombatAxes? WithConditionalModifiers,
    string? MarkerUnresolvedInput, string? WithoutModifiersUnresolvedInput, string? WithModifiersUnresolvedInput);

/// <summary>Finite CE preview rule parameter, independent of scenario stock or
/// the current Formation thresholds. Never repairs omitted saved knowledge.</summary>
public static class DeploymentDraftFigureRules
{
    public const string TroopThresholdEffectScaleKey = "simulation.deployment.troop-threshold-effect-scale";
    public const string NeighborRadiusKey = "simulation.deployment.preview-neighbor-radius";
    internal static GameState InstallFresh(GameState state)
    {
        var simulation = state.NativeArmySimulation ?? throw new InvalidDataException("Fresh preview rule owner is missing.");
        if (simulation.Parameters.Int32Values.ContainsKey(TroopThresholdEffectScaleKey) ||
            simulation.Parameters.Int32Values.ContainsKey(NeighborRadiusKey))
            throw new InvalidDataException("Fresh preview rules cannot replace current values.");
        // Paired inspected CE rule: TC2B4F00 / SC2B4D40.
        //Neighbor radius: paired TC2AFC20 / SC2AFA40, both CE profiles2.
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values)
            { [TroopThresholdEffectScaleKey] = 1, [NeighborRadiusKey] = 2 };
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = values } } };
    }
}
