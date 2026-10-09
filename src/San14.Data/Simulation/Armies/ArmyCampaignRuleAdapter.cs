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
    private readonly LoadedSkillDefinitions _skills;
    private readonly TacticSourceCatalog _catalog;
    private readonly LoadedMap _map;
    private readonly LoadedTerrainTypes _terrain;
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
    private readonly OwnedPersonDestructionRuntimeInputs _personDestructionInputs;
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
        OwnedPersonDestructionRuntimeInputs personDestructionInputs,
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
        string revision)
    {
        if (string.IsNullOrWhiteSpace(revision)) throw new InvalidDataException("Owned Army package revision is missing.");
        _repository = repository;
        _definitions = definitions; _scenario = scenario; _skills = skills; _catalog = catalog; _map = map; _terrain = terrain;
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

    public static ArmyCampaignRuleAdapter FromOwnedCampaign(GameResourceRepository repository,
        LoadedScenario source, LoadedMap map, string language, string revision)
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
            OwnedPersonDestructionRuntimeInputs.Load(repository, source, language),
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
            CityCaptureTerritoryInputs.Load(repository, language), revision);
    }

    public GameState CreateFreshOwnedSimulation(GameState state)
    {
        if (_scenario.Snapshot.WorldParameters is not { } world || world.RawBytes.Length < 100 ||
            Convert.ToHexString(SHA256.HashData(world.RawBytes)) != world.WorldSha256 ||
            state.ContentHashes.GetValueOrDefault(_scenario.ScenarioSource.ResourceUri) != _scenario.ScenarioSource.Sha256)
            throw new InvalidDataException("Fresh owned sampler epoch requires its complete actual scenario World.");
        GameState fresh = _marchPenaltySeed.InstallFresh(_mobilitySeed.InstallFresh(
            _battleSeed.InstallFresh(Seed.CreateFreshOwnedSimulation(state, Revision), Revision)));
        fresh = NormalArmyCombatApplicationSourceMath.InstallFresh(fresh, _normalApplicationSeed, Revision);
        fresh = NormalArmyForceResultSource.InstallFresh(fresh, _forceResultSeed, Revision);
        fresh = NormalArmyForceFreshAllocation.InstallFresh(fresh, _scenario, _forceResultSeed);
        fresh = _normalAftermathInputs.InstallFresh(fresh, _scenario);
        fresh = _destructionSeed.InstallFresh(fresh);
        fresh = _personRelationInputs.InstallFresh(fresh);
        fresh = _personReturnPathSeed.InstallFresh(fresh);
        fresh = _transportRouteInputs.InstallFresh(fresh);
        fresh = _force164Inputs.InstallFresh(fresh);
        fresh = _moraleWaveInputs.InstallFresh(fresh);
        fresh = _rulerCaptureReleaseInputs.InstallFresh(fresh);
        fresh = CaptureBaseThreatInputs.InstallFresh(fresh);
        fresh = PersonCaptureWorldCandidates.InstallFresh(fresh);
        fresh = _baseCaptureResourceInputs.InstallFresh(fresh);
        fresh = _baseCaptureRewardInputs.InstallFresh(fresh);
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
        GameState policies = BattleRemakeCurrentForcePolicies.InstallFresh(formations, _scenario);
        policies = _personAssignmentPrecallInputs.InstallFresh(policies);
        GameState morale = _moraleSeed.InstallFresh(policies, definitions, policies.NativeArmySimulation!.Parameters.Revision);
        GameState control = TacticRemakeCurrentBattleControl.CreateFreshOwnedControl(morale);
        GameState settings = BattleRemakeCurrentSettings.InstallFresh(control, _scenario);
        GameState credit = PersonCreditFreshSource.InstallFresh(settings, _scenario,
            CaptureCurrentDefinitions(settings) ?? throw new InvalidDataException("Fresh credit current Person capture is unresolved."));
        GameState region = _regionSeed.InstallFresh(_personReward.InstallFresh(
            _pendingMerit.InstallFresh(_growthQueries.InstallFresh(credit))), Revision);
        GameState areas = AreaStatisticsSourceAdapter.Initialize(region, _scenario, _map, _terrain);
        GameState lifecycle = InstallFreshArmyLifecycle(OwnedArmyReturnServiceInputs.InstallFresh(
            _governorInputs.InstallFresh(_baseCapacityInputs.InstallFresh(areas))));
        var commandWorld = CaptureOwnedArmyReturnWorld(lifecycle);
        GameState capturedItems = _personCaptureInputs.InstallFresh(lifecycle, commandWorld.Context);
        GameState bases = BaseCaptureCurrentInputs.InstallFresh(ForceUnregisterInputs.InstallFresh(
            PersonReturnCommands.InstallFresh(capturedItems, commandWorld.Context), _scenario), _scenario);
        GameState cityCapture = _cityCaptureTerritoryInputs.InstallFresh(BaseReturnCaptivesInputs.InstallFresh(bases), _scenario);
        var completed = CityCaptureFoodWarningSource.InstallFreshInputs(cityCapture, _repository);
        var initialized = ArmyChangedListsSource.InstallFresh(completed, CaptureOwnedArmyReturnWorld(completed).Context);
        InitializeMapArmyGroupsKnownReset(Enumerable.Range(0, 501));
        return initialized;
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
        if (state.Officers.Values.Any(person => person.NativeAbilityRuntime is null)) return null;
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
    public GameState ConfirmNewGamePlayerPerson(GameState state, int confirmedPersonId) =>
        OwnedCurrentPlayerForce.ConfirmNewGamePlayerPerson(state,
            CaptureCurrentDefinitions(state) ?? throw new InvalidDataException("Confirmed player lacks current Person/District owners."),
            confirmedPersonId);

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
        GameState current = preparedState with
        {
            Cities = new(preparedState.Cities), Gates = new(preparedState.Gates),
            Districts = new(preparedState.Districts), Officers = new(preparedState.Officers),
            Units = new(preparedState.Units)
        };
        long nextId = current.Units.Count == 0 ? 1 : (long)current.Units.Keys.Max() + 1;
        foreach (PreparedDeployment plan in preparedState.PreparedDeployments.Values.OrderBy(plan => plan.Id))
        {
            if (nextId <= int.MaxValue)
            {
                try { nextId = PlayableMilitaryTargetLifecycle.NextPeriodAllocationId(current.Units, (int)nextId,
                    current.NativeArmyLifecycle, current.PendingCombatSettlements, current.ArmyMapOccupancy); }
                catch (OverflowException) { return Unavailable("The current logical Army allocation cursor is exhausted."); }
            }
            //Execution increments its cursor after every insertion as well.
            if (nextId >= int.MaxValue)
                return Unavailable("The current logical Army allocation cursor has no complete next insertion.");
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
            var army = new UnitState((int)nextId, plan.ForceId, plan.OfficerId, hex, hex,
                plan.Troops, 0, morale)
            {
                SourceLocationId = plan.SourceLocationId, FormationId = plan.FormationId,
                TacticIds = plan.TacticIds.ToArray(), WoundedTroops = 0
            };
            PlayableArmyCreationResolution? receipt = Create(current, plan, army);
            if (receipt is null || current.NativeArmyLifecycle is not null && receipt.NativeLifecycle is null)
                return Unavailable($"Current source construction is unavailable for officer {plan.OfficerId}.");
            //Only this detached rehearsal owns these objects. Record real
            //receipt allocation identity so the next preview cannot reuse it.
            current.Units.Add(army.Id, army with
            {
                NativeArmyOrigin = receipt.Origin, NativeArmyConfiguration = receipt.Configuration,
                NativeTacticRuntime = receipt.Runtime, NativeMorale1A = receipt.NativeMorale1A,
                NativeSavedRegionHex152 = receipt.NativeSavedRegionHex152,
                NativeCurrentHex2A = receipt.NativeCurrentHex2A,
                NativeDirection36 = receipt.NativeDirection36,
                NativeMapUiByte13C = receipt.NativeMapUiByte13C,
                NativeCommandControl140 = receipt.NativeCommandControl140,
                NativeRouteHold56 = receipt.NativeRouteHold56, NativeRouteGoal1F8 = receipt.NativeRouteGoal1F8,
                NativeHomeSequence28 = receipt.NativeHomeSequence28,
                NativeReturnResources = receipt.NativeReturnResources, NativeOverrideTarget = receipt.NativeOverrideTarget,
                WoundedTroops = receipt.WoundedTroops
            });
            OfficerState commander = current.Officers[plan.OfficerId];
            current.Officers[plan.OfficerId] = commander with
            {
                Deployed = true,
                NativeLocationRuntime = receipt.CommanderSecondLocation11C is ushort second
                    ? commander.NativeLocationRuntime is { } location
                        ? location with { Second11C = second }
                        : new(receipt.Origin.Revision, second, null, null)
                    : commander.NativeLocationRuntime
            };
            current = current with { RandomState = receipt.RandomState,
                NativeArmyLifecycle = receipt.NativeLifecycle ?? current.NativeArmyLifecycle,
                ArmyMapOccupancy = receipt.ArmyMapOccupancy ?? current.ArmyMapOccupancy };
            nextId++;
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
        NormalArmyCombatPhaseMoment moment, NativeArmyCombatCycleSampler? sampler) =>
        NormalArmyCombatCurrentPhase.Capture(state, moment,
            definitions => Context(definitions, sampler), CaptureCurrentDefinitions, (definitions, descriptors) => {
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
            sampler is null ? null : this);

    public PlayableArmyCreationResolution? Create(GameState state, PreparedDeployment deployment, UnitState army)
    {
        SourceWorldContext? definitions = CaptureCurrentDefinitions(state);
        if (definitions is null || _creation.Create(state, deployment, army) is not { } receipt) return null;
        if (definitions.ReadPersonLookupReference(army.OfficerId)?.PhysicalSlot is not int commanderSlot ||
            definitions.ReadPersonNativeValidity(commanderSlot) != true || receipt.Origin.NativeSlotIndex is not int nativeArmySlot)
            return null;
        var morale = new ArmyMoraleSource(Context(definitions), definitions, Revision);
        BaseReference? selectedBase = morale.ResolveCommanderBase(state, army);
        var result = receipt with { NativeMorale1A = morale.ResolveCreationMorale(state, deployment, army),
            NativeCurrentHex2A = ArmyConstruction.ResolveCurrentHex2A(definitions, army.OfficerId),
            NativeDirection36 = state.NativeArmyLifecycle?.Slots[nativeArmySlot].Direction36,
            NativeMapUiByte13C = state.NativeArmyLifecycle?.Slots[nativeArmySlot].MapUiByte13C,
            NativeCommandControl140 = state.NativeArmyLifecycle?.Slots[nativeArmySlot].CommandControl140,
            NativeRouteGoal1F8 = state.NativeArmyLifecycle?.Slots[nativeArmySlot].RouteGoal1F8,
            CommanderSecondLocation11C = PersonArmyLocationSource.EncodeArmySecondLocation(nativeArmySlot),
            NativeHomeSequence28 = state.NativeArmyLifecycle is null ? null :
                ArmyHomeSequenceSource.ReadNextOrdinary(state, CaptureOwnedArmyReturnWorld(state).Context, army.OfficerId),
            NativeSavedRegionHex152 = selectedBase?.PhysicalSlot is not null
                ? ArmyCurrentRegionSource.ResolveCreationRegion(selectedBase, definitions) : null };
        if (state.NativeArmyLifecycle is null) return result;
        UnitState created = army with { NativeArmyOrigin = result.Origin, NativeArmyConfiguration = result.Configuration,
            NativeTacticRuntime = result.Runtime, NativeMorale1A = result.NativeMorale1A,
            NativeSavedRegionHex152 = result.NativeSavedRegionHex152, NativeReturnResources = result.NativeReturnResources,
            NativeOverrideTarget = result.NativeOverrideTarget,
            NativeCurrentHex2A = result.NativeCurrentHex2A,
            NativeDirection36 = result.NativeDirection36,
            NativeMapUiByte13C = result.NativeMapUiByte13C,
            NativeCommandControl140 = result.NativeCommandControl140,
            NativeRouteHold56 = result.NativeRouteHold56, NativeRouteGoal1F8 = result.NativeRouteGoal1F8,
            NativeHomeSequence28 = result.NativeHomeSequence28,
            WoundedTroops = result.WoundedTroops };
        ArmyLifecycleCreationAdmission? lifecycle = PrepareArmyLifecycleCreation(state, created);
        return lifecycle is null ? null : result with { NativeLifecycle = lifecycle.Lifecycle, ArmyMapOccupancy = lifecycle.Occupancy };
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
    public bool? PreservesTraitSlots64(GameState state, UnitState army)
    {
        SourceWorldContext? definitions = CaptureCurrentDefinitions(state);
        return definitions is null ? null : new ArmyLogisticsPreparation(Context(definitions), definitions, _map, _terrain)
            .PreservesTraitSlots64(state, army);
    }
    public NativeArmyMoralePreparationResolution? ResolveMoraleAfterLogistics(GameState state,
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
    public bool ProvidesFlags150Preparation => true;
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
