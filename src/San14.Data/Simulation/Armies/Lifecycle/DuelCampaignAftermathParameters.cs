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
    // SC377920 ->379B20 selects the independently paired business callbacks.
    // Their actual SC1A4B3A0/B0/B8/C0/C8/D0 data words establish this table;
    // equality with TC does not supply the SC source contract.
    private static readonly (string Name, int Value)[] FreshSc =
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
        if (simulation.Parameters.Language is not ("TC" or "SC")) return state;
        if (simulation.Parameters.Language == "SC" && simulation.Parameters.RuleVersion != SimulationRuleIdentity.ForLocale("SC"))
            throw Missing("fresh_sc_rule_profile");
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values);
        foreach (var row in simulation.Parameters.Language == "SC" ? FreshSc : FreshTc)
            values.Add(Prefix + row.Name, row.Value);
        return state with { NativeArmySimulation = simulation with
            { Parameters = simulation.Parameters with { Int32Values = values } } };
    }
    internal static int Read(GameState state, string name) => state.NativeArmySimulation?.Parameters is { } bank &&
        !bank.Float32Values.ContainsKey(Prefix + name) && bank.Int32Values.TryGetValue(Prefix + name, out int value)
        ? value : throw Missing("parameter_" + name);
    internal static InvalidDataException Missing(string field) => new("current.DuelAftermath_" + field);
}
