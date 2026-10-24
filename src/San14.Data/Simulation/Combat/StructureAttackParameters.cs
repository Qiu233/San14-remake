using San14.Core;

namespace San14.Data;

internal static class StructureAttackParameters
{
    internal static GameState InstallFresh(GameState state, string locale)
    {
        //Existing TC typed/data.tsv records the disk initial49920=10.
        //This is its distinct kind11 loss coefficient, not an Army fallback.
        if (locale != "TC") return state;
        var simulation = state.NativeArmySimulation ?? throw new InvalidDataException("Structure attack parameter owner is unavailable.");
        string key = StructureDestructionForceResult.Kind11LossScale;
        if (simulation.Parameters.Float32Values.ContainsKey(key) || simulation.Parameters.Int32Values.ContainsKey(key))
            throw new InvalidDataException("Fresh structure attack parameters cannot overwrite a current value.");
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values, simulation.Parameters.Int32Values.Comparer) { [key] = 10 };
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = values } } };
    }
}
