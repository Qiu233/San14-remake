using San14.Core;

namespace San14.Data;

internal static class HexFireParameters
{
    /// <summary>Offline paired CE data, installed only by fresh construction.</summary>
    internal static GameState InstallFresh(GameState state)
    {
        if (state.NativeArmySimulation is not { } simulation || state.Units.Count != 0 ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            simulation.Parameters.Language is not ("SC" or "TC"))
            throw new InvalidDataException("Fire parameters require a fresh ordinary campaign.");
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values);
        foreach (var (key, value) in new (string, int)[]
        {
            (SimulationParameterKeys.HexFire.Decay, 1),
            (SimulationParameterKeys.HexFire.CategoryOneSpread, 9),
            (SimulationParameterKeys.HexFire.CategoryThreeSpread, 3),
            (SimulationParameterKeys.HexFire.OtherCategorySpread, 18),
            (SimulationParameterKeys.HexFire.SpreadDivisor, 800),
            (SimulationParameterKeys.HexFire.DurationPercentBase, 30),
            (SimulationParameterKeys.HexFire.DurationRandomBound, 50)
        })
            if (simulation.Parameters.Float32Values.ContainsKey(key) || !values.TryAdd(key, value))
                throw new InvalidDataException("Fire parameters already have an owner.");
        return state with { NativeArmySimulation = simulation with
            { Parameters = simulation.Parameters with { Int32Values = values } } };
    }
}
