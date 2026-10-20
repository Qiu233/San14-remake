using San14.Core;

namespace San14.Data;

/// <summary>Mutable saved repair coefficients. Only a fresh allocation installs
/// paired initial image data; restoring a missing key leaves it unknown.</summary>
public static class FacilityRepairParameters
{
    private const string Prefix = "facility-repair:";
    public const string TroopNormalization = Prefix + "troop-normalization";
    public const string BaseFactor = Prefix + "base-factor";
    public const string PoliticsScale = Prefix + "politics-scale";
    public const string PoliticsDivisor = Prefix + "politics-divisor";
    public const string FactorDivisor = Prefix + "factor-divisor";
    public const string TrapPercent = Prefix + "trap-percent";
    public const string ObstaclePercent = Prefix + "obstacle-percent";
    public const string TickDivisor = Prefix + "tick-divisor";
    public const string MinimumIncrement = Prefix + "minimum-increment";
    public const string TraitScale = Prefix + "trait-scale";
    public const string Policy42Scale = Prefix + "policy42-scale";
    public const string OtherMeritDivisor = Prefix + "other-merit-divisor";
    public const string ObstacleMeritDivisor = Prefix + "obstacle-merit-divisor";
    public const string TrapMeritDivisor = Prefix + "trap-merit-divisor";
    public const string MeritScale = Prefix + "merit-scale";
    public const string PoliticsGrowthPercent = Prefix + "politics-growth-percent";

    internal static GameState InstallFresh(GameState state)
    {
        var simulation = state.NativeArmySimulation ?? throw Missing("fresh_simulation");
        if (state.Units.Count != 0 || state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            simulation.Parameters.Language is not ("TC" or "SC")) throw Missing("fresh_allocation");
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values);
        foreach (var (key, value) in new (string, int)[] {
            (TroopNormalization, 30), (BaseFactor, 200), (PoliticsScale, 1),
            (PoliticsDivisor, 1), (FactorDivisor, 10), (TrapPercent, 8),
            (ObstaclePercent, 150), (TickDivisor, 12), (MinimumIncrement, 1),
            (TraitScale, 1), (Policy42Scale, 1), (OtherMeritDivisor, 50),
            (ObstacleMeritDivisor, 90), (TrapMeritDivisor, 2), (MeritScale, 9),
            (PoliticsGrowthPercent, 7500) })
            if (simulation.Parameters.Float32Values.ContainsKey(key) || !values.TryAdd(key, value))
                throw Missing("fresh_duplicate_parameter");
        return state with { NativeArmySimulation = simulation with
            { Parameters = simulation.Parameters with { Int32Values = values } } };
    }

    public static int? Read(GameState state, string key) =>
        state.NativeArmySimulation?.Parameters is { } parameters &&
        parameters.Language is "TC" or "SC" && !parameters.Float32Values.ContainsKey(key) &&
        parameters.Int32Values.TryGetValue(key, out int value) ? value : null;

    internal static int Require(GameState state, string key) => Read(state, key) ?? throw Missing(key);
    internal static InvalidDataException Missing(string field) => new("army.facility-repair." + field);
}
