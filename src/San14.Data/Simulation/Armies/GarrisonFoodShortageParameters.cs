using San14.Core;

namespace San14.Data;

/// <summary>TC initial parameter table for Base food shortages. Installed at
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
        if (native.Parameters.Language != "TC") return state;
        var values = new Dictionary<string, int>(native.Parameters.Int32Values);
        foreach (var pair in Initial) values.Add(Prefix + pair.Name, pair.Value);
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
