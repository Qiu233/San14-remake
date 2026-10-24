using San14.Core;

namespace San14.Data;

internal static class DuelCampaignAftermathParameters
{
    private const string Prefix = "duel-campaign-aftermath:";
    private static readonly (string Name, int Value)[] FreshTc =
    [
        ("loser-injury-readiness-threshold", 75), ("loser-morale-loss", 50),
        ("winner-injury-readiness-threshold", 30), ("winner-morale-gain", 40),
        ("draw-injury-readiness-threshold", 50), ("draw-morale-gain", 15)
    ];
    internal static GameState InstallFresh(GameState state, LoadedScenario source)
    {
        if (state.Units.Count != 0 || state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            state.NativeArmySimulation is not { } simulation || source.Metadata.FormatMarker != 0x66 ||
            simulation.Parameters.Language != source.Snapshot.Locale ||
            state.ContentHashes.GetValueOrDefault(source.ScenarioSource.ResourceUri) != source.ScenarioSource.Sha256 ||
            simulation.Parameters.Int32Values.Keys.Concat(simulation.Parameters.Float32Values.Keys)
                .Any(key => key.StartsWith(Prefix, StringComparison.Ordinal)))
            throw Missing("fresh_owner");
        if (simulation.Parameters.Language != "TC") return state;
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values);
        foreach (var row in FreshTc) values.Add(Prefix + row.Name, row.Value);
        return state with { NativeArmySimulation = simulation with
            { Parameters = simulation.Parameters with { Int32Values = values } } };
    }
    internal static int Read(GameState state, string name) => state.NativeArmySimulation?.Parameters is { } bank &&
        !bank.Float32Values.ContainsKey(Prefix + name) && bank.Int32Values.TryGetValue(Prefix + name, out int value)
        ? value : throw Missing("parameter_" + name);
    internal static InvalidDataException Missing(string field) => new("current.DuelAftermath_" + field);
}
