using San14.Core;
using San14.Data;

namespace San14.Client.Gameplay;

/// <summary>
/// Composes a campaign from one selected scenario, map and language. Loading and
/// starting build complete sessions before the caller replaces its live campaign.
/// </summary>
public sealed partial class CampaignComposition
{
    private readonly GameResourceRepository _repository;
    private readonly ISaveCodec _saveCodec;
    private readonly ApplicationGameplayState _applicationGameplay;
    private ArmyCampaignRuleAdapter? _army;
    private NarrationVoiceSettings? _personnelNarrationVoiceSettings;
    private LoadedArmyFlagPaneNames? _flagNames;
    private GameState? _headerWorld;
    private int _headerForce;
    private MapHeaderDisplayState? _headerDisplayed;
    private MapHeaderProjection? _currentHeader;

    private CampaignComposition(GameResourceRepository repository, LoadedScenario scenario, LoadedMap map)
    {
        _repository = repository;
        _saveCodec = new ResourceSaveCodec(repository);
        _applicationGameplay = ApplicationGameplayState.CreateNormalCampaign();
        Scenario = scenario;
        Map = map;
        Overview = new MapOverviewContent(map.Map);
        Construction = CampaignConstructionCatalog.Load(repository, scenario);
    }

    public LoadedScenario Scenario { get; }
    public LoadedMap Map { get; }
    public ScenarioCityPreview Snapshot => Scenario.Snapshot;
    public ArmyCampaignRuleAdapter? Army => _army;
    public ApplicationGameplayState ApplicationGameplay => _applicationGameplay;
    internal CampaignConstructionCatalog Construction { get; }
    internal MapOverviewContent Overview { get; }

    public static CampaignLoadResult Load(GameResourceRepository repository, EffectiveScenario selection,
        NewGameOptions options, GameState? restored = null, int? restoredPlayerForceId = null,
        CampaignProgressOperation? restoredProgress = null, long lastStorySequence = 0, ManualTacticProgressOperation? restoredManualTactic = null,
        NormalPeriodOperation? restoredNormalPeriod = null)
    {
        LoadedScenario scenario = repository.LoadScenario(selection);
        LoadedMap map = repository.LoadMapForScenario(selection);
        var composition = new CampaignComposition(repository, scenario, map);
        ScenarioTerritory territory = ScenarioTerritory.Build(map.Map, scenario.Snapshot);
        GameState initial = ScenarioInitializer.Create(scenario, map, 1, options).State;
        if (restored is null)
            initial = composition.PrepareArmyQueries(initial).CreateFreshScenarioMapObjects(initial);
        if (restoredPlayerForceId.HasValue && restored is null)
            throw new InvalidDataException("Restoring a local player requires the saved campaign world.");
        if (restored is not null) composition.ValidateRestoredContent(restored, initial);
        if ((restoredProgress is not null || restoredManualTactic is not null || restoredNormalPeriod is not null) && (!restoredPlayerForceId.HasValue || restored is null))
            throw new InvalidDataException("A pending operation requires its actual restored player session.");
        GameState world = restored ?? initial;
        if (restoredPlayerForceId is int player)
        {
            PreparedRules prepared = composition.PrepareRules(world, player);
            if (prepared.Army is { } army)
                world = RequireFirstStrategy(army.TryInitializeFirstStrategy(world,
                    new(CampaignEntryKind.RestoreCampaign, null, null, null)));
            GameplaySession session = GameplaySession.Restore(world, player,
                prepared.Rules, Calendar(prepared.Army), composition._saveCodec, restoredProgress, lastStorySequence, restoredManualTactic,
                restoredNormalPeriod);
            composition._army = prepared.Army;
            return new(composition, territory, session, prepared.Presentation);
        }
        return new(composition, territory,
            GameplaySession.Preview(world, PlayableSkeleton.Bind(new HexTravelMap(map)), composition._saveCodec),
            CampaignPresentation.Preview);
    }

    public CampaignStartResult Start(GameplaySession preview, int playerForceId,
        CampaignAppRandomState? appRandomSnapshot = null, CampaignCrtRandomState? crtRandomSnapshot = null)
    {
        ValidateSelection(preview.World);
        int rulerId = preview.World.Forces[playerForceId].RulerId ??
            throw new InvalidDataException("The confirmed faction has no ruler.");
        GameState started = PlayableSkeleton.Start(preview.World);
        ArmyCampaignRuleAdapter army = _army ?? CreateArmy(started);
        GameState world = army.CreateFreshOwnedSimulation(started);
        world = army.InstallFreshTalentRules(world);
        world = army.InstallFreshDiplomacyRules(world);
        world = TransportSetupCurrentInputs.InstallFresh(world);
        // Only an explicitly captured host value is installed. No bootstrap or Load repair.
        world = world with { CampaignAppRandom32 = appRandomSnapshot, CampaignCrtRandom = crtRandomSnapshot };
        world = army.CreateFreshDuelService(world);
        world = army.ConfirmNewGamePlayerPerson(world, rulerId);
        world = MapSeasonRuntime.CreateForFreshCampaign(_repository, world,
            MapSeasonNativeSource.Load(_repository.SourceDirectory, Snapshot.Locale));
        if (Snapshot.Locale == "TC")
        {
            var merchants = army.InitializeFreshCityMerchants(world);
            world = merchants.Available ? merchants.Value! : throw new InvalidDataException(
                "The campaign's merchant initialization is unavailable: " + merchants.UnresolvedInput);
            world = army.InitializeFreshPersonnelHire(world);
        }
        // This standalone host has neither the optional selected-user App
        // service nor an external mode. The saved world's user index remains
        // independent; it is not filled from either of these absent services.
        world = RequireFirstStrategy(army.TryInitializeFirstStrategy(world,
            new(CampaignEntryKind.NewCampaign, new(false, null), false, null)));
        PreparedRules prepared = PrepareRules(world, playerForceId, army);
        GameplaySession session = GameplaySession.Restore(world, playerForceId,
            prepared.Rules, Calendar(prepared.Army), _saveCodec);
        // No cache or UI publication occurs in rule construction. Even the
        // session's ownership/initial-state check must succeed before this cache changes.
        _army = prepared.Army;
        return new(session, prepared.Presentation);
    }

    public ArmyCampaignRuleAdapter PrepareArmyQueries(GameState world) => _army ??= CreateArmy(world);

    /// <summary>The application owns voice switches. Rule preparation borrows
    /// this explicit snapshot; loading a world does not invent those switches.</summary>
    public void ConfigurePersonnelNarrationVoice(NarrationVoiceSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        _personnelNarrationVoiceSettings = settings;
        _army?.ConfigurePersonnelNarrationVoice(settings);
    }

    private static GameState RequireFirstStrategy(FirstStrategyInitialization result) => result.State ??
        throw new InvalidDataException("The campaign's initial strategy setup is unavailable: " + result.UnresolvedOwner);

    internal IReadOnlyDictionary<int, DetailForceAppearance?> DetailForces(GameState world, IEnumerable<int> forces)
    {
        _flagNames ??= ArmyFlagPaneNames.Load(Scenario);
        return PrepareArmyQueries(world).ReadDetailForces(world, _flagNames, forces);
    }

    internal DetailForceAppearance? CityDetailForce(GameState world, int cityId)
    {
        _flagNames ??= ArmyFlagPaneNames.Load(Scenario);
        return PrepareArmyQueries(world).ReadCityDetailForce(world, _flagNames, cityId);
    }

    internal DetailForceNameplate ForceDetailNameplate(GameState world, int forceId)
    {
        _flagNames ??= ArmyFlagPaneNames.Load(Scenario);
        return PrepareArmyQueries(world).ReadForceDetailNameplate(world, _flagNames, forceId);
    }

    internal MapHeaderProjection CurrentMapHeader(GameState world, int forceId, MapHeaderDisplayState? displayed)
    {
        if (ReferenceEquals(_headerWorld, world) && _headerForce == forceId && _headerDisplayed == displayed && _currentHeader is not null) return _currentHeader;
        _flagNames ??= ArmyFlagPaneNames.Load(Scenario);
        // This host currently controls the player's first district. An eventual
        // district selector can supply another receiver to the same query.
        var districts = world.Districts.Values.Where(d => d.ForceId == forceId && d.Number == 1).Take(2).ToArray();
        var header = PrepareArmyQueries(world).QueryMapHeader(world,
            districts.Length == 1 ? districts[0].Id : null, displayed, _flagNames);
        _headerWorld = world; _headerForce = forceId; _headerDisplayed = displayed;
        return _currentHeader = header;
    }

    private void ValidateRestoredContent(GameState restored, GameState initial)
    {
        if (restored.NativeArmySimulation?.Parameters is { } parameters)
        {
            string locale = Snapshot.Locale;
            string ruleVersion = SimulationRuleIdentity.ForLocale(locale);
            if (parameters.Language != locale || parameters.RuleVersion != ruleVersion)
                throw new InvalidDataException("Saved campaign rules are incompatible with this implementation.");
        }
        var expected = new Dictionary<string, string>(initial.ContentHashes);
        if (restored.Officers.Values.Any(person => person.Growth is not null))
        {
            // The campaign content ledger owns resource identity; loading
            // never installs XP or replaces ordered growth notifications.
            SourceProvenance source = _repository.LoadExperienceData(ExperienceVariant.ExCe).Source;
            if (restored.ContentHashes.GetValueOrDefault(source.ResourceUri) != source.Sha256)
                throw new InvalidDataException("Current experience content or overrides differ from the saved growth revision.");
            expected[source.ResourceUri] = source.Sha256;
        }
        SourceProvenance events = _repository.LoadEvents(CampaignEventCatalog.SourcePath).Source;
        if (restored.CampaignEvents is not null || restored.CampaignEventWorld is not null ||
            restored.CampaignEventEligibility is not null || restored.ContentHashes.ContainsKey(events.ResourceUri))
        {
            if (restored.ContentHashes.GetValueOrDefault(events.ResourceUri) != events.Sha256)
                throw new InvalidDataException("Current event content or overrides differ from the saved campaign revision.");
            expected[events.ResourceUri] = events.Sha256;
        }
        if (CityDomesticParameters.ValidateRestore(restored, Scenario, _repository) is { } cityDomestic)
            expected[cityDomestic.ResourceUri] = cityDomestic.Sha256;
        if (restored.ContentHashes.Count != expected.Count ||
            restored.ContentHashes.Any(item => !expected.TryGetValue(item.Key, out string? hash) || hash != item.Value))
            throw new InvalidDataException("Original content or overrides differ from the saved scenario revision.");
    }

    private void ValidateSelection(GameState world)
    {
        if (world.ScenarioKey != Scenario.ScenarioSource.ResourceUri ||
            world.MapSha256 != Map.Source.Sha256 ||
            world.ContentHashes.GetValueOrDefault(Scenario.ScenarioSource.ResourceUri) != Scenario.ScenarioSource.Sha256 ||
            world.ContentHashes.GetValueOrDefault(Scenario.FixedDataSource.ResourceUri) != Scenario.FixedDataSource.Sha256)
            throw new InvalidDataException("Owned Army campaign differs from the selected scenario revision.");
    }

    private ArmyCampaignRuleAdapter CreateArmy(GameState world)
    {
        ValidateSelection(world);
        string revision = $"owned-campaign-army-v1:{world.ScenarioKey}:{Scenario.ScenarioSource.Sha256}:{world.MapSha256}:{Scenario.FixedDataSource.Sha256}";
        var army = ArmyCampaignRuleAdapter.FromOwnedCampaign(_repository,
            Scenario, Map, Snapshot.Locale, revision);
        army.ConfigureApplicationGameplay(_applicationGameplay);
        if (_personnelNarrationVoiceSettings is { } voice) army.ConfigurePersonnelNarrationVoice(voice);
        return army;
    }

    private sealed record PreparedRules(PlayableRuleSet Rules, ArmyCampaignRuleAdapter? Army,
        CampaignPresentation Presentation);

    private PreparedRules PrepareRules(GameState world, int playerForceId,
        ArmyCampaignRuleAdapter? existingArmy = null)
    {
        CeDifficultyIncomeRates? rates = Snapshot.WorldParameters is null ? null :
            CeDifficultyIncomeRates.LoadCurrent(_repository.SourceDirectory,
                Snapshot.Locale, world.DifficultyId, new HashSet<int> { playerForceId });
        IPlayableMonthlyIncomeRules? income = rates is null ? null : new ProvisionalAreaMonthlyIncomeRules(rates);
        CampaignIncomePresentation? incomePresentation = rates is null ? null :
            new(rates.RuleVersion, rates.ForForce(playerForceId).GoldPercent,
                rates.ForForce(playerForceId).SuppliesPercent);
        var travel = new HexTravelMap(Map);
        if (world.NativeClock is null || world.NativeArmySimulation is null)
        {
            // Continuing a legacy session must not invent native Army records or an epoch.
            return new(PlayableSkeleton.Bind(travel, income), null,
                new(null, null, incomePresentation));
        }
        ArmyCampaignRuleAdapter army = existingArmy ?? CreateArmy(world);
        PlayableRuleSet rules = PlayableSkeleton.Bind(travel, income, tacticRules: army.Tactics,
            diplomacy: army.CreateDiplomacyRules(),
            talent: army.CreateTalentResolutionRules(),
            personnelRules: army.CreatePersonnelCommandRules(),
            armyCreationRules: army, nativeArmyPreparationRules: army,
            cityEntryResources: new OwnedArmyCityReturnResourceStage(army),
            cityEntryState: army.CreateCityEntryStateRules(),
            siegeSettlementRules: new PlayableCitySiegeSettlement(army),
            fieldSettlementRules: new PlayableArmyDestructionSettlement(army),
            nativePeriodContinuationRules: new PlayableNativePeriodContinuation(army),
            captureReturningPersonRules: army.CreateCaptureReturningPersonRules(),
            captureArmyHomeRules: army.CreateCaptureArmyHomeRules(),
            lastCityPersonnelRules: army.CreateLastCityPersonnelRules(),
            gateEntryState: army.CreateGateEntryStateRules(),
            baseContactPriorityRules: new BaseContactPrioritySourceAdapter(army),
            armyContactRules: new ArmyContactSourceAdapter(army),
            transportCreationRules: army,
            districtCommandBudgetRules: army,
            falseReportCommandRules: army,
            ordinaryDeploymentCommitRules: army,
            areaAdministrationRules: army,
            armyConstructionRules: army,
            armyWithdrawalRules: army,
            armyMovementCommands: army,
            cityDomesticRules: army,
            unitBehaviorRules: army,
            governanceRosterRules: army,
            cityMerchantRules: army,
            cityOfficerMovementRules: army);
        return new(rules, army, new(SimulationRuleIdentity.Version, army.Revision, incomePresentation));
    }

    private static Func<GameState, GameState> Calendar(ArmyCampaignRuleAdapter? army) =>
        army is null ? state => NormalArmyCombatCalendarRuntime.AdvanceCampaign(state) :
            state => army.AdvancePlayableCalendar(state);
}

public sealed record CampaignLoadResult(CampaignComposition Composition, ScenarioTerritory Territory,
    GameplaySession Session, CampaignPresentation Presentation);

public sealed record CampaignStartResult(GameplaySession Session, CampaignPresentation Presentation);

/// <summary>Display metadata, derived only from a fully assembled campaign.</summary>
public sealed record CampaignPresentation(string? ArmyRuleVersion, string? ArmySourceRevision,
    CampaignIncomePresentation? Income)
{
    public static CampaignPresentation Preview { get; } = new(null, null, null);
}

public sealed record CampaignIncomePresentation(string RuleVersion, int GoldPercent, int SuppliesPercent);
