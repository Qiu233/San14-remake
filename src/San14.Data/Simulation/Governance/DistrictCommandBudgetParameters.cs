using San14.Core;

namespace San14.Data;

/// <summary>CE SC/TC rule data extracted offline. Mutable coefficients have a
/// fresh allocation value, then belong exclusively to the saved parameter bank.</summary>
public static class DistrictCommandBudgetParameters
{
    private const string Prefix = "ce-district-command-budget:";
    public static string Key(string name) => Prefix + name;
    private static readonly (string Name, int Value)[] Fresh =
    [
        ("version", 1), ("base", 10), ("cityCoefficient", 5),
        ("politicsDivisor", 1), ("personDivisor", 1), ("upperBound", 200),
        ("effectMultiplier", 1), ("areaDivisor", 10), ("areaMultiplier", 1),
        ("minimumReturnLimit", 50),
        ("politicsPercent", 0), ("personPercent", 0), ("cityMultiplier", 0)
    ];

    public static GameState InstallFresh(GameState state)
    {
        var simulation = state.NativeArmySimulation ?? throw new InvalidDataException("District budget current owner is missing.");
        if (simulation.Parameters.Language is not ("SC" or "TC") ||
            simulation.Parameters.RuleVersion != SimulationRuleIdentity.ForLocale(simulation.Parameters.Language) ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            simulation.Parameters.Int32Values.Keys.Any(key => key.StartsWith(Prefix, StringComparison.Ordinal)))
            throw new InvalidDataException("District budgets require their unused fresh CE owner.");
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values);
        foreach (var (name, value) in Fresh) values.Add(Key(name), value);
        return state with { NativeArmySimulation = simulation with
            { Parameters = simulation.Parameters with { Int32Values = values } } };
    }
}
