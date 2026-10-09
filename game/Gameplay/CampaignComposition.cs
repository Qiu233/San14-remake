using San14.Core;
using San14.Data;

namespace San14.Client.Gameplay;

/// <summary>
/// Composes a campaign from one selected scenario, map and language. Loading and
/// starting build complete sessions before the caller replaces its live campaign.
/// </summary>
public sealed class CampaignComposition
{
    private readonly GameResourceRepository _repository;
    private readonly ISaveCodec _saveCodec;
    private ArmyCampaignRuleAdapter? _army;

    private CampaignComposition(GameResourceRepository repository, LoadedScenario scenario, LoadedMap map)
    {
        _repository = repository;
        _saveCodec = new ResourceSaveCodec(repository);
        Scenario = scenario;
        Map = map;
        Construction = CampaignConstructionCatalog.Load(repository, scenario);
    }

    public LoadedScenario Scenario { get; }
    public LoadedMap Map { get; }
    public ScenarioCityPreview Snapshot => Scenario.Snapshot;
    public ArmyCampaignRuleAdapter? Army => _army;
    internal CampaignConstructionCatalog Construction { get; }

    public static CampaignLoadResult Load(GameResourceRepository repository, EffectiveScenario selection,
        NewGameOptions options, GameState? restored = null, int? restoredPlayerForceId = null)
    {
        LoadedScenario scenario = repository.LoadScenario(selection);
        LoadedMap map = repository.LoadMapForScenario(selection);
        var composition = new CampaignComposition(repository, scenario, map);
        ScenarioTerritory territory = ScenarioTerritory.Build(map.Map, scenario.Snapshot);
        GameState initial = ScenarioInitializer.Create(scenario, map, 1, options).State;
        if (restored is not null) composition.ValidateRestoredContent(restored, initial);
        GameState world = restored ?? initial;
        if (restoredPlayerForceId is int player)
        {
            PreparedRules prepared = composition.PrepareRules(world, player);
            GameplaySession session = GameplaySession.Restore(world, player,
                prepared.Rules, Calendar(prepared.Army), composition._saveCodec);
            composition._army = prepared.Army;
            return new(composition, territory, session, prepared.Presentation);
        }
        return new(composition, territory,
            GameplaySession.Preview(world, PlayableSkeleton.Bind(new HexTravelMap(map)), composition._saveCodec),
            CampaignPresentation.Preview);
    }

    public CampaignStartResult Start(GameplaySession preview, int playerForceId)
    {
        ValidateSelection(preview.World);
        int rulerId = preview.World.Forces[playerForceId].RulerId ??
            throw new InvalidDataException("The confirmed faction has no ruler.");
        GameState started = PlayableSkeleton.Start(preview.World);
        ArmyCampaignRuleAdapter army = _army ?? CreateArmy(started);
        GameState world = army.CreateFreshOwnedSimulation(started);
        world = army.ConfirmNewGamePlayerPerson(world, rulerId);
        world = MapSeasonRuntime.CreateForFreshCampaign(_repository, world,
            MapSeasonNativeSource.Load(_repository.SourceDirectory, Snapshot.Locale));
        PreparedRules prepared = PrepareRules(world, playerForceId, army);
        GameplaySession session = GameplaySession.Restore(world, playerForceId,
            prepared.Rules, Calendar(prepared.Army), _saveCodec);
        // No cache or UI publication occurs in rule construction. Even the
        // session's ownership/initial-state check must succeed before this cache changes.
        _army = prepared.Army;
        return new(session, prepared.Presentation);
    }

    public ArmyCampaignRuleAdapter PrepareArmyQueries(GameState world) => _army ??= CreateArmy(world);

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
        return ArmyCampaignRuleAdapter.FromOwnedCampaign(_repository,
            Scenario, Map, Snapshot.Locale, revision);
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
            transportCreationRules: army);
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
