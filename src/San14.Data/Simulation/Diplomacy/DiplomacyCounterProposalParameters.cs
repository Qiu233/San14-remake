using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //TC805970 and independently paired SC806120 use these same file-backed
    //immutable i32 rules, with no write references or
    //address escape in the cached original references. Saved overrides win.
    //The catalog is project data; gameplay never reads the original program.
    private static readonly IReadOnlyDictionary<string, int> DiplomacyCounterStaticParameters =
        new Dictionary<string, int>
        {
            ["diplomacy:counter-minimum-value-percent"] = 100, //1A49EC4
            ["diplomacy:counter-maximum-value-percent"] = 200, //1A49ED0
            ["diplomacy:counter-ratio-low-value-percent"] = 120, //1A49EDC
            ["diplomacy:counter-ratio-middle-value-percent"] = 150, //1A49EE8
            ["diplomacy:counter-ratio-high-value-percent"] = 200, //1A49EF4
            ["diplomacy:counter-restore-lower-value-percent"] = 150, //1A49F00
            ["diplomacy:counter-restore-upper-value-percent"] = 200, //1A49F0C
            ["diplomacy:counter-restore-upper-probability-threshold"] = 60, //1A49F18
            ["diplomacy:counter-restore-lower-probability-threshold"] = 30, //1A49F24
        };

    private static int ReadDiplomacyCounterParameterCurrent(GameState state, string name)
    {
        string suffix = "counter-" + name;
        string key = "diplomacy:" + suffix;
        var bank = state.NativeArmySimulation?.Parameters;
        if (bank is null || !HasDiplomacyParameterProfile(bank.Language, bank.RuleVersion))
            throw DiplomacyNumericMissing("counter_parameter_bank");
        if (bank.Float32Values.ContainsKey(key) || bank.Int32Values.ContainsKey(key))
            return DParameter(state, suffix);
        return DiplomacyCounterStaticParameters.TryGetValue(key, out int value)
            ? value : DParameter(state, suffix);
    }

    internal GameState InstallFreshDiplomacyCounterParameters(GameState state)
    {
        if (Seed.Language is not ("TC" or "SC")) return state;
        if (state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            state.Units.Count != 0 || state.NativeArmySimulation is not { } native ||
            native.Parameters.Language != Seed.Language ||
            !HasDiplomacyParameterProfile(native.Parameters.Language,native.Parameters.RuleVersion))
            throw DiplomacyNumericMissing("counter_fresh_parameter_lifecycle");
        var values = new Dictionary<string, int>(native.Parameters.Int32Values);
        foreach (var (key, value) in DiplomacyCounterStaticParameters)
        {
            if (values.ContainsKey(key) || native.Parameters.Float32Values.ContainsKey(key))
                throw DiplomacyNumericMissing("counter_fresh_parameter_collision_" + key);
            values.Add(key, value);
        }
        return state with { NativeArmySimulation = native with
        { Parameters = native.Parameters with { Int32Values = values } } };
    }
}
