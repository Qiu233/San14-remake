using San14.Core;

namespace San14.Data;

/// <summary>Independent fresh catalog for duel rewards and readiness recovery.
/// Queries consume the owned saved bank; no original code/data is read at runtime.</summary>
public static class DuelAftermathPrimitiveParameters
{
    private const string Prefix = "duel-aftermath:";
    private static readonly (string Name, int Value)[] FreshTc =
    [
        ("winner-strength-growth", 10000), ("loser-strength-growth", 2500), ("draw-strength-growth", 5000),
        ("winner-pending-merit", 120), ("draw-pending-merit", 60),
        ("readiness-health-cap:0", 100), ("readiness-health-cap:1", 80),
        ("readiness-health-cap:2", 50), ("readiness-health-cap:3", 30),
        ("readiness-strength-divisor", 3)
    ];

    //These two TC scalars have loader-zero initialization, not sampled
    //campaign defaults. The complete cached scalar-block audit contains only
    //reads and no address escape, with no parameter-load or save writer in
    //the existing program artifacts. Saved integer overrides retain precedence.
    private static readonly IReadOnlyDictionary<string, int> TcRewardLoaderDefaults =
        new Dictionary<string, int>
        {
            ["winner-difference-growth-cap"] = 0,
            ["winner-difference-growth-multiplier"] = 0,
        };

    public static GameState InstallFresh(GameState state, LoadedScenario source)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        if (state.Units.Count != 0 || state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            state.NativeArmySimulation is not { } simulation || source.Metadata.FormatMarker != 0x66 ||
            source.Snapshot.Locale != simulation.Parameters.Language || simulation.Parameters.Language is not ("TC" or "SC") ||
            state.ContentHashes.GetValueOrDefault(source.ScenarioSource.ResourceUri) != source.ScenarioSource.Sha256 ||
            simulation.Parameters.Int32Values.Keys.Concat(simulation.Parameters.Float32Values.Keys)
                .Any(key => key.StartsWith(Prefix, StringComparison.Ordinal)))
            throw Missing("fresh_owner");
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values) { [Prefix + "version"] = 1 };
        if (simulation.Parameters.Language == "TC")
        {
            foreach (var row in FreshTc) values.Add(Prefix + row.Name, row.Value);
            foreach (var (name, value) in TcRewardLoaderDefaults) values.Add(Prefix + name, value);
        }
        return state with { NativeArmySimulation = simulation with
            { Parameters = simulation.Parameters with { Int32Values = values } } };
    }

    internal static int Read(GameState state, string name)
    {
        string key = Prefix + name;
        if (state.NativeArmySimulation?.Parameters is not { Language: "TC" or "SC" } bank ||
            bank.Float32Values.ContainsKey(key)) throw Missing("parameter_" + key);
        if (bank.Int32Values.TryGetValue(key, out int value)) return value;
        //Only these immutable TC rules cover an absent key in an older save.
        //Loading does not reinstall the fresh family or change the saved bank.
        if (bank.Language == "TC" && bank.RuleVersion == SimulationRuleIdentity.ForLocale("TC") &&
            TcRewardLoaderDefaults.TryGetValue(name, out value)) return value;
        throw Missing("parameter_" + key);
    }

    internal static InvalidDataException Missing(string field) => new("current.DuelOpportunity_aftermath_" + field);
}
