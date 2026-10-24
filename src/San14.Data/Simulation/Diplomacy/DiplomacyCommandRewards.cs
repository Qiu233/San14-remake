using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private GameState InstallFreshDiplomacyCommandRewardParameters(GameState state, bool preserveExisting = false)
    {
        if (Seed.Language != "TC") return state;
        var native = state.NativeArmySimulation ?? throw DiplomacyMissing("fresh_command_reward_parameters");
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
            ("attack-success-merit", 300)
        })
        {
            string name = "diplomacy:" + key;
            if (values.ContainsKey(name) && preserveExisting) continue;
            if (values.ContainsKey(name) || native.Parameters.Float32Values.ContainsKey(name))
                throw DiplomacyMissing("fresh_command_reward_parameter_conflict");
            values.Add(name, value);
        }
        return state with { NativeArmySimulation = native with { Parameters = native.Parameters with { Int32Values = values } } };
    }

    private static int ReadDiplomacyCommandRewardParameter(GameState state, string key)
    {
        var parameters = state.NativeArmySimulation?.Parameters ?? throw DiplomacyMissing("command_reward_parameters");
        string name = "diplomacy:" + key;
        return !parameters.Float32Values.ContainsKey(name) && parameters.Int32Values.TryGetValue(name, out int value)
            ? value : throw DiplomacyMissing("parameter_" + name);
    }
}
