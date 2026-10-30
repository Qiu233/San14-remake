using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    // CE initial-image rule inputs extracted offline; saved overrides remain authoritative.
    private static readonly IReadOnlyDictionary<string, int> ForeignTradeParameters = new Dictionary<string, int>
    {
        ["return-gold-1-cost"] = 60,
        ["return-gold-2-cost"] = 60,
        ["return-gold-3-cost"] = 90,
        ["return-gold-4-cost"] = 120,
        ["return-gold-cost-divisor"] = 30,
        ["return-gold-cost-scale"] = 1000,
        ["return-gold-ability-divisor"] = 20,
        ["return-gold-friendship-divisor"] = 5,
        ["item-fallback-random-bound"] = 2000,
        ["item-fallback-ability-scale"] = 20,
        ["item-fallback-base"] = 12000,
        ["gem-return-minimum"] = 5,
        ["gem-return-charisma-divisor"] = 70,
        ["reward-friendship-spend"] = 0,
        ["outward-friendship-troop-scale"] = 50,
        ["outward-friendship-item-scale"] = 4000,
        ["outward-friendship-politics-divisor"] = 2,
        ["outward-friendship-other-ability-divisor"] = 10,
        ["outward-friendship-offer-divisor"] = 6,
        ["outward-friendship-ability-divisor"] = 2,
        ["outward-friendship-base"] = 1,
        ["outward-hostile-loss-cap"] = 50,
        ["outward-hostile-loss-divisor"] = 2,
        ["outward-hostile-warning-minimum"] = 1,
        ["first-friendship-threshold"] = 1,
        ["second-friendship-threshold"] = 300,
        ["third-friendship-threshold"] = 600,
        ["fourth-friendship-threshold"] = 1000,
        ["return-leadership-divisor"] = 60,
        ["return-short-days"] = 30,
        ["return-state-advantage-reduction"] = 5,
        ["return-low-difficulty-reduction"] = 15,
        ["first-friendship-floor"] = 1,
        ["second-friendship-floor"] = 150,
        ["third-friendship-floor"] = 300,
        ["other-friendship-floor"] = 500,
        ["unacquired-friendship-floor"] = 0,
        ["return-success-growth-3"] = 10000,
        ["return-success-growth-2"] = 10000,
        ["return-success-growth-1"] = 10000,
        ["return-success-growth-0"] = 10000,
        ["return-success-growth-4"] = 5000,
        ["return-failure-growth-3"] = 4000,
        ["return-failure-growth-2"] = 4000,
        ["return-failure-growth-1"] = 4000,
        ["return-failure-growth-0"] = 4000,
        ["return-failure-growth-4"] = 2000,
        ["return-success-merit"] = 240,
        ["return-failure-merit"] = 80,
        ["outward-growth-3"] = 10000,
        ["outward-growth-2"] = 10000,
        ["outward-growth-1"] = 10000,
        ["outward-growth-0"] = 10000,
        ["outward-growth-4"] = 5000,
        ["outward-merit-day-scale"] = 4,
        ["event-loss-percent"] = 80,
        ["event-extra-percent"] = 20,
        ["event-shorter-days"] = 10,
        ["trial-troop-divisor"] = 30,
        ["trial-troop-scale"] = 40,
        ["trial-food-base"] = 16,
        ["trial-low-leadership-scale"] = 25,
        ["trial-leadership-threshold"] = 200,
        ["trial-trait-percent"] = 92,
        ["trial-advantage-percent"] = 95,
        ["food-to-gold-divisor"] = 50,
    };

    internal static int ForeignTradeParameter(GameState state, string name)
    {
        var parameters = state.NativeArmySimulation?.Parameters ?? throw CaravanMissing("parameter_owner");
        if (parameters.Language is not ("TC" or "SC")) throw CaravanMissing("parameter_profile");
        string key = "simulation.foreign-trade.caravan." + name;
        if (parameters.Float32Values.ContainsKey(key)) throw CaravanMissing("integer_parameter_" + name);
        if (parameters.Int32Values.TryGetValue(key, out int value)) return value;
        return ForeignTradeParameters.TryGetValue(name, out value) ? value : throw CaravanMissing("parameter_" + name);
    }

    private static InvalidDataException CaravanMissing(string name) => new("foreign-trade.caravan." + name);
}
