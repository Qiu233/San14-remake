using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private GameState InstallFreshDiplomacyCommandRewardParameters(GameState state, bool preserveExisting = false)
    {
        if (Seed.Language is not ("TC" or "SC")) return state;
        var native = state.NativeArmySimulation ?? throw DiplomacyMissing("fresh_command_reward_parameters");
        if (native.Parameters.Language != Seed.Language ||
            !HasDiplomacyParameterProfile(native.Parameters.Language,native.Parameters.RuleVersion))
            throw DiplomacyMissing("fresh_command_reward_profile");
        //The complete SC common-growth switch independently reaches the same
        //twenty file-backed values; no TC runtime bank supplies this profile.
        var values = new Dictionary<string, int>(native.Parameters.Int32Values);
        foreach (var (key, value) in new (string, int)[]
        {
            ("goodwill-success-growth", 4500),
            ("goodwill-failure-growth", 1000),
            ("goodwill-success-merit", 160),
            ("alliance-success-growth", 15000),
            ("alliance-failure-growth", 2500),
            ("alliance-success-merit", 400),
            ("cancel-success-growth", 1000),
            ("cancel-command-merit", 120),
            ("attack-success-growth", 4500),
            ("attack-failure-growth", 1000),
            ("attack-success-merit", 300),
            ("surrender-success-growth",4500),("surrender-failure-growth",1000),("surrender-success-merit",750),
            ("return-captives-success-growth",4500),("return-captives-failure-growth",1000),("return-captives-success-merit",180),
            ("exchange-success-growth",4500),("exchange-failure-growth",1000),("exchange-success-merit",180)
        })
        {
            string name = "diplomacy:" + key;
            if (native.Parameters.Float32Values.ContainsKey(name))
                throw DiplomacyMissing("fresh_command_reward_parameter_kind");
            if (values.ContainsKey(name) && preserveExisting) continue;
            if (values.ContainsKey(name))
                throw DiplomacyMissing("fresh_command_reward_parameter_conflict");
            values.Add(name, value);
        }
        return state with { NativeArmySimulation = native with { Parameters = native.Parameters with { Int32Values = values } } };
    }

    private static int ReadDiplomacyCommandRewardParameter(GameState state, string key)
    {
        var parameters = state.NativeArmySimulation?.Parameters ?? throw DiplomacyMissing("command_reward_parameters");
        if(!HasDiplomacyParameterProfile(parameters.Language,parameters.RuleVersion))
            throw DiplomacyMissing("command_reward_profile");
        string name = "diplomacy:" + key;
        if(parameters.Float32Values.ContainsKey(name))throw DiplomacyMissing("parameter_kind_"+name);
        if(parameters.Int32Values.TryGetValue(name,out int value))return value;
        return key switch
            {
                "surrender-success-growth" or "return-captives-success-growth" or "exchange-success-growth"=>4500,
                "surrender-failure-growth" or "return-captives-failure-growth" or "exchange-failure-growth"=>1000,
                "surrender-success-merit"=>750,
                "return-captives-success-merit" or "exchange-success-merit"=>180,
                _=>throw DiplomacyMissing("parameter_"+name)
            };
    }
}
