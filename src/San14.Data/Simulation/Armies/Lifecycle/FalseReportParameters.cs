using San14.Core;
namespace San14.Data;

public static class FalseReportParameters
{
    public const string Prefix = "army-false-report:";
    internal static GameState InstallFresh(GameState state)
    {
        if (state.Units.Count != 0 || state.NativeClock is not { UpdateOrdinal: 0 } ||
            state.NativeArmySimulation is not { Parameters.Language: "SC" or "TC" } simulation)
            throw new InvalidDataException("False-report parameters require a new ordinary campaign.");
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values);
        foreach (var (name, value) in new (string, int)[] { ("version",1), ("target-morale",20), ("actor-morale",-20),
            ("confusion-duration",80), ("merit-troop-threshold",6000), ("merit-scale",100),
            ("success-intelligence-percent",175), ("success-minimum",5), ("success-maximum",70),
            ("backfire-intelligence-percent",125), ("backfire-minimum",5), ("backfire-maximum",70) })
            if (simulation.Parameters.Float32Values.ContainsKey(Prefix + name) || !values.TryAdd(Prefix + name, value))
                throw new InvalidDataException("False-report parameters already have an owner.");
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = values } } };
    }
}
