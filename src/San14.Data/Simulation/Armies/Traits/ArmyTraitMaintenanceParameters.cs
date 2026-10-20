using San14.Core;

namespace San14.Data;

/// <summary>Two saved maintenance thresholds. Fresh installation alone supplies
/// the paired initial values; missing restored knowledge stays unknown.</summary>
internal static class ArmyTraitMaintenanceParameters
{
    internal const string AdjacentEnemyCountKey = "army-traits:adjacent-enemy-count";
    internal const string NearbyArmyCountKey = "army-traits:nearby-army-count";
    internal static GameState InstallFresh(GameState state)
    {
        var simulation = state.NativeArmySimulation ?? throw Missing("fresh_simulation");
        if (state.Units.Count != 0 || state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            simulation.Parameters.Language is not ("TC" or "SC")) throw Missing("fresh_allocation");
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values);
        foreach (var (key, value) in new[] { (AdjacentEnemyCountKey, 2), (NearbyArmyCountKey, 3) })
            if (simulation.Parameters.Float32Values.ContainsKey(key) || !values.TryAdd(key, value))
                throw Missing("fresh_duplicate_parameter");
        return state with { NativeArmySimulation = simulation with
            { Parameters = simulation.Parameters with { Int32Values = values } } };
    }
    internal static int Require(GameState state, string key) =>
        state.NativeArmySimulation?.Parameters is { Language: "TC" or "SC" } parameters &&
        !parameters.Float32Values.ContainsKey(key) && parameters.Int32Values.TryGetValue(key, out int value)
            ? value : throw Missing(key);
    private static InvalidDataException Missing(string field) => new("army.trait-maintenance.parameter." + field);
}
