using San14.Core;

namespace San14.Data;

/// <summary>Current saved trap coefficients. Only a genuinely fresh world
/// installs paired initial data; legacy missing values remain unknown.</summary>
internal static class TrapParameters
{
    private const string Prefix = "facility-trap:";
    internal const string AttackScale = Prefix + "attack-scale";
    internal const string ArgumentDurabilityScale = Prefix + "argument-durability-scale";
    internal const string ObjectTroopScale = Prefix + "object-troop-scale";
    internal const string PolicyDamageScale = Prefix + "policy-damage-scale";
    internal const string TraitDamageScale = Prefix + "trait-damage-scale";
    internal const string ConfusionChanceBaseline = Prefix + "confusion-chance-baseline";
    internal const string ImmobilizationChanceBaseline = Prefix + "immobilization-chance-baseline";
    internal const string DamageNoticeThreshold = Prefix + "damage-notice-threshold";
    internal const string RelationCeiling = Prefix + "relation-ceiling";
    internal const string RelationAdjustment = Prefix + "relation-adjustment";

    internal static GameState InstallFresh(GameState state)
    {
        var simulation = state.NativeArmySimulation ?? throw Missing("fresh_simulation");
        if (state.Units.Count != 0 || state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            simulation.Parameters.Language is not ("TC" or "SC")) throw Missing("fresh_allocation");
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values);
        foreach (var (key, value) in new (string, int)[] {
            (AttackScale, 100), (ArgumentDurabilityScale, 100), (ObjectTroopScale, 1000),
            (PolicyDamageScale, 1), (TraitDamageScale, 1),
            (ConfusionChanceBaseline, 20), (ImmobilizationChanceBaseline, 100),
            (DamageNoticeThreshold, 3000), (RelationCeiling, 15), (RelationAdjustment, -5) })
            if (simulation.Parameters.Float32Values.ContainsKey(key) || !values.TryAdd(key, value))
                throw Missing("fresh_duplicate_parameter");
        return state with { NativeArmySimulation = simulation with
            { Parameters = simulation.Parameters with { Int32Values = values } } };
    }

    internal static int Read(GameState state, string key) =>
        state.NativeArmySimulation?.Parameters is { Language: "TC" or "SC" } p &&
        !p.Float32Values.ContainsKey(key) && p.Int32Values.TryGetValue(key, out int value)
            ? value : throw Missing("current_parameter_" + key);
    internal static int Truncate(float value) => !float.IsFinite(value) || value >= 2147483648f || value < -2147483648f
        ? int.MinValue : (int)value;
    internal static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    internal static InvalidDataException Missing(string field) => new("army.progress.facility-trap." + field);
}
