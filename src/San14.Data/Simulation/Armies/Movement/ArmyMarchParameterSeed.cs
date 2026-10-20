using San14.Core;

namespace San14.Data;

/// <summary>Offline recovered ordinary march scalars. The terrain minimum is
/// an uninitialized cache until the reached path preparation initializes it.</summary>
public static class ArmyMarchParameterSeed
{
    private static readonly string[] OccupationKeys = [SimulationParameterKeys.ArmyMarch.OccupationTraitScale,
        SimulationParameterKeys.ArmyMarch.OccupationGrowthScale, SimulationParameterKeys.ArmyMarch.OccupationMeritScale];
    public static GameState InstallFresh(GameState state)
    {
        if (state.NativeArmySimulation is not { } simulation ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.Units.Count != 0 ||
            simulation.Parameters.Language is not ("SC" or "TC") ||
            simulation.Parameters.RuleVersion != SimulationRuleIdentity.Version ||
            simulation.Parameters.Int32Values.ContainsKey(SimulationParameterKeys.ArmyMarch.SpeedScale) ||
            simulation.Parameters.Int32Values.ContainsKey(SimulationParameterKeys.ArmyMarch.MinimumTerrainCost) ||
            simulation.Parameters.Int32Values.ContainsKey(SimulationParameterKeys.ArmyMarch.DevelopmentLossMultiplier) ||
            simulation.Parameters.Float32Values.ContainsKey(SimulationParameterKeys.ArmyMarch.SpeedScale) ||
            simulation.Parameters.Float32Values.ContainsKey(SimulationParameterKeys.ArmyMarch.MinimumTerrainCost) ||
            simulation.Parameters.Float32Values.ContainsKey(SimulationParameterKeys.ArmyMarch.DevelopmentLossMultiplier) ||
            OccupationKeys.Any(key => simulation.Parameters.Int32Values.ContainsKey(key) || simulation.Parameters.Float32Values.ContainsKey(key)))
            throw new InvalidDataException("Fresh march parameters cannot replace current or omitted saved owners.");
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values)
        {
            [SimulationParameterKeys.ArmyMarch.SpeedScale] = 100,
            [SimulationParameterKeys.ArmyMarch.MinimumTerrainCost] = int.MaxValue,
            //Offline SC/TC image initial scalar; later calls consume this saved owner.
            [SimulationParameterKeys.ArmyMarch.DevelopmentLossMultiplier] = 1,
            [SimulationParameterKeys.ArmyMarch.OccupationTraitScale] = 1,
            [SimulationParameterKeys.ArmyMarch.OccupationGrowthScale] = 7,
            [SimulationParameterKeys.ArmyMarch.OccupationMeritScale] = 3
        };
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = values } } };
    }
}
