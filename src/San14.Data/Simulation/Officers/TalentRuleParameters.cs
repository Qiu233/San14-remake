using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Installs proved immutable TC rule data in an unused fresh
    /// allocation. Loading never overwrites saved parameter values.</summary>
    public GameState InstallFreshTalentRules(GameState state)
    {
        if (Seed.Language != "TC") return state;
        const string version = "simulation.talent.parameter-version";
        if (state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.Units.Count != 0 ||
            state.NativeArmySimulation is not { } simulation || simulation.Parameters.Int32Values.ContainsKey(version))
            throw new InvalidDataException("Talent parameters require an unused fresh allocation.");
        //TC offline data constants and proved loader-zero rules. No original
        //executable or current process is accessed by the game.
        var data = new Dictionary<string, int>
        {
            [PersonReturnCommands.TalentSearchBookCostKey] = 1,
            [PersonReturnCommands.TalentRecruitBookCostKey] = 1,
            [TalentSearchSource.CandidateCountLimitKey] = 3,
            [TalentSearchSource.DiscoveryDivisorKey] = 30,
            [TalentSearchSource.Trait63ScaleKey] = 1,
            ["simulation.talent-search.scenery-count-limit"] = 2,
            ["simulation.talent-search.scenery-discovery-divisor"] = 200,
            ["simulation.talent-search.item-ordinary-category-weight"] = 60,
            ["simulation.talent-search.item-type13-category-weight"] = 10,
            ["simulation.talent-search.item-type12-category-weight"] = 5,
            ["simulation.talent-search.item-count-limit"] = 1,
            ["simulation.talent-search.item-discovery-divisor"] = 90,
            ["simulation.talent-search.item-type13-city-threshold"] = 5,
            ["simulation.talent-search.item-type13-divisor"] = 5,
            ["simulation.talent-search.item-type12-divisor"] = 5,
            ["simulation.talent-search.gold-force-flag-additive"] = 10,
            ["simulation.talent-search.gold-force-flag-multiplier"] = 70,
            [TalentRecruitParameters.Key("player-easy-bonus")] = 10,
            [TalentRecruitParameters.Key("player-intermediate-bonus")] = 5,
            [TalentRecruitParameters.Key("land-advantage-multiplier")] = 1,
            [TalentRecruitParameters.Key("employ-success-growth")] = 2000,
            [TalentRecruitParameters.Key("employ-failure-growth")] = 400,
            [TalentRecruitParameters.Key("employ-success-merit-base")] = 60,
            [TalentRecruitParameters.Key("employ-success-ability-count-scale")] = 10,
            [TalentRecruitParameters.Key("search-success-growth")] = 60,
            [TalentRecruitParameters.Key("search-failure-growth")] = 10,
            [TalentRecruitParameters.Key("search-common-merit")] = 40,
            [TalentRecruitParameters.Key("search-no-find-merit")] = 18,
            [TalentRecruitParameters.Key("search-scenery-merit")] = 50,
            [TalentRecruitParameters.Key("search-item-merit")] = 35,
            [TalentRecruitParameters.Key("search-gold-merit")] = 30,
            [TalentRecruitParameters.Key("scenery-immediate-security")] = 20,
            [TalentRecruitParameters.Key("scenery-immediate-morale")] = 10,
            [TalentRecruitParameters.Key("scenery-immediate-force-experience")] = 500,
            [TalentRecruitParameters.Key("scenery-discovery-popularity")] = 10,
            [TalentRecruitParameters.Key("join-diplomacy-office-base")] = 10
        };
        foreach (var (name, value) in TalentRecruitParameters.LoaderDefaults)
            data.Add(TalentRecruitParameters.Key(name), value);
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values) { [version] = 1 };
        foreach (var (key, value) in data)
        {
            if (simulation.Parameters.Float32Values.ContainsKey(key) || values.ContainsKey(key))
                throw new InvalidDataException("Talent parameter installation conflicts with current values.");
            values.Add(key, value);
        }
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = values } },
            TalentGemDiscoveries = TalentGemDiscoveryInputs.LoadFresh(_scenario),
            GemForceInventory = TalentGemAcquisitionSource.LoadFresh(_scenario),
            //326230 resets World17B0/17B4/17B8 to0/1/1, represented by
            //the three visible radio indices0/0/0. Loading never installs this.
            TalentAutoSearchPreferences = new(0, 0, 0) };
    }
}
