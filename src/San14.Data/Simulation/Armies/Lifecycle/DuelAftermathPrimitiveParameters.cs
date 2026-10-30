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

    //SC2B9DA0 reward and2E7650 readiness scalar operands are paired
    //independently with the actual SC data image, even where values match TC.
    private static readonly (string Name, int Value)[] FreshSc =
    [
        ("winner-strength-growth", 10000),
        ("loser-strength-growth", 2500),
        ("draw-strength-growth", 5000),
        ("winner-pending-merit", 120),
        ("draw-pending-merit", 60),
        ("readiness-health-cap:0", 100),
        ("readiness-health-cap:1", 80),
        ("readiness-health-cap:2", 50),
        ("readiness-health-cap:3", 30),
        ("readiness-strength-divisor", 3),
    ];

    //The SC complete zero-owner audit covers these exact independent fields,
    //including all possible covering RIP instruction starts and pointer escapes.
    private static readonly IReadOnlyDictionary<string, int> ScRewardLoaderDefaults =
        new Dictionary<string, int>
        {
            ["winner-difference-growth-cap"] = 0,
            ["winner-difference-growth-multiplier"] = 0,
        };

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
        var fresh = simulation.Parameters.Language == "SC" ? FreshSc : FreshTc;
        var loaderDefaults = simulation.Parameters.Language == "SC" ? ScRewardLoaderDefaults : TcRewardLoaderDefaults;
        foreach (var row in fresh) values.Add(Prefix + row.Name, row.Value);
        foreach (var (name, value) in loaderDefaults) values.Add(Prefix + name, value);
        return state with { NativeArmySimulation = simulation with
            { Parameters = simulation.Parameters with { Int32Values = values } } };
    }

    internal static int Read(GameState state, string name)
    {
        string key = Prefix + name;
        if (state.NativeArmySimulation?.Parameters is not { Language: "TC" or "SC" } bank ||
            bank.Float32Values.ContainsKey(key)) throw Missing("parameter_" + key);
        if (bank.Int32Values.TryGetValue(key, out int value)) return value;
        //Only the independently proved locale-specific loader-zero rules
        //cover absent keys in older saves. Loading never reinstalls the fresh
        //nonzero family or changes the saved bank.
        var loaderDefaults = bank.Language == "SC" ? ScRewardLoaderDefaults : TcRewardLoaderDefaults;
        if (bank.RuleVersion == SimulationRuleIdentity.ForLocale(bank.Language) &&
            loaderDefaults.TryGetValue(name, out value)) return value;
        throw Missing("parameter_" + key);
    }

    internal static InvalidDataException Missing(string field) => new("current.DuelOpportunity_aftermath_" + field);
}
