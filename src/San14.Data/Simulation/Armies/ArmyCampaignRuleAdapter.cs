using San14.Core;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace San14.Data;

/// <summary>Production provider package for this game's owned campaign.
/// Source effects receive a complete operation-local Base/Hex capture. Source
/// Army construction and native ticks remain explicit opt-in providers.</summary>
public sealed partial class ArmyCampaignRuleAdapter : ITacticRuleSource,
    IPlayableTacticUseSource, IPlayableArmyCreationRules, IPlayableNativeArmyPreparationRules,
    IPlayableArmyCreationPreparationRules, IPlayableNativeTacticActivationTailSource
{
    private readonly GameResourceRepository _repository;
    private readonly ArmyOperationContext _definitions;
    private readonly LoadedScenario _scenario;
    internal int? ResolveOfficerSource(int officerId) => _definitions.ResolveOfficerSource(officerId);
    private readonly LoadedSkillDefinitions _skills;
    private readonly TacticSourceCatalog _catalog;
    private readonly LoadedMap _map;
    private readonly LoadedTerrainTypes _terrain;
    private readonly HashSet<int> _occupationSceneryHexes;
    private readonly TacticOrdinaryArmyUsageSourceProfile _usage;
    private readonly ArmyConstruction _creation;
    private readonly ArmyMoraleParameterSeed _moraleSeed;
    private readonly BattleRemakeCurrentParameterSeed _battleSeed;
    private readonly BattleRemakeCurrentRegionParameterSeed _regionSeed;
    private readonly ArmyMobilityParameterSeed _mobilitySeed;
    private readonly ArmyMarchPenaltyParameterSeed _marchPenaltySeed;
    private readonly TransportArmyRouteInputs _transportRouteInputs;
    private readonly NormalArmyCombatApplicationSourceParameters _normalApplicationSeed;
    private readonly PersonGrowthSource _growthSource;
    private readonly PersonGrowthCurrentQueries _growthQueries;
    private readonly PersonPendingMeritSource _pendingMerit;
    private readonly NormalArmyForceResultSourceParameters _forceResultSeed;
    private readonly PersonNormalArmyRewardSource _personReward;
    private readonly OwnedBaseCapacityInputs _baseCapacityInputs;
    private readonly OwnedArmyGovernorComparatorInputs _governorInputs;
    private readonly OfficerAftermathInputs _personDestructionInputs;
    private readonly ArmyDestructionParameterSeed _destructionSeed;
    private readonly NormalArmyAftermathInputs _normalAftermathInputs;
    private readonly OwnedPersonRelationInputs _personRelationInputs;
    private readonly PersonReturnPathParameterSeed _personReturnPathSeed;
    private readonly OwnedForce164Inputs _force164Inputs;
    private readonly PersonArmyCaptureInputs _personCaptureInputs;
    private readonly OwnedArmyMoraleWaveInputs _moraleWaveInputs;
    private readonly RulerCaptureReleaseInputs _rulerCaptureReleaseInputs;
    private readonly PersonAssignmentPrecallInputs _personAssignmentPrecallInputs;
    private readonly BaseCaptureResourceInputs _baseCaptureResourceInputs;
    private readonly BaseCaptureRewardInputs _baseCaptureRewardInputs;
    private readonly CityCaptureTerritoryInputs _cityCaptureTerritoryInputs;
    private readonly TacticRuleAdapter _ticks;
    private readonly IManualTacticPresentationAdmission? _automaticPresentationAdmission;
    public TacticRemakeParameterSeed Seed { get; }
    public string Revision { get; }
    public TacticRuleAdapter Tactics { get; }

    private ArmyCampaignRuleAdapter(GameResourceRepository repository, ArmyOperationContext definitions, LoadedScenario scenario,
        LoadedSkillDefinitions skills, TacticSourceCatalog catalog, LoadedMap map, LoadedTerrainTypes terrain,
        TacticRemakeParameterSeed seed, ArmyConstruction creation,
        ArmyMoraleParameterSeed moraleSeed, BattleRemakeCurrentParameterSeed battleSeed,
        BattleRemakeCurrentRegionParameterSeed regionSeed, ArmyMobilityParameterSeed mobilitySeed,
        ArmyMarchPenaltyParameterSeed marchPenaltySeed,
        NormalArmyCombatApplicationSourceParameters normalApplicationSeed,
        PersonGrowthSource growthSource, PersonGrowthCurrentQueries growthQueries,
        PersonPendingMeritSource pendingMerit,
        NormalArmyForceResultSourceParameters forceResultSeed,
        PersonNormalArmyRewardSource personReward, OwnedBaseCapacityInputs baseCapacityInputs,
        OwnedArmyGovernorComparatorInputs governorInputs,
        OfficerAftermathInputs personDestructionInputs,
        ArmyDestructionParameterSeed destructionSeed,
        NormalArmyAftermathInputs normalAftermathInputs,
        OwnedPersonRelationInputs personRelationInputs,
        PersonReturnPathParameterSeed personReturnPathSeed,
        OwnedForce164Inputs force164Inputs,
        PersonArmyCaptureInputs personCaptureInputs,
        OwnedArmyMoraleWaveInputs moraleWaveInputs,
        RulerCaptureReleaseInputs rulerCaptureReleaseInputs,
        PersonAssignmentPrecallInputs personAssignmentPrecallInputs,
        BaseCaptureResourceInputs baseCaptureResourceInputs,
        BaseCaptureRewardInputs baseCaptureRewardInputs,
        CityCaptureTerritoryInputs cityCaptureTerritoryInputs,
        LoadedExperienceData experience,
        string revision, IManualTacticPresentationAdmission? automaticPresentationAdmission)
    {
        if (string.IsNullOrWhiteSpace(revision)) throw new InvalidDataException("Owned Army package revision is missing.");
        _repository = repository;
        definitions.BindPortraitResources(repository);
        _automaticPresentationAdmission = automaticPresentationAdmission;
        _campaignEvents = new(() => repository.LoadEvents(CampaignEventCatalog.SourcePath));
        _definitions = definitions; _scenario = scenario; _skills = skills; _catalog = catalog; _map = map; _terrain = terrain;
        var sceneries = repository.LoadSceneries(scenario.FixedDataSource.SourcePath);
        _landmarkDefinitions = sceneries;
        _landmarkDevelopment = new(experience);
        if (sceneries.Source.Sha256 != scenario.FixedDataSource.Sha256 || sceneries.Catalog.Sceneries.Count != 101)
            throw new InvalidDataException("Army occupation requires the complete matching fixed scenery map.");
        //Fixed scenery44 is a map definition, as are terrain and Area3A.
        //The ordinary no-scenery leaf does not read mutable scenario flags47.
        //Valid registered sites write their fixed Hex through TC241AD0; retain
        //every such site even if its current special-site flag is unknown.
        _occupationSceneryHexes = sceneries.Catalog.Sceneries.Where(row => row.Slot != 0 &&
            row.RawHex44 < 48400 && map.Map.Cells[row.RawHex44].AreaSlot != 0).Select(row => (int)row.RawHex44).ToHashSet();
        Seed = seed; _creation = creation; _moraleSeed = moraleSeed; _battleSeed = battleSeed; _regionSeed = regionSeed;
        _mobilitySeed = mobilitySeed; _marchPenaltySeed = marchPenaltySeed; Revision = revision;
        _transportRouteInputs = TransportArmyRouteInputs.Load(repository, seed.Language);
        _normalApplicationSeed = normalApplicationSeed;
        _growthSource = growthSource;
        _growthQueries = growthQueries;
        _pendingMerit = pendingMerit;
        _forceResultSeed = forceResultSeed;
        _personReward = personReward;
        _baseCapacityInputs = baseCapacityInputs;
        _governorInputs = governorInputs;
        _personDestructionInputs = personDestructionInputs;
        _destructionSeed = destructionSeed;
        _normalAftermathInputs = normalAftermathInputs;
        _personRelationInputs = personRelationInputs;
        _personReturnPathSeed = personReturnPathSeed;
        _force164Inputs = force164Inputs;
        _personCaptureInputs = personCaptureInputs;
        _moraleWaveInputs = moraleWaveInputs;
        _rulerCaptureReleaseInputs = rulerCaptureReleaseInputs;
        _personAssignmentPrecallInputs = personAssignmentPrecallInputs;
        _baseCaptureResourceInputs = baseCaptureResourceInputs;
        _baseCaptureRewardInputs = baseCaptureRewardInputs;
        _cityCaptureTerritoryInputs = cityCaptureTerritoryInputs;
        _usage = new(seed.Language, SimulationRuleIdentity.ForLocale(seed.Language));
        _ticks = Context(definitions.CaptureSourcePrefix()).CreateRules(); Tactics = new(this);
    }

    /// <param name="automaticPresentationAdmission">An optional immutable host
    /// snapshot for the special presentation route. Absence stays unknown;
    /// ordinary effects do not require camera admission.</param>
    public static ArmyCampaignRuleAdapter FromOwnedCampaign(GameResourceRepository repository,
        LoadedScenario source, LoadedMap map, string language, string revision,
        IManualTacticPresentationAdmission? automaticPresentationAdmission = null)
    {
        LoadedTerrainTypes terrain = repository.LoadTerrainTypes($"0010_{language}/fixdataexce.s14");
        LoadedSkillDefinitions skills = repository.LoadSkillDefinitions($"0010_{language}/fixdataexce.s14");
        LoadedPersonalities personalities = repository.LoadPersonalities($"0010_{language}/fixdataexce.s14");
        var definitions = new ArmyOperationContext(source, map, terrain, skills, personalities,
            repository.LoadObjectDefinitions(source.FixedDataSource.SourcePath));
        TacticRemakeParameterSeed seed = TacticRemakeParameterSeed.LoadPinnedSeed(repository, language);
        LoadedExperienceData experience = repository.LoadExperienceData(ExperienceVariant.ExCe);
        return new(repository, definitions, source, skills, TacticSourceParameters.Read(skills.Formations, skills.Source), map, terrain,
            seed, ArmyConstruction.LoadCurrent(repository, language,
                ArmyCreationConfiguration.ObservedShortPacketFields),
            ArmyMoraleParameterSeed.LoadPinnedSeed(repository, language),
            BattleRemakeCurrentParameterSeed.Load(repository, language),
            BattleRemakeCurrentRegionParameterSeed.Load(repository, language),
            ArmyMobilityParameterSeed.Load(repository, language),
            ArmyMarchPenaltyParameterSeed.Load(repository, language),
            NormalArmyCombatApplicationSourceMath.LoadInitial(repository, language),
            new PersonGrowthSource(experience),
            PersonGrowthCurrentQueries.Load(repository, source, language),
            PersonPendingMeritSource.Load(repository, source, language),
            NormalArmyForceResultSource.LoadInitial(repository, language),
            PersonNormalArmyRewardSource.Load(repository, source, language),
            OwnedBaseCapacityInputs.Load(repository, source, language),
            OwnedArmyGovernorComparatorInputs.Load(repository, source, language),
            OfficerAftermathInputs.Load(repository, source, language),
            ArmyDestructionParameterSeed.Load(repository, language),
            NormalArmyAftermathInputs.Load(repository, language),
            OwnedPersonRelationInputs.Load(repository, source, language),
            PersonReturnPathParameterSeed.Load(repository, language),
            OwnedForce164Inputs.FromLoaded(experience, language),
            PersonArmyCaptureInputs.Load(source, terrain, language),
            OwnedArmyMoraleWaveInputs.Load(repository, source, language),
            RulerCaptureReleaseInputs.Load(repository, source, language),
            PersonAssignmentPrecallInputs.Load(repository, source, language),
            BaseCaptureResourceInputs.Load(repository, language),
            BaseCaptureRewardInputs.Load(repository, source),
            CityCaptureTerritoryInputs.Load(repository, language), experience, revision, automaticPresentationAdmission);
    }

    /// <summary>Constructs the selected fresh scenario's current map objects
    /// before preview. Native clocks, gameplay seeds and readiness stay separate.</summary>
    public GameState CreateFreshScenarioMapObjects(GameState state)
    {
        if (state.Phase != TurnPhase.Initializing || state.NativeClock is not null ||
            state.NativeArmySimulation is not null || state.ScenarioKey != _scenario.ScenarioSource.ResourceUri ||
            state.MapSha256 != _map.Source.Sha256 ||
            state.ContentHashes.GetValueOrDefault(_scenario.ScenarioSource.ResourceUri) != _scenario.ScenarioSource.Sha256 ||
            state.ContentHashes.GetValueOrDefault(_scenario.FixedDataSource.ResourceUri) != _scenario.FixedDataSource.Sha256)
            throw new InvalidDataException("Fresh preview map objects require the selected initializing scenario.");
        return OwnedMapObjectLifecycle.InstallFresh(state, _scenario,
            _repository.LoadObjectDefinitions(_scenario.FixedDataSource.SourcePath), _definitions.CaptureSourcePrefix());
    }

    public GameState CreateFreshOwnedSimulation(GameState state)
    {
        if (_scenario.Snapshot.WorldParameters is not { } world || world.RawBytes.Length < 100 ||
            Convert.ToHexString(SHA256.HashData(world.RawBytes)) != world.WorldSha256 ||
            state.ContentHashes.GetValueOrDefault(_scenario.ScenarioSource.ResourceUri) != _scenario.ScenarioSource.Sha256)
            throw new InvalidDataException("Fresh owned sampler epoch requires its complete actual scenario World.");
        GameState fresh = _marchPenaltySeed.InstallFresh(_mobilitySeed.InstallFresh(
            _battleSeed.InstallFresh(Seed.CreateFreshOwnedSimulation(state, Revision), Revision)));
        fresh = InstallFreshCampaignEndingBranchInputs(fresh);
        bool hasMapObjects = fresh.MapObjects is not null;
        bool hasMapObjectSource = CampaignSourceKnowledge.From(fresh.ResourceState).MapObjectSource is not null;
        if (hasMapObjects != hasMapObjectSource)
            throw new InvalidDataException("Fresh map objects and their source owner must be present together.");
        if (!hasMapObjects)
            fresh = OwnedMapObjectLifecycle.InstallFresh(fresh, _scenario,
                _repository.LoadObjectDefinitions(_scenario.FixedDataSource.SourcePath), _definitions.CaptureSourcePrefix());
        else
            OwnedMapObjectLifecycle.Validate(fresh);
        fresh = ApplicationCommandAccountingSource.InstallFresh(fresh);
        fresh = ArmyMarchParameterSeed.InstallFresh(fresh);
        fresh = NormalArmyCombatApplicationSourceMath.InstallFresh(fresh, _normalApplicationSeed, Revision);
        fresh = NormalArmyForceResultSource.InstallFresh(fresh, _forceResultSeed, Revision);
        fresh = StructureAttackParameters.InstallFresh(fresh, Seed.Language);
        fresh = NormalArmyForceFreshAllocation.InstallFresh(fresh, _scenario, _forceResultSeed);
        fresh = _normalAftermathInputs.InstallFresh(fresh, _scenario);
        fresh = _destructionSeed.InstallFresh(fresh);
        fresh = _personRelationInputs.InstallFresh(fresh);
        fresh = _personReturnPathSeed.InstallFresh(fresh);
        fresh = _transportRouteInputs.InstallFresh(fresh);
        fresh = _force164Inputs.InstallFresh(fresh);
        fresh = _moraleWaveInputs.InstallFresh(fresh);
        fresh = PersonDeathReportInputs.InstallFresh(fresh, Seed.Language);
        fresh = PersonDeathReportInputs.InstallFreshPopularityScale(fresh, _scenario, Seed.Language);
        fresh = AnnualPersonLifespanNotificationSource.InstallFreshParameters(fresh, _scenario, Seed.Language);
        fresh = _rulerCaptureReleaseInputs.InstallFresh(fresh);
        fresh = CaptiveReleaseInitialParameters.InstallFresh(fresh, _scenario);
        fresh = CaptureBaseThreatInputs.InstallFresh(fresh, _scenario);
        fresh = CommanderSelectionSource.InstallFresh(fresh);
        fresh = _baseCaptureResourceInputs.InstallFresh(fresh);
        fresh = _baseCaptureRewardInputs.InstallFresh(fresh);
        fresh = DeploymentDraftDefaults.InstallFresh(fresh);
        // This newly constructed remake campaign owns its own manager. Restore
        // never enters this method and cannot synthesize an empty saved queue.
        if (fresh.ManualTacticIntervention is not null) throw new InvalidDataException("Fresh manual tactic manager would replace a saved owner.");
        fresh = fresh with { ManualTacticIntervention = ManualTacticInterventionState.Empty };
        var epoch = new NativeArmySamplerEpoch(_scenario.ScenarioSource.ResourceUri, _scenario.ScenarioSource.Sha256,
            world.WorldSha256, BinaryPrimitives.ReadInt32LittleEndian(world.RawBytes.AsSpan(80, 4)),
            BinaryPrimitives.ReadInt32LittleEndian(world.RawBytes.AsSpan(96, 4)), fresh.NativeClock!.Revision,
            "san14:ce-world-sampler-epoch-20261002");
        GameState result = fresh with { NativeArmySimulation = fresh.NativeArmySimulation! with
            { World = fresh.NativeArmySimulation!.World with { SamplerEpoch = epoch } } };
        SourceWorldContext definitions = _definitions.Capture(result)
            ?? throw new InvalidDataException("Fresh owned Person source prefix is unresolved.");
        GameState people = new TacticRemakeCurrentAbilities(definitions, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language))
            .CreateFreshOwnedPeople(result);
        GameState currentFields = _personDestructionInputs.InstallFresh(
            OwnedArmyReturnCurrentFields.InstallFresh(people, _definitions));
        GameState growth = _growthSource.InstallFresh(currentFields, _scenario,
            CaptureCurrentDefinitions(currentFields) ?? throw new InvalidDataException("Fresh growth current Person capture is unresolved."));
        GameState formations = TacticRemakeCurrentFormations.CreateFreshOwnedDefinitions(growth, _skills);
        formations = DeploymentTacticCurrentInputs.InstallFresh(formations, definitions);
        GameState policies = BattleRemakeCurrentForcePolicies.InstallFresh(formations, _scenario);
        policies = _personAssignmentPrecallInputs.InstallFresh(policies);
        policies = AreaAdministrationUpkeep.InstallFresh(policies);
        GameState morale = _moraleSeed.InstallFresh(policies, definitions, policies.NativeArmySimulation!.Parameters.Revision);
        GameState control = TacticRemakeCurrentBattleControl.CreateFreshOwnedControl(morale);
        GameState settings = BattleRemakeCurrentSettings.InstallFresh(control, _scenario);
        //These are source-field/constant owners, independent of credit and
        //area aggregates. Construct the actual registries before3361D0 removes
        //women and held items, rather than approximating those writes later.
        GameState returnServices = OwnedArmyReturnServiceInputs.InstallFresh(
            _governorInputs.InstallFresh(_baseCapacityInputs.InstallFresh(settings)));
        GameState lifecycle = InstallFreshArmyLifecycle(HistoricalOfficerScenario.InstallFreshFlags(
            returnServices, _scenario.InitialHistoricalOfficerEditsAdopted));
        var commandWorld = CaptureOwnedArmyReturnWorld(lifecycle);
        GameState capturedItems = _personCaptureInputs.InstallFresh(lifecycle, commandWorld.Context);
        //Construct the fresh condition/service owners before226480 resets
        //selected women; a later constructor must not overwrite those resets.
        capturedItems = CreateFreshDuelService(capturedItems);
        var initializationPeople = commandWorld.Context;
        var initializationPool = initializationPeople.ReadOwnedCurrentListPool()
            ?? throw new InvalidDataException("Fresh current Person registry is unresolved.");
        var female = ApplyFreshFemaleSetting(capturedItems, initializationPeople, initializationPool);
        //3361D0 precedes335E90's city writes and bloodline candidate collection;
        //the real3255D0 credit pass follows all three transformations.
        GameState appearance = NewGameWanderingCityInitialization.Apply(female.State, initializationPeople);
        GameState family = NewGameBloodlineInitialization.Apply(appearance, initializationPeople);
        _personRelationInputs.BindCurrent(family, initializationPeople);
        GameState credit = PersonCreditFreshSource.InstallFresh(family, _scenario, initializationPeople, female.ResetIdentities);
        GameState region = _regionSeed.InstallFresh(_personReward.InstallFresh(
            _pendingMerit.InstallFresh(_growthQueries.InstallFresh(credit))), Revision);
        GameState areas = AreaStatisticsSourceAdapter.Initialize(region, _scenario, _map, _terrain);
        GameState bases = BaseCaptureCurrentInputs.InstallFresh(ForceUnregisterInputs.InstallFresh(
            PersonReturnCommands.InstallFresh(areas, initializationPeople), _scenario), _scenario);
        GameState cityCapture = _cityCaptureTerritoryInputs.InstallFresh(
            BaseSupplyExpenseAccumulatorSource.InstallFresh(BaseReturnCaptivesInputs.InstallFresh(bases)), _scenario);
        var completed = CityCaptureFoodWarningSource.InstallFreshInputs(cityCapture, _repository);
        var initialized = ArmyChangedListsSource.InstallFresh(completed, CaptureOwnedArmyReturnWorld(completed).Context);
        InitializeMapArmyGroupsKnownReset(Enumerable.Range(0, 501));
        var ready = InstallFreshCampaignEventState(InstallFreshLandmarkInvestments(SubdayStatusParameters.InstallFresh(FireCommandParameters.InstallFresh(
            HexFireParameters.InstallFresh(BaseWoundedRecoveryParameters.InstallFresh(initialized)))))) with { RegionOccupationFollowups = new([], []) };
        ready = OriginalAiStrategyMemoryLifecycle.InitializeFresh(ready);
        ready = MonthlyCampaignMaintenanceRules.InstallFresh(ready);
        ready = InstallFreshNormalTurnForeignRelations(ready);
        ready = InstallFreshForeignTradeProgress(ready);
        ready = InstallFreshNormalTurnTradeAdmissionParameters(ready);
        ready = InstallFreshNormalTurnDistrictHistory(ready);
        ready = InstallFreshNormalTurnChallengeService(ready);
        ready = CityDomesticParameters.InstallFresh(ready, _repository.LoadCityDomesticParameters(Seed.Language), _scenario);
        ready = PersonnelCommandParameters.InstallFresh(ready);
        ready = EmperorCaptureConfiguration.InstallFresh(ready, Seed.Language);
        ready = NormalTurnPersonnelLoyaltyParameters.InstallFresh(ready, Seed.Language);
        ready = NormalTurnDisasterParameters.InstallFresh(ready, Seed.Language);
        ready = GovernanceRosterParameters.InstallFresh(ready);
        ready = CityFoodIncomeSource.InstallFreshParameters(ready);
        ready = ArmyFoodExpenseParameters.InstallFresh(ready);
        ready = GarrisonFoodShortageParameters.InstallFresh(ready);
        return TerrainDamageParameters.InstallFresh(FireDamageParameters.InstallFresh(TrapParameters.InstallFresh(ArmyTraitMaintenanceParameters.InstallFresh(FacilityRepairParameters.InstallFresh(InstallFreshBaseDamageTiers(RefreshDistrictCommandBudgets(DistrictCommandBudgetParameters.InstallFresh(FalseReportParameters.InstallFresh(ready))))))))) with
            { NormalCombatRefresh = new(0, []), CampaignEffectRequests = CampaignEffectRequests.Empty,
                ArmyMapSpeech = new(1, []) };
    }
    public IPlayableCityEntryStateRules CreateCityEntryStateRules() =>
        new OwnedArmyOrdinaryCityReturnStage(this, _governorInputs);
    public SourceWorldContext? CaptureCurrentDefinitions(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        //The prefix is used only for immutable definitions. Once these query
        //owners exist, their mutable bytes come from the saved current fields.
        //Absent location owners overlay null masks; a reached conditional trait
        //must remain unknown while permanent traits can still resolve.
        SourceWorldContext? current = _definitions.Capture(state);
        if (current is null) return null;
        current.OverlayOwnedArmyReturnCurrentFields(state);
        _personDestructionInputs.BindCurrent(state, current);
        current.CurrentPlayerForce = OwnedCurrentPlayerForce.ReadCurrentReference(state, current);
        _baseCapacityInputs.BindCurrent(state, current);
        PersonAdvisorResetSource.BindCurrent(state, current);
        CaptureBaseThreatInputs.BindCurrent(state, current);
        OwnedArmyDestructionCurrent.BindOwnedCurrentContext(state, current);
        ForceUnregisterInputs.BindCurrent(state, current);
        BaseCaptureCurrentInputs.BindCurrent(state, current);
        return current;
    }

    /// <summary>Bind the finite saved World3A store only at normal new-game
    /// confirmation, using its actual selected ruler Person.</summary>
    public GameState ConfirmNewGamePlayerPerson(GameState state, int confirmedPersonId)
    {
        var current = CaptureCurrentDefinitions(state)
            ?? throw new InvalidDataException("Confirmed player lacks current Person/District owners.");
        var confirmed = OwnedCurrentPlayerForce.ConfirmNewGamePlayerPerson(state, current, confirmedPersonId);
        //33CCA0 writes World3A, then2EE410 applies this actual player's Rank
        //capacity. Restoring or querying a campaign never enters this writer.
        return ApplyConfirmedPlayerCommanderCapacity(confirmed, current);
    }

    /// <summary>Read-only rehearsal over the ordered pending allocations. This
    /// is a preparation availability check, not an original command formula or
    /// a promise that later current owners will remain unchanged. Only the real
    /// execution can publish native creation, debit resources and advance RNG.</summary>
    public DeploymentEligibility CheckPreparedDeployments(GameState preparedState)
    {
        ArgumentNullException.ThrowIfNull(preparedState);
        if (preparedState.Phase != TurnPhase.Strategy || preparedState.NativeClock is null ||
            preparedState.NativeArmySimulation is null)
            return Unavailable("The pending native Army queue lacks its owned strategy clock or simulation.");
        GameState current = ArmyIdentityAllocation.Initialize(preparedState) with
        {
            Cities = new(preparedState.Cities), Gates = new(preparedState.Gates),
            Districts = new(preparedState.Districts), Officers = new(preparedState.Officers),
            Units = new(preparedState.Units)
        };
        if (current.ArmyIdentities!.NextId + preparedState.PreparedDeployments.Count - 1 > int.MaxValue)
            return Unavailable("The campaign army identity sequence is exhausted.");
        foreach (PreparedDeployment plan in preparedState.PreparedDeployments.Values.OrderBy(plan => plan.Id))
        {
            int nextId = current.ArmyIdentities!.Candidate;
            int? sourceHex;
            int morale;
            if (current.Cities.TryGetValue(plan.SourceLocationId, out CityState? city))
            {
                sourceHex = city.HexId; morale = city.Morale ?? 100;
                if (!plan.TroopsDebited)
                {
                    if (city.Troops < plan.Troops) return Unavailable("The pending source troop debit is unavailable.");
                    current.Cities[city.Id] = city with { Troops = city.Troops - plan.Troops };
                }
            }
            else if (current.Gates.TryGetValue(plan.SourceLocationId, out GateState? gate))
            {
                sourceHex = gate.HexId; morale = 100;
                if (!plan.TroopsDebited)
                {
                    if (gate.Troops < plan.Troops) return Unavailable("The pending source troop debit is unavailable.");
                    current.Gates[gate.Id] = gate with { Troops = gate.Troops - plan.Troops };
                }
            }
            else return Unavailable("The pending source Base no longer exists.");
            if (sourceHex is not int hex) return Unavailable("The pending source Base Hex is unknown.");
            if (!plan.CommandBookDebited)
            {
                if (!current.Districts.TryGetValue(plan.DistrictId, out DistrictState? district) ||
                    district.OrdersRemaining is not int remaining || remaining < 1)
                    return Unavailable("The pending district command debit is unavailable.");
                current.Districts[district.Id] = district with { OrdersRemaining = remaining - 1 };
            }
            var army = new UnitState(nextId, plan.ForceId, plan.OfficerId, hex, hex,
                plan.Troops, 0, new(morale, null))
            {
                SourceLocationId = plan.SourceLocationId, Formations = new(plan.FormationId, null, null),
                TacticIds = plan.TacticIds.ToArray(), WoundedTroops = 0
            };
            PlayableArmyCreationResolution? receipt = Create(current, plan, army);
            if (receipt is null || current.ReadArmyLifecycle() is not null && receipt.Lifecycle is null)
                return Unavailable($"Current source construction is unavailable for officer {plan.OfficerId}.");
            //Only this detached rehearsal owns these objects. Record real
            //receipt allocation identity so the next preview cannot reuse it.
            current.Units.Add(army.Id, army with
            {
                NativeArmyOrigin = receipt.Origin, Formations = receipt.Formations, FieldAttackPolicy = receipt.FieldAttackPolicy, TraitRoster = receipt.TraitRoster,
                NativeTacticRuntime = receipt.Runtime, MoraleState = army.MoraleState with { CurrentValue = receipt.CurrentMorale },
                NativeSavedRegionHex152 = receipt.NativeSavedRegionHex152,
                March = receipt.March,
                PositionKnowledge = receipt.CurrentHexId.HasValue ? ArmyPositionKnowledge.Confirmed : ArmyPositionKnowledge.Unknown,
                NativeDirection36 = receipt.NativeDirection36,
                NativeMapUiByte13C = receipt.NativeMapUiByte13C,
                NativeCommandControl140 = receipt.NativeCommandControl140,
                NativeRouteHold56 = receipt.NativeRouteHold56, NativeRouteGoal1F8 = receipt.NativeRouteGoal1F8,
                NativeHomeSequence28 = receipt.NativeHomeSequence28, AiActionFlags = receipt.AiActionFlags,
                CarriedStores = receipt.CarriedStores, OverrideTarget = receipt.OverrideTarget,
                WoundedTroops = receipt.WoundedTroops
            });
            OfficerState commander = current.Officers[plan.OfficerId];
            current.Officers[plan.OfficerId] = commander with
            {
                Deployed = true,
                LocationState = receipt.CommanderPosition is { } position
                    ? commander.LocationState is { } location
                        ? location with { Position = position }
                        : new(position, null)
                    : commander.LocationState
            };
            current = current with { RandomState = receipt.RandomState,
                ResourceState = receipt.ResourceState ?? current.ResourceState,
                ArmyLifecycle = receipt.Lifecycle ?? current.ArmyLifecycle,
                ArmyMapOccupancy = receipt.ArmyMapOccupancy ?? current.ArmyMapOccupancy };
            current = ArmyIdentityAllocation.Commit(current, army.Id);
        }
        return new(DeploymentDecisionStatus.Allowed);

        static DeploymentEligibility Unavailable(string explanation) => new(
            DeploymentDecisionStatus.Unavailable, explanation, PlayableArmyCreationPreparationGuard.MissingRuleKey);
    }

    /// <summary>Actual2706A0 nonzero native morale writer. The unowned zero
    /// routing/event branch returns the original state without its prefix store.
    /// This callback does not complete the surrounding ordinary application.</summary>
    public ArmyMoraleAdjustmentResult TryApplyNativeMoraleAdjustment(GameState state, int armyId, int adjustment)
    {
        ArgumentNullException.ThrowIfNull(state);
        SourceWorldContext? definitions = CaptureCurrentDefinitions(state);
        return definitions is null
            ? new(ArmyMoraleAdjustmentOutcome.CurrentInputsUnresolved, state, null)
            : new ArmyMoraleAdjustmentSource(Context(definitions), definitions, Revision)
                .TryApply(state, armyId, adjustment);
    }

    internal ArmyMoraleAdjustmentResult ApplyMoraleDuringArmyOperation(GameState state, int armyId, int adjustment)
    {
        SourceWorldContext? definitions = CaptureCurrentDefinitions(state);
        return definitions is null
            ? new(ArmyMoraleAdjustmentOutcome.CurrentInputsUnresolved, state, null)
            : new ArmyMoraleAdjustmentSource(Context(definitions), definitions, Revision)
                .TryApplyWithinArmyOperation(state, armyId, adjustment);
    }

    public PersonGrowthResult TryApplyNativePersonGrowth(GameState state, int officerId,
        int abilityIndex, int requestedGrowth, int reportContext = 0)
    {
        var current = CaptureCurrentDefinitions(state);
        return current is null ? new(PersonGrowthOutcome.CurrentInputsUnresolved, state, null) :
            _growthSource.TryApply(state, current, _growthQueries, officerId, abilityIndex, requestedGrowth, reportContext);
    }

    /// <summary>Actual169320 pending-word callback. It preserves XP, merit,
    /// reward counts and clock. Missing reached current inputs publish no writes;
    /// the enclosing ordinary battle application remains a separate transaction.</summary>
    public PersonPendingMeritResult TryApplyNativePendingMerit(GameState state, int officerId, int requested)
    {
        var current = CaptureCurrentDefinitions(state);
        return current is null ? new(PersonPendingMeritOutcome.CurrentInputsUnresolved, state, null) :
            _pendingMerit.TryApply(state, current, officerId, requested);
    }

    public OfficerState? ResolveNativeActivationCommanderTail(GameState state, UnitState caster,
        PlayableTacticDefinition definition) => definition.NativeActivationWaiting is null ? null :
        Capture(state)?.ResolveOrdinaryActivationCommanderTail(state, caster, definition.TacticId,
            TryApplyNativePendingMerit);

    public NormalArmyForceResultApplication TryApplyNativeForceResult(GameState state, int attackerForceSlot,
        int targetForceSlot, int cappedLoss, bool killed, INormalArmyForceResultDefinitionQueries registry)
    {
        var current = CaptureCurrentDefinitions(state);
        return current is null ? new(NormalArmyForceResultOutcome.CurrentInputsUnresolved, state, null) :
            NormalArmyForceResultSource.TryApply(state, attackerForceSlot, targetForceSlot, cappedLoss,
                killed, current, registry, _forceResultSeed, Revision);
    }

    internal NormalArmyForceResultApplication ApplyForceResultDuringArmyOperation(GameState state, int attackerForceId,
        int targetForceId, int cappedLoss, bool killed, INormalArmyForceResultDefinitionQueries registry)
    {
        var current = CaptureCurrentDefinitions(state);
        return current is null ? new(NormalArmyForceResultOutcome.CurrentInputsUnresolved, state, null) :
            NormalArmyForceResultSource.ApplyWithinArmyOperation(state, attackerForceId, targetForceId, cappedLoss,
                killed, current, registry, _forceResultSeed, Revision);
    }

    public PersonNormalArmyRewardResult TryApplyNativeNormalPersonReward(GameState state,
        int officerId, int kind, int amount)
    {
        var current = CaptureCurrentDefinitions(state);
        return current is null ? new(PersonNormalArmyRewardOutcome.CurrentInputsUnresolved, state, null, "current-definitions") :
            _personReward.TryApply(state, current, Context(current), this, officerId, kind, amount);
    }

    internal PersonNormalArmyRewardResult TryApplyNativeNormalPersonRewardDuringArmyOperation(GameState state,
        int officerId, int kind, int amount)
    {
        var current = CaptureCurrentDefinitions(state);
        return current is null ? new(PersonNormalArmyRewardOutcome.CurrentInputsUnresolved, state, null, "current-definitions") :
            _personReward.TryApplyWithinArmyOperation(state, current, Context(current), this, officerId, kind, amount);
    }

    internal PersonNormalArmyRewardResult TryApplyNativeNormalPersonRewardDuringArmyOperation(GameState state,
        SourceWorldContext current, int officerId, int kind, int amount) =>
        _personReward.TryApplyWithinArmyOperation(state, current, Context(current), this, officerId, kind, amount);

    internal PersonGrowthResult ApplyPersonGrowthDuringArmyOperation(GameState state, int officerId,
        int abilityIndex, int requestedGrowth, int reportContext)
    {
        var current = CaptureCurrentDefinitions(state);
        return current is null ? new(PersonGrowthOutcome.CurrentInputsUnresolved, state, null) :
            _growthSource.TryApplyWithinArmyOperation(state, current, _growthQueries, officerId, abilityIndex, requestedGrowth, reportContext);
    }

    internal PersonPendingMeritResult ApplyPendingMeritDuringArmyOperation(GameState state, int officerId, int requested)
    {
        var current = CaptureCurrentDefinitions(state);
        return current is null ? new(PersonPendingMeritOutcome.CurrentInputsUnresolved, state, null) :
            _pendingMerit.TryApplyWithinArmyOperation(state, current, officerId, requested);
    }

    /// <summary>Reached hour10 candidate generation over the caller's live
    /// operation. Mixed ordering is retained across borrowed ordinary Army and
    /// City-origin applications. Every unclosed reached branch rejects this
    /// entire private operation rather than skipping a pair or changing pools.</summary>
    internal GameState CompleteCurrentArmyCombatCut(GameState state, SourceWorldContext current,
        NativeMapSeasonDate date, long revision)
        => CompleteCurrentArmyCombatCut(state, current, date, revision, out _, out _);

    private GameState CompleteCurrentArmyCombatCut(GameState state, SourceWorldContext current,
        NativeMapSeasonDate date, long revision, out int armyPairCount,
        out NativeArmyCombatCycleSampler? cycleSampler)
    {
        armyPairCount = 0;
        cycleSampler = null;
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(current);
        if (date.Hour != 10 || revision < 0)
            throw new InvalidDataException("ordinary_army_combat_cut.actual_hour10");
        var pool = current.ReadOwnedCurrentListPool();
        if (pool is null || !pool.OwnsArmyReturnProfile() ||
            !ReferenceEquals(pool.ArmyReturnOwner, current))
            throw new InvalidDataException("ordinary_army_combat_cut.current_pool_owner");

        //Rebind only saved views consumed here. Neither binder imports a pool,
        //creates a current, advances a clock, or publishes a combat result.
        OwnedArmyDestructionCurrent.BindOwnedCurrentContext(state, current);
        CaptureBaseThreatInputs.BindCurrent(state, current);
        //A fresh capture imports association order, not task payload fields.
        //Bind the saved task owner once; a borrowed operation can already own
        //a later manager and must retain its current raw58/member chains.
        if (current.ReadOwnedPersonCommandManager() is null)
            PersonReturnCommands.BindCurrent(state, current);
        //The ordinary campaign owns no tutorial/challenge service. Match the
        //same explicit absent owner used by normal return and occupation.
        current.ArmyReturnTransferMode = new(false, null);
        var queries = new ArmyMapGroupingCurrentQueries(state, current, _scenario, _governorInputs,
            Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
        var retainedCombat = state.NormalCombatImmediateContinuation;
        var projection = retainedCombat is null
            ? new NormalCombatMixedProducer(state, current, Context(current), queries).Generate()
            : RestoreImmediateCombatProjection(retainedCombat);
        var season = state.NativeMapSeason ?? throw new InvalidDataException("ordinary_army_combat_cut.calendar");
        var moment = new NormalArmyCombatPhaseMoment(revision, date, 13, season.SourceScenarioUri,
            season.SourceScenarioSha256, season.SourceWorldSha256, NormalArmyCombatCalendarRuntime.Revision);
        var sampler = retainedCombat is not null
            ? NativeArmyCombatCycleSampler.RestoreCombatCut(state, moment, retainedCombat.SampleDay, retainedCombat.SampleSalt)
            : NativeArmyCombatCycleSampler.Create(new(moment, state, NativeArmyProgressAtomicRuntime.Revision))
                ?? throw new InvalidDataException("ordinary_army_combat_cut.actual_cycle_sampler");
        cycleSampler = sampler;
        armyPairCount = projection.OrderedPairs.Count(pair => pair.Actor.Kind == 27 && pair.Target.Kind == 27);
        if (projection.OrderedPairs.Length == 0) return state;
        NormalArmyCombatDescriptor Descriptor(NormalCombatParticipant participant) => new(
            participant.UnitId ?? throw new InvalidDataException("ordinary_army_combat_cut.Army_identity"),
            checked((ushort)participant.PhysicalSlot), participant.Morale, participant.Troops,
            participant.HexId, participant.ForceSlot, participant.AcceptedCount);
        var descriptors = retainedCombat is null ? new Dictionary<int, NormalArmyCombatDescriptor>()
            : RestoreImmediateCombatDescriptors(retainedCombat);
        foreach (var row in projection.Actors)
            if (row.Actor.Kind == 27)
            {
                var descriptor = Descriptor(row.Actor);
                if (retainedCombat is null) descriptors[descriptor.ArmyId] = descriptor;
            }
        var pairs = projection.OrderedPairs.Where(pair => pair.Actor.Kind == 27 && pair.Target.Kind == 27)
            .Select((pair, index) => new NormalArmyCombatPair(index,
            Descriptor(pair.Actor), Descriptor(pair.Target))).ToArray();
        foreach (var pair in projection.OrderedPairs)
            if (pair.Target.Kind == 27)
            {
                var descriptor = Descriptor(pair.Target);
                descriptors.TryAdd(descriptor.ArmyId, descriptor);
            }
        var plan = new NormalArmyCombatPlan(moment, pairs, state.RandomState, "ordinary-current-mixed-Army-pairs:" + Revision);
        var effects = new List<CampaignEffectRequest>();
        var retainedIdentities = retainedCombat?.ArmyDescriptors.ToDictionary(item => item.Key,
            item => (Officer: item.Value.OfficerId, Slot: (int)item.Value.NativePoolSlot));
        var phase = pairs.Length == 0 ? null : CaptureNormalArmyCombatPhase(state, moment, sampler, current, plan, descriptors, effects, retainedIdentities)
            ?? throw new InvalidDataException("ordinary_army_combat_cut.current_phase_inputs");
        var sourceQueries = new TacticRemakeCurrentBattleQueries(Context(current, sampler), current, Revision,
            currentPhaseDescriptors: descriptors);
        var baseCounts = new Dictionary<(int Kind, int Physical), byte>();
        foreach (var row in projection.Actors)
            if (row.Actor.Kind is 5 or 6)
                baseCounts[(row.Actor.Kind, row.Actor.PhysicalSlot)] = row.Actor.AcceptedCount;
        int nextPairIndex = retainedCombat?.PairIndex ?? 0;
        int armyPairIndex = projection.OrderedPairs.Take(nextPairIndex)
            .Count(pair => pair.Actor.Kind == 27 && pair.Target.Kind == 27);
        var working = state;
        if (retainedCombat is not null)
        {
            var interrupted = projection.OrderedPairs[nextPairIndex];
            working = ResumeImmediateCombatSettlementCurrent(working, current, date, revision, effects);
            if (HasNativeCampaignStop(working)) return PublishStoppedCombat();
            if (working.BaseCaptureContinuation is not null || AutomaticEmperorCaptureLifecycle.HasCaptureTail(working))
                return RetainSuspendedMixedPair(nextPairIndex, retainedCombat.ReportWord20,
                    retainedCombat.AutomaticExecution, retainedCombat.BaseAutomaticExecution,
                    retainedCombat.PendingAutomaticFatalFeedback);
            if (interrupted.Actor.Kind == 27 && interrupted.Target.Kind == 27 && phase is not null)
            {
                if (retainedCombat.AutomaticExecution is { } automaticExecution)
                {
                    var resumedPair = phase.ResumeOrdinaryPairAfterAutomatic(working, armyPairIndex++,
                        retainedCombat.ReportWord20, automaticExecution, new NormalArmyForceResultDefinitions(current));
                    if (resumedPair.Outcome is NormalArmyNonlethalPairOutcome.CurrentInputsUnresolved or
                        NormalArmyNonlethalPairOutcome.DestructionRequired)
                        throw ImmediateCombatMissing(resumedPair.MissingOwner ?? "resumed_automatic_pair");
                    working = resumedPair.State;
                    if (HasNativeCampaignStop(working)) return PublishStoppedCombat();
                    if (resumedPair.Outcome == NormalArmyNonlethalPairOutcome.AwaitingPlayerDecision)
                        return RetainSuspendedPair(resumedPair, nextPairIndex);
                }
                else working = phase.CompletePairAfterImmediateDefeat(working, armyPairIndex++, retainedCombat.ReportWord20);
            }
            else if (interrupted.Actor.Kind == 27 && interrupted.Target.Kind is 5 or 6 &&
                retainedCombat.AutomaticExecution is { } baseAutomatic)
            {
                var resumedPair = ResumeBorrowedArmyBasePairAfterAutomatic(working, current, interrupted,
                    retainedCombat.ReportWord20, baseAutomatic, sampler, date, revision, effects);
                working = resumedPair.State;
                if (HasNativeCampaignStop(working)) return PublishStoppedCombat();
                if (resumedPair.SuspendedWord20 is ushort word20)
                    return RetainSuspendedMixedPair(nextPairIndex, word20, resumedPair.AutomaticExecution,
                        resumedPair.BaseAutomaticExecution, resumedPair.PendingAutomaticFatalFeedback);
            }
            else if (interrupted.Actor.Kind == 27 && interrupted.Target.Kind is 5 or 6 &&
                retainedCombat.AutomaticExecution is null && retainedCombat.BaseAutomaticExecution is null)
                working = CompleteBorrowedArmyBasePairAfterCapture(working, current, interrupted,
                    retainedCombat.ReportWord20, retainedCombat.PendingAutomaticFatalFeedback, sampler);
            else if (interrupted.Actor.Kind is 5 or 6 && interrupted.Target.Kind == 27 &&
                retainedCombat.BaseAutomaticExecution is { } sourceBaseAutomatic)
            {
                var resumedPair = ResumeBorrowedBaseArmyPairAfterAutomatic(working, current, interrupted,
                    retainedCombat.ReportWord20, sourceBaseAutomatic, sampler, date, revision, effects);
                working = resumedPair.State;
                if (HasNativeCampaignStop(working)) return PublishStoppedCombat();
                if (resumedPair.SuspendedWord20 is ushort word20)
                    return RetainSuspendedMixedPair(nextPairIndex, word20, null, resumedPair.BaseAutomaticExecution,
                        resumedPair.PendingAutomaticFatalFeedback);
            }
            else if (interrupted.Actor.Kind is 5 or 6 && interrupted.Target.Kind == 27 &&
                retainedCombat.AutomaticExecution is null)
                working = CompleteBorrowedBaseArmyPairTail(working, current, interrupted,
                    retainedCombat.ReportWord20, sampler, effects);
            else throw ImmediateCombatMissing("resume_mixed_pair_kind");
            working = working with { NormalCombatImmediateContinuation = null };
            nextPairIndex++;
        }
        for (int mixedPairIndex = nextPairIndex; mixedPairIndex < projection.OrderedPairs.Length; mixedPairIndex++)
        {
            var pair = projection.OrderedPairs[mixedPairIndex];
            if (pair.Actor.Kind == 27 && pair.Target.Kind == 27)
            {
                var completed = phase!.TryCompleteOrdinaryPair(working, armyPairIndex++,
                    new NormalArmyForceResultDefinitions(current));
                if (completed.Outcome is NormalArmyNonlethalPairOutcome.CurrentInputsUnresolved or
                    NormalArmyNonlethalPairOutcome.DestructionRequired)
                {
                    var failure = new InvalidDataException("ordinary_army_combat_cut." + completed.MissingOwner);
                    //Failure-only retained receipts. Do not select, sample, capture,
                    //serialize or scan the world again to explain a rejected pair.
                    failure.Data["CombatCalendarCycleOrdinal"] = revision;
                    failure.Data["CombatDate"] = date;
                    failure.Data["CombatArmyPairIndex"] = armyPairIndex - 1;
                    failure.Data["CombatMixedGenerationOrdinal"] = pair.GenerationOrdinal;
                    failure.Data["CombatActorPhysical"] = pair.Actor.PhysicalSlot;
                    failure.Data["CombatActorUnitId"] = pair.Actor.UnitId;
                    failure.Data["CombatActorHex"] = pair.Actor.HexId;
                    failure.Data["CombatTargetPhysical"] = pair.Target.PhysicalSlot;
                    failure.Data["CombatTargetUnitId"] = pair.Target.UnitId;
                    failure.Data["CombatTargetHex"] = pair.Target.HexId;
                    failure.Data["CombatPairOutcome"] = completed.Outcome.ToString();
                    failure.Data["CombatMissingOwner"] = completed.MissingOwner;
                    if (completed.Prefix?.Automatic is { } automatic)
                    {
                        failure.Data["AutomaticOutcome"] = automatic.Outcome.ToString();
                        failure.Data["AutomaticMissingOwner"] = automatic.MissingOwner;
                        if (automatic.Selection is { } reached)
                        {
                            failure.Data["AutomaticStage6Gate"] = reached.Stage5.Stage6Gate.ToString();
                            failure.Data["AutomaticFrameWord20"] = reached.Stage5.Damage.NativeFrameWord20;
                            failure.Data["AutomaticSnapshotTargetTroops"] = reached.Stage5.Pair.Target.TroopsA;
                            if (reached.Selection is { } selection)
                            {
                                failure.Data["AutomaticSelectionStatus"] = selection.Status.ToString();
                                failure.Data["AutomaticSelectedTacticSlot"] = selection.SelectedCandidate?.TacticSlot;
                                if (selection.Candidates is { } candidates)
                                {
                                    failure.Data["AutomaticAvailableSlots"] = string.Join(",", candidates.ActualAvailableSlots);
                                    failure.Data["AutomaticCandidateCount"] = candidates.CandidatesAfterTargetFiltering.Length;
                                }
                            }
                        }
                    }
                    throw failure;
                }
                working = completed.State;
                if (HasNativeCampaignStop(working)) return PublishStoppedCombat();
                if (completed.Outcome == NormalArmyNonlethalPairOutcome.AwaitingPlayerDecision)
                {
                    return RetainSuspendedPair(completed, mixedPairIndex);
                }
            }
            else if (pair.Actor.Kind is 5 or 6 && pair.Target.Kind == 27)
            {
                byte? baseDivisor = baseCounts.TryGetValue((pair.Actor.Kind, pair.Actor.PhysicalSlot), out var count)
                    ? count : (byte)1;
                var appliedBase = CompleteBorrowedBaseArmyPair(working, current, pair,
                    baseDivisor, sourceQueries, sampler, date, revision, effects);
                working = appliedBase.State;
                if (HasNativeCampaignStop(working)) return PublishStoppedCombat();
                if (appliedBase.SuspendedWord20 is ushort word20)
                    return RetainSuspendedMixedPair(mixedPairIndex, word20, appliedBase.AutomaticExecution,
                        appliedBase.BaseAutomaticExecution, appliedBase.PendingAutomaticFatalFeedback);
            }
            else if (pair.Actor.Kind == 27 && pair.Target.Kind is 5 or 6)
            {
                byte? baseDivisor = baseCounts.TryGetValue((pair.Target.Kind, pair.Target.PhysicalSlot), out var count)
                    ? count : (byte)1;
                var appliedBase = CompleteBorrowedArmyBasePair(working, current, pair,
                    baseDivisor, sourceQueries, sampler, date, revision, effects);
                working = appliedBase.State;
                if (HasNativeCampaignStop(working)) return PublishStoppedCombat();
                if (appliedBase.SuspendedWord20 is ushort word20)
                    return RetainSuspendedMixedPair(mixedPairIndex, word20, appliedBase.AutomaticExecution,
                        appliedBase.BaseAutomaticExecution, appliedBase.PendingAutomaticFatalFeedback);
            }
            else if (pair.Target.Kind == 11 && pair.Actor.Kind is 5 or 6 or 27)
            {
                working = CompleteBorrowedObjectCombatPair(working, current, pair, sampler, date, effects);
            }
            else
            {
                string application = pair.Actor.Kind != 27 ? "Base_origin_application" : pair.Target.Kind switch
                {
                    5 or 6 => "Base_target_application",
                    11 => "Object_target_application",
                    _ => "target_kind"
                };
                throw new InvalidDataException($"ordinary_army_combat_cut.{application}.actor{pair.Actor.Kind}:{pair.Actor.PhysicalSlot}.target{pair.Target.Kind}:{pair.Target.PhysicalSlot}");
            }
        }
        if (effects.Count != 0)
            working = working with { CampaignEffectRequests = (working.CampaignEffectRequests ?? CampaignEffectRequests.Empty)
                .Append(CampaignEffectSource.NormalCombatApplication, revision, effects) };
        return working.WithArmyLifecycle(working.ReadArmyLifecycle()! with { Indexes = pool.ProjectOwnedRoles() });

        GameState PublishStoppedCombat()
        {
            working = FlushNormalCombatEffects(working, revision, effects);
            //A terminal callback has no remaining instruction/pair to resume.
            //Its real clock and pending source requests are retained.
            return (working with { NormalCombatImmediateContinuation = null })
                .WithArmyLifecycle(working.ReadArmyLifecycle()! with { Indexes = pool.ProjectOwnedRoles() });
        }

        GameState RetainSuspendedPair(NormalArmyNonlethalPairResult result, int mixedPairIndex)
        {
            ushort word20 = result.PendingOrdinaryFrame20 ?? result.Prefix?.Application?.Receipt?.Request.FrameWord20After
                ?? throw ImmediateCombatMissing("suspended_pair_report");
            return RetainSuspendedMixedPair(mixedPairIndex, word20, result.PendingAutomaticExecution);
        }

        GameState RetainSuspendedMixedPair(int mixedPairIndex, ushort word20,
            AutomaticTacticExecutionContinuation? automaticExecution,
            BaseAutomaticTacticExecutionContinuation? baseAutomaticExecution = null,
            bool pendingAutomaticFatalFeedback = false)
        {
            var retained = retainedCombat is null
                ? CaptureImmediateCombatContinuation(state, projection, descriptors, mixedPairIndex, word20,
                    sampler.Read(working) ?? throw ImmediateCombatMissing("suspended_sampler"))
                : ContinueImmediateCombatSnapshot(retainedCombat, mixedPairIndex, word20);
            working = FlushNormalCombatEffects(working, revision, effects);
            return working with { NormalCombatImmediateContinuation = retained with
                { AutomaticExecution = automaticExecution, BaseAutomaticExecution = baseAutomaticExecution,
                    PendingAutomaticFatalFeedback = pendingAutomaticFatalFeedback } };
        }
    }

    /// <summary>Generate an exact Army-only phase descriptor cache over the
    /// reached stage13 capture. Later pair damage reads fresh current quantities
    /// while retaining this cache; it does not execute the application callbacks.</summary>
    public NormalArmyCombatCurrentPhase? CaptureNormalArmyCombatPhase(GameState state,
        NormalArmyCombatPhaseMoment moment) => CaptureNormalArmyCombatPhase(state, moment, null);

    /// <summary>The actual source driver captured Army state after stage2/8,
    /// before stage13. The saved season intentionally remains at block entry.</summary>
    public NormalArmyCombatCurrentPhase? CaptureNormalArmyCombatPhase(NormalArmyCombatPhaseLocalCapture capture) =>
        NativeArmyCombatCycleSampler.Create(capture) is { } sampler
            ? CaptureNormalArmyCombatPhase(capture.State, capture.Moment, sampler) : null;

    /// <summary>Normal stage provider seam called by the actual cycle driver,
    /// after waiting/movement and before its calendar writer. It constructs the
    /// local capture from the reached stage moment, never a period-end cursor.</summary>
    public NormalArmyCombatCurrentPhase? CaptureNormalArmyCombatPhase(GameState current,
        NativeArmyProgressStageMoment reached)
    {
        if (reached is not { SchedulerStageIndex: 13, CycleWithinAtomicBlock: 2,
                Boundary: NativeArmyProgressStageBoundary.StageEntry, PreWriterThirdDate.Hour: 10 } ||
            current.NativeMapSeason is not { } season) return null;
        return CaptureNormalArmyCombatPhase(new(new(reached.CalendarCycleOrdinal, reached.PreWriterThirdDate, 13,
            season.SourceScenarioUri, season.SourceScenarioSha256, season.SourceWorldSha256,
            NormalArmyCombatCalendarRuntime.Revision),
            current, NativeArmyProgressAtomicRuntime.Revision));
    }

    private NormalArmyCombatCurrentPhase? CaptureNormalArmyCombatPhase(GameState state,
        NormalArmyCombatPhaseMoment moment, NativeArmyCombatCycleSampler? sampler,
        SourceWorldContext? borrowedCurrent = null, NormalArmyCombatPlan? currentPlan = null,
        IReadOnlyDictionary<int, NormalArmyCombatDescriptor>? currentDescriptors = null,
        List<CampaignEffectRequest>? currentEffects = null,
        IReadOnlyDictionary<int, (int Officer, int Slot)>? retainedIdentities = null)
    {
        GameState latest = state;
        Func<ForceReference, int, int?> readEffect = (force, effect) =>
            borrowedCurrent!.ReadForceNativeValidity(force) is bool valid ? !valid ? 0 :
            force.PhysicalSlot is int id && latest.Forces.TryGetValue(id, out var owner)
                ? BattleRemakeCurrentForcePolicies.ReadForceEffect(latest, owner, effect) : null : null;
        Func<ForceReference, int, int?> readModifier = (force, effect) => force.PhysicalSlot is int id
            ? _baseCapacityInputs.ReadPanelModifier(latest, id, effect) : null;
        void BindLatestPolicyReaders()
        {
            borrowedCurrent!.BaseForcePolicyEffectReader = readEffect;
            borrowedCurrent.BaseForceModifierReader = readModifier;
        }
        SourceWorldContext? ReadLatest(GameState reached)
        {
            if (borrowedCurrent is null) return CaptureCurrentDefinitions(reached);
            latest = reached;
            BindLatestPolicyReaders();
            OwnedArmyDestructionCurrent.BindOwnedCurrentContext(reached, borrowedCurrent);
            return borrowedCurrent;
        }
        if (borrowedCurrent is not null)
        {
            _baseCapacityInputs.BindCurrent(state, borrowedCurrent);
            BindLatestPolicyReaders();
        }
        return NormalArmyCombatCurrentPhase.Capture(state, moment,
            definitions => Context(definitions, sampler), ReadLatest, (definitions, descriptors) => {
                var abilities = new TacticRemakeCurrentAbilities(definitions, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
                var prefix = new TacticRemakeCurrentContext(definitions, _catalog, _map, _terrain, _usage, Revision, abilities,
                    operationSampler: sampler);
                var queries = new TacticRemakeCurrentBattleQueries(prefix, definitions, Revision,
                    new TacticRemakeCurrentBattleFactors(prefix, definitions, _regionSeed, Revision), descriptors);
                var attributes = new BattleRemakeCurrentArmyAttributes(_battleSeed, definitions, abilities, queries, Revision);
                return new BattleRemakeCurrentDamage(_battleSeed.DamageSource,
                    new BattleRemakeCurrentArmyDamageQueries(attributes, queries, Revision), Revision);
            }, (context, definitions) => new(_regionSeed, context, definitions, Revision),
            Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language), Revision, sampler is null ? null : this,
            sampler is null ? null : this, borrowedCurrent, currentPlan, currentDescriptors, sampler, currentEffects, retainedIdentities);
    }

    public PlayableArmyCreationResolution? Create(GameState state, PreparedDeployment deployment, UnitState army) =>
        CreateOrdinaryCore(state, deployment, army, null);

    private PlayableArmyCreationResolution? CreateOrdinaryCore(GameState state, PreparedDeployment deployment,
        UnitState army, ArmyCreationConfiguration? normalConfiguration,
        Func<GameState, PlayableArmyCreationResolution, GameState>? beforeRegistration = null,
        ArmyTargetSourceFields? normalTarget = null)
    {
        SourceWorldContext? definitions = CaptureCurrentDefinitions(state);
        if (definitions is null) return null;
        var receipt = normalConfiguration is null ? _creation.Create(state, deployment, army) :
            _creation.Create(state, deployment, army, normalConfiguration,
                id => TacticRemakeCurrentFormations.ReadRawByte(state, id, 0x7E));
        if (receipt is null) return null;
        if (definitions.ReadPersonLookupReference(army.OfficerId)?.PhysicalSlot is not int commanderSlot ||
            definitions.ReadPersonNativeValidity(commanderSlot) != true || receipt.Origin.NativeSlotIndex is not int nativeArmySlot)
            return null;
        var morale = new ArmyMoraleSource(Context(definitions), definitions, Revision);
        BaseReference? selectedBase = morale.ResolveCommanderBase(state, army);
        var result = receipt with { CurrentMorale = morale.ResolveCreationMorale(state, deployment, army),
            CurrentHexId = ArmyConstruction.ResolveCurrentHex2A(definitions, army.OfficerId),
            NativeDirection36 = state.ReadArmyLifecycle()?.Slots[nativeArmySlot].Direction36,
            NativeMapUiByte13C = state.ReadArmyLifecycle()?.Slots[nativeArmySlot].MapUiByte13C,
            NativeCommandControl140 = state.ReadArmyLifecycle()?.Slots[nativeArmySlot].CommandControl140,
            NativeRouteGoal1F8 = state.ReadArmyLifecycle()?.Slots[nativeArmySlot].RouteGoal1F8,
            AiActionFlags = state.ReadArmyLifecycle() is { } aiArena ? aiArena.Slots[nativeArmySlot].AiActionFlags : (ushort)0,
            CommanderPosition = OfficerLocationSourceAdapter.DecodePosition(
                PersonArmyLocationSource.EncodeArmySecondLocation(nativeArmySlot), OfficerLocationSourceAdapter.Receivers(state)),
            NativeHomeSequence28 = state.ReadArmyLifecycle() is null ? null :
                ArmyHomeSequenceSource.ReadNextOrdinary(state, CaptureOwnedArmyReturnWorld(state).Context, army.OfficerId),
            NativeSavedRegionHex152 = selectedBase?.PhysicalSlot is not null
                ? ArmyCurrentRegionSource.ResolveCreationRegion(selectedBase, definitions) : null };
        var march = normalConfiguration is { SourceDeparture: true } sourcePacket && normalTarget is { } sourceGoal
            ? ArmyDeploymentMarchSource.ReadSourceDeparture(sourceGoal, (byte)Math.Clamp(sourcePacket.Action34, 0, 17), result.NativeCommandControl140)
            : ArmyDeploymentMarchSource.Read(deployment, result.NativeCommandControl140, normalTarget);
        // Preserve the draft's independent credit/hold. The wrapper's final
        // next is unknown until its actual route branch has completed.
        result = result with { March = march is null ? null : march.March with { NextStep = null },
            NativeRouteHold56 = result.NativeRouteHold56 ?? march?.RouteHold };
        if (result.CurrentHexId is int confirmedHex && confirmedHex != army.HexId) return null;
        var residues = CampaignSourceKnowledge.From(receipt.ResourceState ?? state.ResourceState);
        var previousLocationResidue = residues.Find(army.OfficerId);
        result = result with { ResourceState = residues.With(new(army.OfficerId,
            result.CommanderPosition?.Kind == OfficerPositionKind.Unresolved
                ? PersonArmyLocationSource.EncodeArmySecondLocation(nativeArmySlot) : null,
            previousLocationResidue?.DistrictCode)) };
        if (state.ReadArmyLifecycle() is null) return result;
        UnitState created = army with { NativeArmyOrigin = result.Origin, Formations = result.Formations, FieldAttackPolicy = result.FieldAttackPolicy, TraitRoster = result.TraitRoster,
            NativeTacticRuntime = result.Runtime, MoraleState = army.MoraleState with { CurrentValue = result.CurrentMorale },
            NativeSavedRegionHex152 = result.NativeSavedRegionHex152, CarriedStores = result.CarriedStores,
            OverrideTarget = result.OverrideTarget,
            March = result.March,
            PositionKnowledge = result.CurrentHexId.HasValue ? ArmyPositionKnowledge.Confirmed : ArmyPositionKnowledge.Unknown,
            NativeDirection36 = result.NativeDirection36,
            NativeMapUiByte13C = result.NativeMapUiByte13C,
            NativeCommandControl140 = result.NativeCommandControl140,
            NativeRouteHold56 = result.NativeRouteHold56, NativeRouteGoal1F8 = result.NativeRouteGoal1F8,
            NativeHomeSequence28 = result.NativeHomeSequence28, AiActionFlags = result.AiActionFlags,
            WoundedTroops = result.WoundedTroops };
        if (beforeRegistration is not null) state = beforeRegistration(state, result);
        ArmyLifecycleCreationAdmission? lifecycle = OwnedArmyLifecycleCurrent.PrepareCreationAdmission(
            state, created, _definitions, march?.Goal.Kind38 ?? _creation.Configuration.TargetKind38,
            march?.Goal.Identity3A, prepareGroups: normalConfiguration is null);
        if (lifecycle is null) return null;
        result = result with { Lifecycle = lifecycle.Lifecycle.Domain, ArmyMapOccupancy = lifecycle.Occupancy,
            ResourceState = CampaignSourceKnowledge.From(result.ResourceState)
                .ReplaceArmyTargets(CampaignSourceKnowledge.From(lifecycle.ResourceState).ArmyTargets)
                .WithArmyLifecycleSource(lifecycle.Lifecycle.Source) };
        // Normal1EC980's final route follows its successful creation, governor
        // tail and book/currency debits; the immediate committer owns that order.
        if (march is null || normalConfiguration is not null) return result;
        // Run the initial route against the privately admitted receiver. Source
        // path preparation owns no credit writer; unsupported inputs keep next
        // unknown and do not publish partial scratch mutations.
        var routeState = (state with { Units = new(state.Units) { [created.Id] = created },
            ResourceState = result.ResourceState, ArmyLifecycle = result.Lifecycle,
            ArmyMapOccupancy = result.ArmyMapOccupancy });
        try
        {
            var routeCurrent = CaptureWritableTransportRouteCurrent(routeState);
            var route = OrdinaryArmyRouteSource.PrepareCreation(routeState, routeCurrent, created);
            if (route.Receipt is not { } initialRoute) return normalConfiguration is null ? result : null;
            var routeArena = routeState.ReadArmyLifecycle()! with { Indexes = routeCurrent.ReadOwnedCurrentListPool()!.ExportOwnedState() };
            var routed = routeState.WithArmyLifecycle(routeArena);
            return result with { March = result.March! with { NextStep = initialRoute.NextHexWrite is ushort next
                    ? ArmyMarchSourceAdapter.DecodeDestination(next) : march.March.NextStep },
                Lifecycle = routed.ArmyLifecycle, ResourceState = routed.ResourceState };
        }
        catch (InvalidDataException) { return normalConfiguration is null ? result : null; }
    }
    public PlayableTacticDefinition? Resolve(GameState state, int tacticId) => null; //Native usage requires a caster.
    public PlayableTacticDefinition? ResolveForCaster(GameState state, UnitState caster, int tacticId) =>
        Capture(state)?.CreateRules().ResolveForCaster(state, caster, tacticId);
    public TacticUseEligibility CanUse(GameState state, UnitState caster, PlayableTacticDefinition definition) =>
        Capture(state)?.CreateRules().CanUse(state, caster, definition) ?? Unknown();
    public int[] ResolveTargetIds(GameState state, UnitState caster, UnitState primaryTarget,
        PlayableTacticDefinition definition, PlayableLandRoutes routes) =>
        (Capture(state) ?? throw new InvalidDataException("Owned campaign tactic context is unresolved."))
            .CreateRules().ResolveTargetIds(state, caster, primaryTarget, definition, routes);
    public TacticTargetResolution ResolveEffects(GameState state, UnitState caster, UnitState target,
        PlayableTacticDefinition definition) =>
        (Capture(state) ?? throw new InvalidDataException("Owned campaign tactic context is unresolved."))
            .CreateRules().ResolveEffects(state, caster, target, definition);
    public bool UsesSourceEffectOrder => true;
    public TacticUseEligibility CanUseTarget(GameState state, UnitState caster, UnitState primaryTarget,
        PlayableTacticDefinition definition, PlayableLandRoutes routes) =>
        Capture(state)?.CreateRules().CanUseTarget(state, caster, primaryTarget, definition, routes) ?? Unknown();
    public TacticUseSourceResolution? ResolveUse(GameState state, UnitState caster, UnitState primaryTarget,
        PlayableTacticDefinition definition, PlayableLandRoutes routes) =>
        Capture(state)?.CreateRules().ResolveUse(state, caster, primaryTarget, definition, routes);
    public NativeSubdayUnitResolution? AdvanceUnit(GameState state, UnitState unit, NativeSubdayMoment moment) =>
        _ticks.AdvanceUnit(state, unit, moment);
    public bool? ResolveLogisticsConnection(GameState state, UnitState army)
    {
        SourceWorldContext? definitions = CaptureCurrentDefinitions(state);
        return definitions is null ? null : new ArmyLogisticsPreparation(Context(definitions), definitions, _map, _terrain)
            .ResolveLogisticsConnection(state, army);
    }
    public bool? PreservesTraitRoster(GameState state, UnitState army)
    {
        SourceWorldContext? definitions = CaptureCurrentDefinitions(state);
        return definitions is null ? null : new ArmyLogisticsPreparation(Context(definitions), definitions, _map, _terrain)
            .PreservesTraitRoster(state, army);
    }
    public ArmyMoralePreparationResolution? ResolveMoraleAfterLogistics(GameState state,
        UnitState army, bool connected)
    {
        SourceWorldContext? definitions = CaptureCurrentDefinitions(state);
        return definitions is null ? null : new(new ArmyMoraleSource(Context(definitions), definitions, Revision)
            .ResolveLogisticsMorale(state, army, connected));
    }
    public byte? ResolveFlags150AfterLogistics(GameState state, UnitState army)
    {
        SourceWorldContext? definitions = CaptureCurrentDefinitions(state);
        return definitions is null ? null : new ArmyLogisticsPreparation(Context(definitions), definitions, _map, _terrain)
            .ResolveFlags150AfterLogistics(state, army);
    }
    public ICampaignResourceState? ResolveResourceKnowledgeAfterLogistics(GameState state, UnitState army,
        ICampaignResourceState? stagedKnowledge, bool traitRosterPreserved) => ArmyLogisticsPreparation.UpdateResourceKnowledge(
            army, stagedKnowledge, traitRosterPreserved, () => ResolveFlags150AfterLogistics(state, army));
    public NativeArmyRegionPreparationResolution? ResolveSavedRegionAfterPreparation(GameState state, UnitState army)
    {
        SourceWorldContext? definitions = CaptureCurrentDefinitions(state);
        if (definitions is null) return null;
        bool? valid = Context(definitions).ArmyValid(state, army);
        ushort? hex = army.HexId is >= 0 and < 48400 &&
            definitions.ReadHexTableReference(army.HexId)?.PhysicalSlot == army.HexId ? (ushort)army.HexId : null;
        ArmySavedRegionWrite? receipt = ArmyCurrentRegionSource.ResolveReachedSnapshot(
            army.NativeSavedRegionHex152, valid, army.ForceId, hex);
        return receipt is null ? null : new(receipt.Word152);
    }
    public bool ProvidesGlobalRegionPreparation => true;
    public AreaStatisticsUpdate? ResolveGlobalRegionPreparation(GameState state)
    {
        return state.RegionStatistics is { } statistics
            ? new(AreaStatisticsRules.RefreshOwnership(statistics, state.Territory)) : null;
    }
    private TacticRemakeCurrentContext? Capture(GameState state) =>
        CaptureCurrentDefinitions(state) is { } definitions ? Context(definitions) : null;
    private TacticRemakeCurrentContext Context(SourceWorldContext definitions,
        NativeArmyCombatCycleSampler? sampler = null)
    {
        var abilities = new TacticRemakeCurrentAbilities(definitions, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
        var prefix = new TacticRemakeCurrentContext(definitions, _catalog, _map, _terrain, _usage, Revision, abilities,
            operationSampler: sampler);
        var queries = new TacticRemakeCurrentBattleQueries(prefix, definitions, Revision,
            new TacticRemakeCurrentBattleFactors(prefix, definitions, _regionSeed, Revision));
        var attributes = new BattleRemakeCurrentArmyAttributes(_battleSeed, definitions, abilities, queries, Revision);
        var normal = new BattleRemakeCurrentArmyDamageQueries(attributes, queries, Revision);
        var damage = new BattleRemakeCurrentDamage(_battleSeed.DamageSource, normal, Revision);
        return new(definitions, _catalog, _map, _terrain, _usage, Revision, abilities, normalDamage: damage,
            operationSampler: sampler);
    }
    private static TacticUseEligibility Unknown() => new(null, "tactic.current_context_unavailable", "tactic.current_context");
}
