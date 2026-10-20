using San14.Core;
namespace San14.Data;

internal static class FireCommandParameters
{
    internal static GameState InstallFresh(GameState state)
    {
        if (state.Units.Count != 0 || state.NativeClock is not { UpdateOrdinal: 0 } ||
            state.NativeArmySimulation is not { Parameters.Language: "SC" or "TC" } simulation)
            throw new InvalidDataException("Fire command parameters require an unused ordinary campaign.");
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values);
        foreach (var (key, value) in new (string, int)[] {
            (SimulationParameterKeys.FireCommand.DurationBase,36), (SimulationParameterKeys.FireCommand.IntelligenceDivisor,150),
            (SimulationParameterKeys.FireCommand.DurationPolicyScale,1), (SimulationParameterKeys.FireCommand.SuccessMerit,80),
            (SimulationParameterKeys.FireCommand.FailureMorale,2), (SimulationParameterKeys.FireCommand.TerrainZeroRate,5),
            (SimulationParameterKeys.FireCommand.TerrainOneRate,10), (SimulationParameterKeys.FireCommand.TerrainTwoRate,25),
            (SimulationParameterKeys.FireCommand.TerrainThreeRate,5), (SimulationParameterKeys.FireCommand.MinimumRate,5),
            (SimulationParameterKeys.FireCommand.TraitRateScale,1), (SimulationParameterKeys.FireCommand.BackfireBase,25),
            (SimulationParameterKeys.FireCommand.BackfireMinimum,1),
            (SimulationParameterKeys.FireCommand.AbnormalTargetBonus,20),
            (SimulationParameterKeys.FireCommand.BackfireDefenseScale,35),
            (SimulationParameterKeys.FireCommand.RelationAdjustment,-5),
            (SimulationParameterKeys.ForceRelations.LevelThreshold(1),1),
            (SimulationParameterKeys.ForceRelations.LevelThreshold(2),30),
            (SimulationParameterKeys.ForceRelations.LevelThreshold(3),50),
            (SimulationParameterKeys.ForceRelations.LevelThreshold(4),70),
            (SimulationParameterKeys.ForceRelations.LevelThreshold(5),100) })
            if (simulation.Parameters.Float32Values.ContainsKey(key) || !values.TryAdd(key,value))
                throw new InvalidDataException("Fire command parameters already have an owner.");
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = values } } };
    }
}
