using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Creates one dedicated service on the caller's genuine fresh
    /// campaign candidate. Loading or binding an existing world never calls it.</summary>
    public GameState CreateFreshDuelService(GameState state)
    {
        if (state.CampaignDuelService is not null || state.DuelOpportunityCount is not null ||
            state.Officers.Values.Any(person => person.DuelCondition is not null) || state.Phase != TurnPhase.Strategy ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.Units.Count != 0 ||
            state.NativeArmySimulation?.Parameters.Language is not ("TC" or "SC") ||
            state.NativeArmySimulation.Parameters.Language != _scenario.Snapshot.Locale)
            throw DuelMissing("fresh_campaign_boundary");
        //Both constructors establish these gate values. The SC history helper
        //calls are not yet independently owned, so its history stays unknown.
        GameState fresh = InstallFreshDuelPersonConditions(DuelCampaignAftermathParameters.InstallFresh(
            DuelAftermathPrimitiveParameters.InstallFresh(DuelNumericParameters.InstallFresh(
                DuelOpportunityParameters.InstallFresh(state), _scenario), _scenario), _scenario));
        return fresh with { DuelOpportunityCount = 0, CampaignDuelService = new(false, false,
            _scenario.Snapshot.Locale == "TC" ? CampaignDuelHistoryKnowledge.KnownEmpty :
                CampaignDuelHistoryKnowledge.Unknown)
            { HistoricalPairs = _scenario.Snapshot.Locale == "TC" ? [] : null } };
    }

    internal void RequireInactiveDuelServiceForProgress(GameState state)
    {
        var service = state.CampaignDuelService ?? throw DuelMissing("service_owner");
        bool active = service.Active ?? throw DuelMissing("active");
        if (active) throw DuelMissing(state.CampaignDuelService?.Registered is null
            ? "active_registered_result_owner" : "aftermath_379790_campaign_consumer");
        //The inactive updater has no effects. Busy remains independent and
        //must be read from the current candidate after that updater boundary.
        if (state.CampaignDuelService?.Busy is not false)
            throw DuelMissing("pre_loop_busy");
    }

    internal GameState AdvanceDuelOpportunity(GameState state, NativeArmyProgressStageMoment moment)
    {
        //Preserve the source's BC-before-E58 read order. BC is discarded in
        //advanced mode, so unknown BC does not block its known refusal branch.
        int? normalMode = BattleRemakeCurrentSettings.ReadInt32(state, 0xBC);
        int selector = BattleRemakeCurrentSettings.ReadSelectorE58(state) ?? throw DuelMissing("settings_selector");
        bool refused = selector == 0
            ? (BattleRemakeCurrentSettings.ReadAdvancedParameter(state, 3) ?? throw DuelMissing("settings_advanced_duel_percent")) == 0
            : (normalMode ?? throw DuelMissing("settings_normal_mode")) == 1;
        GameState result = state;
        if (!refused)
        {
            int header = TacticRemakeCurrentBattleControl.ReadHeader(state) ?? throw DuelMissing("tutorial_header");
            if (header == 0)
            {
                byte count = state.DuelOpportunityCount ?? throw DuelMissing("count");
                int cap = DuelOpportunityParameters.Read(state, DuelOpportunityParameters.MaximumOpportunities);
                bool capped = count >= cap;
                if (capped)
                {
                    int world = state.CampaignEventWorld?.ScenarioId
                        ?? throw DuelMissing("World48");
                    capped = world is not (0 or 151);
                }
                if (!capped) result = AdvanceDuelEligibleOpportunities(state, moment);
            }
        }
        //Every real manager return reaches the scheduler's fresh Busy read.
        //Active/history are not extra gates inside the due manager.
        if (result.DuelAftermathContinuation is not null) return result;
        if (result.CampaignDuelService?.Busy is not false)
            throw DuelMissing(result.CampaignDuelService?.Registered is null ?
                "post_manager_busy" : "aftermath_379790_campaign_consumer");
        return result;
    }

    private GameState InstallFreshDuelPersonConditions(GameState state)
    {
        if (_scenario.Metadata.FormatMarker != 0x66 || _scenario.Snapshot.Characters.Count != 1851 ||
            state.ContentHashes.GetValueOrDefault(_scenario.ScenarioSource.ResourceUri) != _scenario.ScenarioSource.Sha256 ||
            state.ContentHashes.GetValueOrDefault(_scenario.FixedDataSource.ResourceUri) != _scenario.FixedDataSource.Sha256)
            throw DuelMissing("fresh_Person_resource_profile");
        var people = new Dictionary<int, OfficerState>(state.Officers);
        foreach (var person in state.Officers.Values)
        {
            int? physical = _definitions.ResolveOfficerSource(person.Id);
            if (physical is not >= 1 or > 1850) continue;
            var row = _scenario.Snapshot.Characters[physical.Value];
            var input = row.DuelConditionSource;
            bool specialInitializer = person.Id is >= 2001 and <= 2088 or >= 3001 and <= 3150 or >= 5101 and <= 5200;
            bool affiliationMatches = (row.DistrictSlot == 0 ? person.DistrictId is null or 0 : person.DistrictId == row.DistrictSlot) &&
                (row.ForceSlot == 0 ? person.ForceId is null or 0 : person.ForceId == row.ForceSlot);
            if (row.CharacterId != person.Id || input is null || input.RecordSlot != physical.Value || input.PersonId != person.Id ||
                OfficerBehaviorRules.UsesOrdinaryPersonRules(person.Behavior) is not true || row.CreditReference is not { } credit ||
                OfficerBehaviorSourceAdapter.EncodeFlags(person.Behavior) != credit.RawFlagsWord ||
                OfficerServiceStatusSourceAdapter.Encode(person.ServiceStatus) != row.StatusCode || !affiliationMatches || specialInitializer)
                continue;
            //The actual fresh allocation's F1 reset survives the static stream.
            //16A instead takes its real signed static value, including negatives.
            people[person.Id] = person with { DuelCondition = new(input.InitialQualificationAdjustment, 100) };
        }
        return state with { Officers = people };
    }

    private static InvalidDataException DuelMissing(string field) =>
        new("current.DuelOpportunity_" + field);
}
