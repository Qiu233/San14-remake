using San14.Core;

namespace San14.Data;

internal static class DuelOpportunityParameters
{
    internal const string MaximumOpportunities = "duel-opportunity:maximum-opportunities";
    internal const string MinimumEffectiveStrength = "duel-opportunity:minimum-effective-strength";

    // Offline paired image initial values. These are installed once for the
    // actual fresh profile; current queries consume only the saved bank.
    internal static GameState InstallFresh(GameState state)
    {
        var simulation = state.NativeArmySimulation ?? throw Missing("parameter_owner");
        var bank = simulation.Parameters;
        if (bank.Language is not ("TC" or "SC") || bank.Int32Values.ContainsKey(MaximumOpportunities) ||
            bank.Int32Values.ContainsKey(MinimumEffectiveStrength) ||
            bank.Float32Values.ContainsKey(MaximumOpportunities) || bank.Float32Values.ContainsKey(MinimumEffectiveStrength))
            throw Missing("fresh_parameter_owner");
        var values = new Dictionary<string, int>(bank.Int32Values)
        { [MaximumOpportunities] = 2, [MinimumEffectiveStrength] = 65 };
        return state with { NativeArmySimulation = simulation with { Parameters = bank with { Int32Values = values } } };
    }

    internal static int Read(GameState state, string key) =>
        state.NativeArmySimulation?.Parameters is { Language: "TC" or "SC" } parameters &&
        !parameters.Float32Values.ContainsKey(key) && parameters.Int32Values.TryGetValue(key, out int value)
            ? value : throw Missing("parameter_" + key);
    internal static InvalidDataException Missing(string field) => new("current.DuelOpportunity_" + field);
}
