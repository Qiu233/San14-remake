using San14.Core;
namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //These two scalars have independently qualified TC and SC loader-zero owners. The original program
    //only reads them, with no store or address escape in their scalar block.
    //They are immutable rule defaults, not observed current-process values.
    private static readonly IReadOnlyDictionary<string, int> DiplomacyNumericLoaderDefaults = new Dictionary<string, int>
    {
        ["goodwill-failure-adjustment"] = 0,
        ["alliance-success-extra-relation"] = 0,
    };

    //TC and SC independently read the same values from their own reached
    //file-backed constants and qualified loader-zero owners.
    //Loaded integer values retain priority; no current globals are sampled.
    public GameState InstallFreshDiplomacyNumericParameters(GameState state)
    {
        if (Seed.Language is not ("TC" or "SC")) return state;
        if (state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.Units.Count != 0 ||
            state.NativeArmySimulation is not { } native || native.Parameters.Language != Seed.Language ||
            !HasDiplomacyParameterProfile(native.Parameters.Language, native.Parameters.RuleVersion) ||
            native.Parameters.Int32Values.ContainsKey("diplomacy:parameter-version"))
            throw DiplomacyNumericMissing("fresh_parameter_lifecycle");
        var values = new Dictionary<string,int>(native.Parameters.Int32Values);
        foreach (var (name,value) in new (string,int)[]
        {
            ("gold-value-percent", 100),
            ("supplies-value-divisor", 50),
            ("item-value-multiplier", 400),
            ("area-value-base", 3000),
            ("area-value-cell-multiplier", 100),
            ("port-area-value-cell-multiplier", 50),
            ("policy-offer-scale", 1),
            ("politics-trait-scale", 1),
            ("politics-baseline", 1070),
            ("goodwill-relation-maximum", 200),
            ("goodwill-relation-square-divisor", 8),
            ("goodwill-city-base", 100),
            ("goodwill-politics-divisor", 2),
            ("goodwill-cost-rounding", 50),
            ("goodwill-hostile-cost-scale", 200),
            ("goodwill-spouse-reduction", 30),
            ("goodwill-sworn-reduction", 30),
            ("goodwill-liked-reduction", 15),
            ("goodwill-disliked-relation-divisor", 2),
            ("goodwill-rate-cost-multiplier", 3),
            ("goodwill-rate-scale", 100),
            ("goodwill-rate-cost-divisor", 10),
            ("alliance-cost-base", 4000),
            ("alliance-city-cost", 150),
            ("alliance-months-baseline", 5),
            ("alliance-month-cost", 100),
            ("alliance-relation-offset", 50),
            ("alliance-rate-cost-multiplier", 3),
            ("alliance-rate-scale", 100),
            ("alliance-rate-cost-divisor", 10),
            ("attack-relation-offset", 30),
            ("attack-cost-base", 3000),
            ("attack-city-square-cost", 35),
            ("attack-rate-cost-multiplier", 3),
            ("attack-rate-scale", 100),
            ("attack-rate-cost-divisor", 10),
            ("attack-forbid-relation-difference", 20),
            ("attack-forbid-armies-per-city", 3000),
            ("attack-forbid-food-per-army", 10),
            ("attack-minimum-alliance-months", 3),
            ("goodwill-flat-gain", 10),
            ("goodwill-extra-gain-cap", 40),
            ("goodwill-failure-cooldown-days", 60),
            ("goodwill-special-base", 1),
            ("goodwill-special-extra-cap", 19),
            ("goodwill-special-relation-divisor", 2),
            ("goodwill-special-gift-divisor", 1000),
            ("goodwill-special-relation-threshold", 50),
            ("alliance-relation-floor", 50),
            ("alliance-failure-adjustment", -5),
            ("alliance-failure-cooldown-days", 60),
            ("attack-duration-months", 12),
            ("attack-relation-gain", -15),
            ("attack-relation-floor", 50),
            ("attack-failure-adjustment", -5),
            ("attack-failure-cooldown-days", 60),
            ("cancel-popularity-loss", 20),
            ("cancel-low-relation-adjustment", -50),
            ("cancel-low-relation-threshold", 110),
            ("cancel-high-relation-target", 60),
            ("cancel-trespass-adjustment", -50),
            ("cancel-third-force-adjustment", -5),
            ("cancel-third-force-trespass-adjustment", -10),
            ("cancel-loyalty-fixed-loss", 10),
            ("cancel-loyalty-random-bound", 2),
            ("cancel-loyalty-disliked-penalty", 30),
            ("cancel-loyalty-affinity-middle-penalty", 10),
            ("cancel-loyalty-affinity-near-penalty", 20),
        })
        {
            string key = "diplomacy:" + name;
            if (values.ContainsKey(key) || native.Parameters.Float32Values.ContainsKey(key)) throw DiplomacyNumericMissing("fresh_parameter_collision_" + name);
            values.Add(key,value);
        }
        foreach (var (name, value) in DiplomacyNumericLoaderDefaults)
        {
            string key = "diplomacy:" + name;
            if (values.ContainsKey(key) || native.Parameters.Float32Values.ContainsKey(key))
                throw DiplomacyNumericMissing("fresh_parameter_collision_" + name);
            values.Add(key, value);
        }
        values.Add("diplomacy:parameter-version",1);
        return state with { NativeArmySimulation = native with { Parameters = native.Parameters with { Int32Values = values } } };
    }
    //Both profiles are qualified by their own source consumers and data;
    //accepting a locale never admits a bank belonging to another rule version.
    private static bool HasDiplomacyParameterProfile(string language,string ruleVersion) => language switch
    {
        "TC" => ruleVersion == SimulationRuleIdentity.ForLocale("TC"),
        "SC" => ruleVersion == SimulationRuleIdentity.ForLocale("SC"),
        _ => false,
    };

    private static int DParameter(GameState state,string name)
    {
        string key="diplomacy:"+name;
        var p=state.NativeArmySimulation?.Parameters;
        if(p is null || !HasDiplomacyParameterProfile(p.Language,p.RuleVersion) ||
            p.Float32Values.ContainsKey(key)) throw DiplomacyNumericMissing("parameter_"+name);
        if (p.Int32Values.TryGetValue(key, out int value)) return value;
        //Older saves may predate these proved defaults. Only these exact
        //rules may fall back; arbitrary absent current parameters stay unknown.
        if (DiplomacyNumericLoaderDefaults.TryGetValue(name, out value)) return value;
        throw DiplomacyNumericMissing("parameter_"+name);
    }
    private static int DDiv(int numerator,int divisor,string owner) => divisor==0 ? throw DiplomacyNumericMissing(owner+"_zero_divisor") : numerator/divisor;
    private static T DNeed<T>(T? value,string field) where T:struct => value ?? throw DiplomacyNumericMissing(field);
    private static InvalidDataException DiplomacyNumericMissing(string owner) => new("diplomacy.current_"+owner);
}
