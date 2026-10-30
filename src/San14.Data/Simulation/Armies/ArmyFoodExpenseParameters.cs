using San14.Core;

namespace San14.Data;

/// <summary>Independently sourced SC/TC food parameters, installed only in a
/// new campaign. No query repairs an older parameter bank.</summary>
internal static class ArmyFoodExpenseParameters
{
    internal const string Version = "simulation.army-food.version";
    private const string Prefix = "simulation.army-food.";
    private static readonly (string Name, int Value)[] Initial =
    [ ("troop-numerator", 40), ("duration-growth", 3), ("terrain-correction", 75),
      ("policy-scale", 1), ("trait-scale", 1), ("super-territory-percent", 150), ("top-territory-percent", 200) ];

    internal static GameState InstallFresh(GameState state)
    {
        var native = state.NativeArmySimulation ?? throw new InvalidDataException("Food parameters lack their current owner.");
        if (native.Parameters.Language is not ("TC" or "SC")) return state;
        if (native.Parameters.RuleVersion != SimulationRuleIdentity.ForLocale(native.Parameters.Language) ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.Units.Count != 0)
            throw new InvalidDataException("Food parameters require a fresh matching CE campaign.");
        //TC269A50/SC269750 share the complete arithmetic. Each language's
        //seven initial values were read from its own actual RIP references.
        var values = new Dictionary<string, int>(native.Parameters.Int32Values);
        Install(Version, 1);
        if (values[Version] != 1) throw new InvalidDataException("Food parameter profile is unsupported.");
        foreach (var pair in Initial) Install(Prefix + pair.Name, pair.Value);
        // TC227070/SC2272E0 construct the placeholder Force with its policy activation
        // bitmap clear. Its source row does not serialize that later bitmap;
        // normal policy activation only handles actual playable Forces.
        Install(Prefix + "unassigned-territory-policy-enabled", 0);
        return state with { NativeArmySimulation = native with { Parameters = native.Parameters with { Int32Values = values } } };

        void Install(string key, int initial)
        {
            if (native.Parameters.Float32Values.ContainsKey(key))
                throw new InvalidDataException("Food parameter has a different numeric owner: " + key);
            values.TryAdd(key, initial); // Explicit current integers remain authoritative.
        }
    }

    internal static int Read(GameState state, string name)
    {
        var p = state.NativeArmySimulation?.Parameters;
        string key = Prefix + name;
        if (p is null || p.Int32Values.GetValueOrDefault(Version) != 1 || p.Float32Values.ContainsKey(key) ||
            !p.Int32Values.TryGetValue(key, out int value))
            throw new InvalidDataException("food_upkeep.parameter." + name);
        return value;
    }
}
