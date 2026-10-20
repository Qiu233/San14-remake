using San14.Core;

namespace San14.Data;

internal static class BaseWoundedRecoveryParameters
{
    // Paired SC/TC data recovered offline. These are only fresh allocation
    // values; old or incomplete saved parameter owners are never repaired.
    internal static GameState InstallFresh(GameState state)
    {
        if (state.NativeArmySimulation is not { } simulation || state.Units.Count != 0 ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            simulation.Parameters.Language is not ("SC" or "TC"))
            throw new InvalidDataException("Wounded recovery parameters require a fresh ordinary campaign.");
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values);
        foreach (var (key, value) in new (string, int)[]
        {
            (SimulationParameterKeys.BaseWoundedRecovery.SlowAmount, 100),
            (SimulationParameterKeys.BaseWoundedRecovery.NormalAmount, 150),
            (SimulationParameterKeys.BaseWoundedRecovery.FastAmount, 200),
            (SimulationParameterKeys.BaseWoundedRecovery.WoundedDivisor, 400),
            (SimulationParameterKeys.BaseWoundedRecovery.RecoveredMorale, 40),
            (SimulationParameterKeys.BaseWoundedRecovery.TraitMultiplier, 1)
        })
            if (simulation.Parameters.Float32Values.ContainsKey(key) || !values.TryAdd(key, value))
                throw new InvalidDataException("Wounded recovery parameters already have an owner.");
        return state with { NativeArmySimulation = simulation with
            { Parameters = simulation.Parameters with { Int32Values = values } } };
    }
}
