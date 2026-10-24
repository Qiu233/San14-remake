using San14.Core;

namespace San14.Data;

/// <summary>TC initial-table food parameters, installed only in a new campaign.
/// No query repairs an older parameter bank. SC retains its declared legacy
/// expense rule until its corresponding profile has been established.</summary>
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
        if (native.Parameters.Language != "TC") return state;
        if (state.NativeClock is not { UpdateOrdinal: 0 } || state.Units.Count != 0)
            throw new InvalidDataException("Food parameters require a fresh campaign.");
        var values = new Dictionary<string, int>(native.Parameters.Int32Values);
        values.Add(Version, 1);
        foreach (var pair in Initial) values.Add(Prefix + pair.Name, pair.Value);
        // 227070 constructs the placeholder Force with its policy activation
        // bitmap clear. Its source row does not serialize that later bitmap;
        // normal policy activation only handles actual playable Forces.
        values.Add(Prefix + "unassigned-territory-policy-enabled", 0);
        return state with { NativeArmySimulation = native with { Parameters = native.Parameters with { Int32Values = values } } };
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
