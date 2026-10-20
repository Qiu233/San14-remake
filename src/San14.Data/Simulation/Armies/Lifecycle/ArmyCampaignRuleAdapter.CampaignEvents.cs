using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed record CampaignEventConditionScan(GameState State, S14EventEligibilityScanResult Eligibility);

public sealed partial class ArmyCampaignRuleAdapter
{
    private readonly Lazy<LoadedEvents> _campaignEvents;
    private PersonFreedomHomeSource? _eventCityNeighborhoods;

    private GameState InstallFreshCampaignEventState(GameState state)
    {
        if (state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.CampaignEventWorld is not null ||
            state.CampaignEvents is not null || state.CampaignEventEligibility is not null ||
            _scenario.Snapshot.WorldParameters is not { RawField48: int scenarioId } source)
            throw new InvalidDataException("Campaign event state requires the actual fresh world source.");
        var events = _campaignEvents.Value;
        var values = state.NativeArmySimulation?.Parameters.Int32Values;
        sbyte? userIndex = source.RawModeValue != 3 &&
            values?.GetValueOrDefault(PersonGrowthCurrentQueries.VersionKey) == 1 &&
            values.TryGetValue(PersonGrowthCurrentQueries.WorldMode40Key, out int worldMode) && worldMode != 4
                ? (sbyte)-1 : null;
        // World construction clears both history arrays and unset dates;
        // event resource loading independently clears the manager decisions.
        // First-strategy evaluation and its writers have not run at this point.
        return state with
        {
            CampaignEventWorld = new(scenarioId, source.RawModeValue, source.RawPastGameTime)
                { CurrentUserIndex = userIndex, SpecialPersonEncounterFlags = 0, EventAchievementFlags = 0 },
            CampaignEvents = new(new bool[1024], new bool[1024], new CampaignEventOccurrenceDate?[1024], false),
            CampaignEventEligibility = new(new bool[992]) { HistoryRecordingSuppressed = false },
            CampaignHistory = state.CampaignHistory is null ? CampaignHistoryState.Fresh()
                : throw new InvalidDataException("Fresh campaign history is already owned."),
            ContentHashes = new(state.ContentHashes) { [events.Source.ResourceUri] = events.Source.Sha256 }
        };
    }

    /// <summary>Current, detached condition queries. Date identity and histories
    /// come from the saved world; no static scenario value fills a missing owner.</summary>
    public SourceWorldContext CaptureCurrentCampaignEventQueries(GameState state, NativeArmyProgressStageMoment? moment = null)
    {
        var world = state.CampaignEventWorld ?? throw OccupationMissing("campaign_event_current_world");
        var history = state.CampaignEvents ?? throw OccupationMissing("campaign_event_history");
        var manager = state.CampaignEventEligibility ?? throw OccupationMissing("campaign_event_manager");
        var season = state.NativeMapSeason ?? throw OccupationMissing("campaign_event_calendar");
        if (season.SourceScenarioUri != _scenario.ScenarioSource.ResourceUri ||
            season.SourceScenarioSha256 != _scenario.ScenarioSource.Sha256 ||
            season.SourceWorldSha256 != _scenario.Snapshot.WorldParameters?.WorldSha256)
            throw OccupationMissing("campaign_event_calendar_source");
        var current = CaptureWritableTransportRouteCurrent(state);
        current.MaterializeFixedScenery(_landmarkDefinitions);
        // Overwrite every current lifecycle field, including missing old owners;
        // fixed loading must never make scenario flags a saved-world fallback.
        for (int i = 0; i < 101; i++) current.SetSceneryFlags47(i, state.LandmarkInvestments?.GetValueOrDefault(i)?.Flags);
        current.ConditionSceneryLevelReader = physical => state.LandmarkInvestments?.GetValueOrDefault(physical)?.Development?.Level;
        OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
        OwnedArmyDestructionCurrent.BindOwnedCurrentContext(state, current);
        _personAssignmentPrecallInputs.BindCurrent(state, current);
        _personRelationInputs.BindCurrent(state, current);
        PersonReturnCommands.BindCurrent(state, current);
        PersonArmyCaptureInputs.BindCurrent(state, current);
        _moraleWaveInputs.BindCurrent(state, current);
        current.ConditionLoyaltyReader = physical => ArmyDestructionLoyalty.ReadCurrentRawE8(state, current, physical, _governorInputs);
        current.ConditionBaseAbilityReader = (physical, index) =>
        {
            if (current.ReadPersonIdentity(physical) is not ushort id || current.ResolveOfficerSource(id) != physical ||
                state.Officers.GetValueOrDefault(id)?.BaseAbilities is not { } ability) return null;
            int value = index switch
            {
                0 => ability.Leadership, 1 => ability.Strength, 2 => ability.Intelligence,
                3 => ability.Politics, 4 => ability.Charm, _ => -1
            };
            return value is >= 0 and <= 255 ? (byte)value : null;
        };
        current.ConditionAppearanceYearReader = physical =>
        {
            if (current.ReadPersonIdentity(physical) is not ushort id) return null;
            if (id != 0) return current.ResolveOfficerSource(id) == physical ? state.Officers.GetValueOrDefault(id)?.AppearanceYear : null;
            return state.NativeArmySimulation!.Parameters.Int32Values.TryGetValue(
                OwnedArmyLifecycleDetachedInputs.Key("Person", physical, "appearance-year"), out int year) && year is >= 0 and <= 65535 ? (ushort)year : null;
        };
        current.ConditionLifespanReader = physical => current.ReadPersonIdentity(physical) is ushort id &&
            current.ResolveOfficerSource(id) == physical ? state.Officers.GetValueOrDefault(id)?.Lifespan : null;
        current.ConditionImperialCourtTendencyReader = physical => RulerCaptureReleaseInputs.ReadCurrentWord(state, physical, 0x17A);
        current.ConditionLifespanModeReader = () => BattleRemakeCurrentSettings.ReadSelectorE58(state) is int selector
            ? BattleRemakeCurrentSettings.ReadInt32(state, selector == 0 ? 0x1C4 : 0x2C) : null;
        current.CurrentPlayerForce = OwnedCurrentPlayerForce.ReadCurrentReference(state, current);
        current.ConditionForceDependenceReader = physical =>
            state.NativeForceUnregisterCurrent?.ForceResetImages.TryGetValue(physical, out var reset) == true
                ? reset[0x194] : state.NativeArmySimulation?.Forces.GetValueOrDefault(physical)?.Flags194;
        current.ConditionForceCoalitionReader = physical =>
        {
            byte? flags = state.NativeForceUnregisterCurrent?.ForceResetImages.TryGetValue(physical, out var reset) == true
                ? reset[0x12] : state.NativeArmySimulation?.Forces.GetValueOrDefault(physical)?.Flags12;
            return flags is byte value ? (value & 0x02) != 0 : null;
        };
        current.ConditionForcePolicyEnabledReader = (physical, effect) => state.Forces.GetValueOrDefault(physical)?.PolicyEffects?.IsEnabled(effect);
        current.ConditionForceProcessingOrderIds = state.ForceProcessingOrder?.ForceIds;
        current.ConditionCompleteStateCountReader = physical => BaseCaptureRewardSource.CountCompleteStates(state, current, new(physical));
        current.ConditionAllianceReader = (first, second) => second is >= 0 and < 52 &&
            state.NativeArmySimulation?.Forces.GetValueOrDefault(first)?.RelationsEA is { Length: 52 } relations ? relations[second] : null;
        current.ConditionFriendshipLevelReader = (first, second) =>
        {
            if (state.NativeArmySimulation is not { } simulation) return null;
            int value = 0;
            if (second < 52)
            {
                if (simulation.Forces.GetValueOrDefault(first)?.NormalAftermath?.Relations13 is not { Length: 52 } relations) return null;
                value = relations[second];
            }
            for (int level = 1; level <= 5; level++)
            {
                if (!state.NativeArmySimulation.Parameters.Int32Values.TryGetValue(SimulationParameterKeys.ForceRelations.LevelThreshold(level), out int threshold)) return null;
                if (value < threshold) return level - 1;
            }
            return 5;
        };
        current.ConditionForceAdjacentReader = (first, second) =>
            (_eventCityNeighborhoods ??= new(_scenario)).AreForceCitiesAdjacent(current, first, second);
        current.ConditionCityRegionReader = physical => _growthQueries.ReadCityState(state, physical) is byte province
            ? BaseCaptureRewardInputs.ReadStateRegion(state, province < 23 ? province : 0) : null;
        current.ConditionCityStateReader = physical => _growthQueries.ReadCityState(state, physical);
        current.ConditionStateAdvantageReader = physical => _growthQueries.ReadStateAdvantage(state, physical);
        current.ConditionAdvantageRequirementReader = physical => _growthQueries.ReadAdvantageRequirement(state, physical);
        current.ConditionCityLevelReader = physical => state.Cities.GetValueOrDefault(physical)?.Size is { } size && Enum.IsDefined(size)
            ? (byte)((int)size + 1) : null;
        current.ConditionAreaSecurityReader = physical => state.Areas.GetValueOrDefault(physical)?.Security is int security && security is >= 0 and <= 255
            ? (byte)security : null;
        current.ConditionAreaDevelopmentLevelReader = (physical, field) =>
        {
            var development = state.Areas.GetValueOrDefault(physical)?.Development;
            int? progress = field switch
            {
                CeAreaDevelopmentField.Commerce => development?.Commerce,
                CeAreaDevelopmentField.Agriculture => development?.Agriculture,
                CeAreaDevelopmentField.Barracks => development?.Barracks,
                _ => null
            };
            return progress is >= 0 and <= ushort.MaxValue ? CeAreaIncomeMath.Level((ushort)progress.Value) : null;
        };
        current.RawWorldField48 = world.ScenarioId;
        current.EventConditions.ScenarioMode = world.ScenarioMode;
        current.ConditionCurrentUserIndex = world.CurrentUserIndex;
        current.ConditionSpecialPersonEncounterFlags = world.SpecialPersonEncounterFlags;
        current.ConditionEventAchievementFlags = world.EventAchievementFlags;
        current.ConditionCombatEngagements = state.NormalCombatRefresh?.Engagements;
        current.EventConditions.SelectedForce = state.NativeArmySimulation?.World.SelectedForce1656;
        CityCaptureTerritoryInputs.BindCurrentEmperor(state, current);
        current.EventConditions.CurrentYear = season.SecondDate.Year;
        var scenarioDate = season.SecondDate;
        current.EventConditions.ScenarioDate = new(scenarioDate.Year, scenarioDate.Month, scenarioDate.Day, scenarioDate.Hour);
        var date = season.ThirdDate;
        long elapsed = season.AdvancedPeriods;
        if (moment is not null)
        {
            if (moment.CycleWithinAtomicBlock is < 0 or >= NativeArmyProgressAtomicRuntime.CyclesPerSaveBlock ||
                moment.CalendarCycleOrdinal != checked(season.AdvancedCalendarSteps + moment.CycleWithinAtomicBlock))
                throw OccupationMissing("campaign_event_reached_clock");
            for (int i = 0; i < moment.CycleWithinAtomicBlock; i++)
            {
                var advanced = MapSeasonRuntime.StepCalendarWriter(date);
                date = advanced.Date;
                if (advanced.PeriodAdvanced) elapsed++;
            }
            if (date != moment.PreWriterThirdDate) throw OccupationMissing("campaign_event_reached_date");
        }
        current.EventConditions.CurrentDate = new(date.Year, date.Month, date.Day, date.Hour);
        current.EventConditions.PastGameTime = unchecked(world.ElapsedPeriodOffset + (int)elapsed);
        current.EventEligibility.IsPresent = true;
        for (int i = 0; i < 1024; i++)
        {
            current.SetWorldEventFlag(i, history.Occurred[i]);
            current.SetWorldResultFlag(i, history.Acknowledged[i]);
            var occurred = history.OccurrenceDates[i];
            current.EventConditions.SetOccurrenceDate(i, occurred is null ? new(0, 0, 0) : new(occurred.Year, occurred.Month, occurred.Day));
        }
        for (int i = 0; i < 992; i++) current.SetManagerEventFlag(i, manager.Blocked[i]);
        return current;
    }

    /// <summary>Ordinary full-pool scan outside event playback. All manager writes
    /// are visible to later conditions, then published together only on success.</summary>
    public CampaignEventConditionScan ScanOrdinaryCampaignEventConditions(GameState state, NativeArmyProgressStageMoment? moment = null)
        => ScanCampaignEventConditions(state, -1, moment);

    private CampaignEventConditionScan ScanCampaignEventConditions(GameState state, int userIndex, NativeArmyProgressStageMoment? moment = null, bool? applicationPresent = null)
    {
        var source = _campaignEvents.Value;
        if (state.ContentHashes.GetValueOrDefault(source.Source.ResourceUri) != source.Source.Sha256)
            throw OccupationMissing("campaign_event_resource_revision");
        var current = CaptureCurrentCampaignEventQueries(state, moment);
        current.ConditionApplicationPresent = applicationPresent;
        var result = S14EventEligibilityEvaluator.Scan(source.Document, current, new(0, 0, userIndex),
            _ => new S14PlayerEventConditionRuntime(current, _ => ReadOrdinaryEventDispatchInputs(state), current));
        var blocked = Enumerable.Range(0, 992).Select(i => OccupationKnown(current.ReadManagerEventFlag(i), "campaign_event_manager_result")).ToArray();
        var pool = current.ReadOwnedCurrentListPool() ?? throw OccupationMissing("campaign_event_list_pool");
        var next = state.WithArmyLifecycle(state.ReadArmyLifecycle()! with { Indexes = pool.ExportOwnedState() });
        return new(next with { CampaignEventEligibility = state.CampaignEventEligibility! with { Blocked = blocked } }, result);
    }

    public static S14ConditionDispatchInputs ReadOrdinaryEventDispatchInputs(GameState state)
    {
        int selector = OccupationKnown(BattleRemakeCurrentSettings.ReadSelectorE58(state), "event_current_settings_selector");
        int setting = selector == 0
            ? OccupationKnown(BattleRemakeCurrentSettings.ReadInt32(state, 0x1DC), "event_advanced_setting") == 1 ? 1 : 0
            : unchecked((sbyte)OccupationKnown(BattleRemakeCurrentSettings.ReadByte(state, 0x78), "event_primary_setting"));
        // Resource-load reset and no active playback mean skip is clear.
        // Each scan allocates a fresh execution with its execution gate clear.
        return new(false, setting, false);
    }
}
