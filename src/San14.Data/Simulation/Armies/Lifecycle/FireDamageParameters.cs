using San14.Core;

namespace San14.Data;

/// <summary>Paired initial coefficients of the finite burning-Hex consumer.
/// Current saved keys are read only at their actual use; old saves are not repaired.</summary>
internal static class FireDamageParameters
{
    private const string Prefix = "fire-damage:";
    internal const string CityDivisor = Prefix + "city-divisor", GateDivisor = Prefix + "gate-divisor",
        LossRandomBound = Prefix + "loss-random-bound", ReadyFixed = Prefix + "ready-fixed",
        ReadyPercent = Prefix + "ready-percent", ReadyDenominator = Prefix + "ready-denominator",
        TraitReductionScale = Prefix + "trait-reduction-scale", PermanentTraitMultiplier = Prefix + "permanent-trait-multiplier",
        ReadyNormalPercent = Prefix + "ready-normal-percent", WoundedFixed = Prefix + "wounded-fixed",
        WoundedPercent = Prefix + "wounded-percent", WoundedDenominator = Prefix + "wounded-denominator",
        DurabilityRandomBound = Prefix + "durability-random-bound", DurabilityFixed = Prefix + "durability-fixed",
        DurabilityNormalPercent = Prefix + "durability-normal-percent";

    internal static GameState InstallFresh(GameState state)
    {
        var simulation = state.NativeArmySimulation ?? throw Missing("fresh_simulation");
        if (state.Units.Count != 0 || state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            simulation.Parameters.Language is not ("TC" or "SC")) throw Missing("fresh_allocation");
        var integers = new Dictionary<string, int>(simulation.Parameters.Int32Values);
        foreach (var (key, value) in new (string, int)[] {
            (CityDivisor, 5), (GateDivisor, 5), (LossRandomBound, 75), (ReadyFixed, 25),
            (ReadyPercent, 100), (ReadyDenominator, 10000), (TraitReductionScale, 1),
            (PermanentTraitMultiplier, 3), (ReadyNormalPercent, 150), (WoundedFixed, 30),
            (WoundedPercent, 100), (WoundedDenominator, 10000), (DurabilityRandomBound, 5),
            (DurabilityFixed, 2), (DurabilityNormalPercent, 130) })
            if (simulation.Parameters.Float32Values.ContainsKey(key) || !integers.TryAdd(key, value))
                throw Missing("fresh_duplicate_parameter");
        return state with { NativeArmySimulation = simulation with
            { Parameters = simulation.Parameters with { Int32Values = integers } } };
    }
    internal static int Read(GameState state, string key) => state.NativeArmySimulation?.Parameters is
        { Language: "TC" or "SC" } p && !p.Float32Values.ContainsKey(key) && p.Int32Values.TryGetValue(key, out int value)
            ? value : throw Missing("current_parameter_" + key);
    internal static int Truncate(float value) => !float.IsFinite(value) || value >= 2147483648f || value < -2147483648f
        ? int.MinValue : (int)value;
    internal static T Need<T>(T? value, string key) where T : struct => value ?? throw Missing(key);
    internal static InvalidDataException Missing(string key) => new("army.progress.fire-damage." + key);
}
