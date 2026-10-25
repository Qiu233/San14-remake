using San14.Core;

namespace San14.Data;

/// <summary>Five independently recovered SC/TC Base automatic rule scalars.
/// This catalog uses the saved mechanism owner, independently of UI locale.
/// Original executable data is never opened by the running game.</summary>
internal static class BaseAutomaticRuleCatalog
{
    internal static int Read(GameState state, string semanticName)
    {
        var bank = state.NativeArmySimulation?.Parameters
            ?? throw Missing("parameter_owner");
        string key = "simulation.tactics.base-origin." + semanticName;
        if (bank.Float32Values.ContainsKey(key)) throw Missing(key + "_integer");
        if (bank.Int32Values.TryGetValue(key, out int saved)) return saved;
        if (bank.RuleVersion != SimulationRuleIdentity.Version || bank.Language is not ("SC" or "TC"))
            throw Missing("mechanism_catalog_" + bank.RuleVersion + "_" + bank.Language);
        //Both current CE data sources independently contain these values.
        //They are mechanism rules, not defaults inferred from one scenario.
        return semanticName switch
        {
            "city-trial-rate" => 100,
            "gate-trial-rate" => 100,
            "object14-additive-percent" => 20,
            "advantage3-scale" => 1,
            "fire-policy-multiplier" => 1,
            _ => throw Missing("unknown_scalar_" + semanticName)
        };
    }

    private static InvalidDataException Missing(string owner) => new("automatic_Base_rule." + owner);
}
