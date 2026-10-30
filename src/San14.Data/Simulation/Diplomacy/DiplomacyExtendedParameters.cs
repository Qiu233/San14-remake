using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //TC and SC independently closed rule data from the ordinary ransom,
    //return-captive probability and result functions. Runtime uses these
    //semantic rules and the saved override bank, never program bytes.
    private static readonly IReadOnlyDictionary<string,int> ExtendedDiplomacyRules=new Dictionary<string,int>
    {
        ["return-captives-ransom-high-threshold"]=100,
        ["return-captives-ransom-middle-threshold"]=90,
        ["return-captives-ransom-low-threshold"]=70,
        ["return-captives-ransom-high-addition"]=600,
        ["return-captives-ransom-middle-addition"]=180,
        ["return-captives-ransom-low-addition"]=20,
        ["return-captives-ransom-trait-addition"]=100,
        ["return-captives-ransom-special-policy-addition"]=200,
        ["return-captives-ransom-unique-tactic-addition"]=200,
        ["return-captives-ransom-scale"]=50,
        ["return-captives-ransom-minimum"]=1000,
        ["return-captives-ransom-release-trait-addition"]=3000,
        ["return-captives-cost-relation-threshold"]=20,
        ["return-captives-rate-cost-multiplier"]=3,
        ["return-captives-rate-cost-divisor"]=10,
        ["return-captives-rate-scale"]=100,
        ["return-captives-relation-value-scale"]=110,
        ["return-captives-success-relation-addition"]=10,
        ["return-captives-success-relation-floor"]=30,
        ["return-captives-failure-adjustment"]=-5,
        ["return-captives-failure-cooldown-days"]=60,
    };

    private static int ReadExtendedDiplomacyRuleCurrent(GameState state,string key)
    {
        var bank=state.NativeArmySimulation?.Parameters??throw DiplomacyMissing("extended_parameters");
        if(!HasDiplomacyParameterProfile(bank.Language,bank.RuleVersion))
            throw DiplomacyMissing("extended_parameter_profile");
        string name="diplomacy:"+key;
        if(bank.Float32Values.ContainsKey(name))throw DiplomacyMissing("extended_parameter_kind_"+key);
        if(bank.Int32Values.TryGetValue(name,out int saved))return saved;
        return ExtendedDiplomacyRules.TryGetValue(key,out int value)?value:throw DiplomacyMissing("extended_parameter_"+key);
    }

    private GameState InstallFreshExtendedDiplomacyRules(GameState state)
    {
        var native=state.NativeArmySimulation??throw DiplomacyMissing("extended_fresh_parameters");
        if(native.Parameters.Language!=Seed.Language ||
            !HasDiplomacyParameterProfile(native.Parameters.Language,native.Parameters.RuleVersion))
            throw DiplomacyMissing("extended_fresh_parameter_profile");
        var values=new Dictionary<string,int>(native.Parameters.Int32Values);
        foreach(var (key,value) in ExtendedDiplomacyRules)
        {
            string name="diplomacy:"+key;
            if(values.ContainsKey(name)||native.Parameters.Float32Values.ContainsKey(name))
                throw DiplomacyMissing("extended_fresh_parameter_conflict_"+key);
            values.Add(name,value);
        }
        return state with {NativeArmySimulation=native with {Parameters=native.Parameters with {Int32Values=values}}};
    }
}
