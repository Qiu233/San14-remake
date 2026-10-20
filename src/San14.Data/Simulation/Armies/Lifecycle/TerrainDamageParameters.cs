using San14.Core;

namespace San14.Data;

/// <summary>Paired initial image-profile coefficients of terrain damage.
/// This seed does not claim the native initialization/override lifecycle.
/// Runtime consumers read saved values at use; old saves are not repaired.</summary>
internal static class TerrainDamageParameters
{
    private const string Prefix = "terrain-damage:";
    internal const string WoundedBaseLoss = Prefix + "wounded-base-loss",
        WoundedSamplerBound = Prefix + "wounded-sampler-bound",
        ReadyBaseLoss = Prefix + "ready-base-loss",
        ReadySamplerBound = Prefix + "ready-sampler-bound",
        PermanentEffect56Divisor = Prefix + "permanent-effect56-divisor";

    internal static GameState InstallFresh(GameState state)
    {
        var simulation = state.NativeArmySimulation ?? throw Missing("fresh_simulation");
        if (state.Units.Count != 0 || state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            simulation.Parameters.Language is not ("TC" or "SC")) throw Missing("fresh_allocation");
        var integers = new Dictionary<string, int>(simulation.Parameters.Int32Values);
        foreach (var (key, value) in new (string, int)[] {
            (WoundedBaseLoss, 25), (WoundedSamplerBound, 100),
            (ReadyBaseLoss, 25), (ReadySamplerBound, 100), (PermanentEffect56Divisor, 2) })
            if (simulation.Parameters.Float32Values.ContainsKey(key) || !integers.TryAdd(key, value))
                throw Missing("fresh_duplicate_parameter");
        return state with { NativeArmySimulation = simulation with
            { Parameters = simulation.Parameters with { Int32Values = integers } } };
    }

    internal static int Read(GameState state, string key) => state.NativeArmySimulation?.Parameters is
        { Language: "TC" or "SC" } p && !p.Float32Values.ContainsKey(key) && p.Int32Values.TryGetValue(key, out int value)
            ? value : throw Missing("current_parameter_" + key);

    private static InvalidDataException Missing(string key) => new("army.progress.terrain-damage." + key);
}
