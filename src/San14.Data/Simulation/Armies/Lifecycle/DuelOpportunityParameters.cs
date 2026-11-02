using San14.Core;

namespace San14.Data;

internal static class DuelOpportunityParameters
{
    internal const string MaximumOpportunities = "duel-opportunity:maximum-opportunities";
    internal const string MinimumEffectiveStrength = "duel-opportunity:minimum-effective-strength";

    internal const string BaseChance = "duel-opportunity:base-chance";
    internal const string CountDecayPercent = "duel-opportunity:count-decay-percent";
    internal const string Trait61OrEffect29Chance = "duel-opportunity:trait61-or-effect29-chance";
    internal const string Trait62Chance = "duel-opportunity:trait62-chance";

    // Independently qualified SC PE-loader initialization. The complete
    // scalar-owner audit has no writes, address escapes or relocated pointers;
    // SC2CADE3/2CADEB read these signed coefficients from its virtual tail.
    private static readonly IReadOnlyDictionary<string, int> ScLoaderDefaults =
        new Dictionary<string, int>
        {
            [Trait61OrEffect29Chance] = 0,
            [Trait62Chance] = 0,
        };

    // Offline paired image initial values. These are installed once for the
    // actual fresh profile; current queries consume only the saved bank.
    internal static GameState InstallFresh(GameState state, LoadedScenario source)
    {
        var simulation = state.NativeArmySimulation ?? throw Missing("parameter_owner");
        var bank = simulation.Parameters;
        if (bank.Language is not ("TC" or "SC") || bank.Int32Values.ContainsKey(MaximumOpportunities) ||
            bank.Int32Values.ContainsKey(MinimumEffectiveStrength) ||
            bank.Float32Values.ContainsKey(MaximumOpportunities) || bank.Float32Values.ContainsKey(MinimumEffectiveStrength) ||
            bank.Int32Values.ContainsKey(BaseChance) || bank.Int32Values.ContainsKey(CountDecayPercent) ||
            bank.Float32Values.ContainsKey(BaseChance) || bank.Float32Values.ContainsKey(CountDecayPercent) ||
            bank.Int32Values.ContainsKey(Trait61OrEffect29Chance) || bank.Int32Values.ContainsKey(Trait62Chance) ||
            bank.Float32Values.ContainsKey(Trait61OrEffect29Chance) || bank.Float32Values.ContainsKey(Trait62Chance))
            throw Missing("fresh_parameter_owner");
        var values = new Dictionary<string, int>(bank.Int32Values)
        { [MaximumOpportunities] = 2, [MinimumEffectiveStrength] = 65, [BaseChance] = 15, [CountDecayPercent] = 100 };
        AddConfirmedFreshChanceProfile(state, source, values);
        DuelActorWeightParameters.AddFresh(bank.Language, values, bank.Float32Values);
        DuelOpponentParameters.AddFresh(bank.Language, values, bank.Float32Values);
        DuelSelectionParameters.AddFresh(bank.Language, values, bank.Float32Values);
        return state with { NativeArmySimulation = simulation with { Parameters = bank with { Int32Values = values } } };
    }

    private static void AddConfirmedFreshChanceProfile(GameState state, LoadedScenario source, Dictionary<string, int> values)
    {
        if (state.NativeArmySimulation?.Parameters is { Language: "SC" } sc)
        {
            if (sc.RuleVersion != SimulationRuleIdentity.ForLocale("SC"))
                throw Missing("fresh_sc_rule_profile");
            foreach (var (key, value) in ScLoaderDefaults) values.Add(key, value);
            return;
        }
        // TC independently retains its own PE-loader initialization across
        // the complete qualified scalar owner: no write or escaped address.
        // These invariant fresh values do not depend on one measured game.
        if (state.NativeArmySimulation?.Parameters is not { Language: "TC" } tc ||
            source.Snapshot.Locale != "TC" ||
            tc.RuleVersion != SimulationRuleIdentity.ForLocale("TC"))
            throw Missing("fresh_tc_rule_profile");
        values.Add(Trait61OrEffect29Chance, 0);
        values.Add(Trait62Chance, 0);
    }

    internal static int Read(GameState state, string key)
    {
        if (state.NativeArmySimulation?.Parameters is not { Language: "TC" or "SC" } parameters ||
            parameters.Float32Values.ContainsKey(key)) throw Missing("parameter_" + key);
        if (parameters.Int32Values.TryGetValue(key, out int value)) return value;
        // Missing older SC keys use only this independently proved immutable
        // rule profile. Saved integers win; reads never reinstall a bank.
        if (parameters.Language == "SC" && parameters.RuleVersion == SimulationRuleIdentity.ForLocale("SC") &&
            ScLoaderDefaults.TryGetValue(key, out value)) return value;
        throw Missing("parameter_" + key);
    }
    internal static InvalidDataException Missing(string field) => new("current.DuelOpportunity_" + field);
}
