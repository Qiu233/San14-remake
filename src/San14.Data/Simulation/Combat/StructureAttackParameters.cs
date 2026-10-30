using San14.Core;

namespace San14.Data;

internal static class StructureAttackParameters
{
    // Independently extracted from each locale's installed scalar bank.
    private const int FreshTraditionalLossScale = 10;
    private const int FreshSimplifiedLossScale = 10;

    internal static GameState InstallFresh(GameState state, string locale)
    {
        // The structure kind11 and wall kind5/6 callbacks share this loss
        // coefficient in each locale; it is not an Army fallback.
        if (locale is not ("TC" or "SC")) return state;
        int lossScale = locale == "TC" ? FreshTraditionalLossScale : FreshSimplifiedLossScale;
        var simulation = state.NativeArmySimulation ?? throw new InvalidDataException("Structure attack parameter owner is unavailable.");
        string key = StructureDestructionForceResult.Kind11LossScale;
        if (simulation.Parameters.Float32Values.ContainsKey(key) || simulation.Parameters.Int32Values.ContainsKey(key))
            throw new InvalidDataException("Fresh structure attack parameters cannot overwrite a current value.");
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values, simulation.Parameters.Int32Values.Comparer) { [key] = lossScale };
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = values } } };
    }
}
