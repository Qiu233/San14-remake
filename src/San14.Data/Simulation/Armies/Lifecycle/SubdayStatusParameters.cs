using San14.Core;

namespace San14.Data;

internal static class SubdayStatusParameters
{
    internal static GameState InstallFresh(GameState state)
    {
        var simulation = state.NativeArmySimulation ?? throw new InvalidDataException("Status parameters require an owned campaign.");
        if (state.Units.Count != 0 || state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            simulation.Parameters.Language is not ("SC" or "TC")) throw new InvalidDataException("Status parameters require a fresh campaign.");
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values);
        foreach (var (key, value) in new (string, int)[] {
            (SimulationParameterKeys.SubdayStatus.BaseAbnormalUpper, 120),
            (SimulationParameterKeys.SubdayStatus.BaseCooldownUpper, 100),
            (SimulationParameterKeys.SubdayStatus.OfficerRumorDecay, 1) })
            if (simulation.Parameters.Float32Values.ContainsKey(key) || !values.TryAdd(key, value))
                throw new InvalidDataException("Status parameters already have an owner.");
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = values } } };
    }
}
