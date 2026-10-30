using San14.Core;

namespace San14.Data;

/// <summary>Independent SC/TC initial tables for Base food shortages. Installed at
/// new-campaign creation only; restored banks are never repaired by a query.</summary>
internal static class GarrisonFoodShortageParameters
{
    private const string Prefix = "simulation.garrison-food-shortage.";
    private static readonly (string Name, int Value)[] Initial =
    [ ("random-bound", 200), ("minimum-troop-coefficient", 400), ("troop-rate", 30),
      ("troop-charm-divisor", 15), ("morale-rate", 14), ("morale-charm-divisor", 20) ];

    internal static GameState InstallFresh(GameState state)
    {
        var native = state.NativeArmySimulation ?? throw new InvalidDataException("garrison_food.parameters_owner");
        if (native.Parameters.Language is not ("TC" or "SC")) return state;
        if (native.Parameters.RuleVersion != SimulationRuleIdentity.ForLocale(native.Parameters.Language) ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.Units.Count != 0)
            throw new InvalidDataException("Garrison food parameters require a fresh matching CE campaign.");
        //TC2CCE70/SC2CCCF0 have the same complete shortage algorithm;
        //all six values were independently read from each initial image.
        var values = new Dictionary<string, int>(native.Parameters.Int32Values);
        foreach (var pair in Initial)
        {
            string key = Prefix + pair.Name;
            if (native.Parameters.Float32Values.ContainsKey(key))
                throw new InvalidDataException("Garrison food parameter has a different numeric owner: " + key);
            values.TryAdd(key, pair.Value);
        }
        return state with { NativeArmySimulation = native with
            { Parameters = native.Parameters with { Int32Values = values } } };
    }

    internal static int Read(GameState state, string name)
    {
        string key = Prefix + name;
        var owner = state.NativeArmySimulation?.Parameters;
        if (owner is null || owner.Float32Values.ContainsKey(key) ||
            !owner.Int32Values.TryGetValue(key, out int value))
            throw new InvalidDataException("garrison_food.parameter." + name);
        return value;
    }
}
