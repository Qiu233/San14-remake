using San14.Core;
using San14.Data;

public partial class Main
{
    private LoadedScenario? _loadedScenario;
    private OriginalOwnedArmyCampaignRules? _ownedArmyCampaign;

    private void ValidateRestoredCampaignContent(GameState restored, GameState initial)
    {
        if (_repository is null) throw new InvalidOperationException("Campaign repository is unavailable.");
        if (restored.NativeArmySimulation?.Parameters is { } parameters)
        {
            string locale = _locale.Selected == 1 ? "TC" : "SC";
            string executable = OriginalCeHexWorldCoordinates.VerifyInstalledExecutable(_repository.SourceDirectory, locale);
            if (parameters.Language != locale || parameters.ExecutableSha256 != executable)
                throw new InvalidDataException("Installed Army rules differ from the saved native campaign executable revision.");
        }
        var expected = new Dictionary<string, string>(initial.ContentHashes);
        NativePersonGrowthRuntime[] growth = restored.Officers.Values
            .Where(person => person.NativeGrowthRuntime is not null)
            .Select(person => person.NativeGrowthRuntime!).ToArray();
        if (growth.Length != 0)
        {
            // Verify a saved module's source revision without installing new
            // current XP or replacing its ordered notification vector.
            SourceProvenance source = _repository.LoadExperienceData(OriginalExperienceVariant.ExCe).Source;
            if (growth.Any(owner => owner.SourceExperienceUri != source.ResourceUri ||
                owner.SourceExperienceSha256 != source.Sha256))
                throw new InvalidDataException("Current experience content or overrides differ from the saved growth revision.");
            expected[source.ResourceUri] = source.Sha256;
        }
        if (restored.ContentHashes.Count != expected.Count ||
            restored.ContentHashes.Any(item => !expected.TryGetValue(item.Key, out string? hash) || hash != item.Value))
            throw new InvalidDataException("Original content or overrides differ from the saved scenario revision.");
    }

    private OriginalOwnedArmyCampaignRules PrepareOwnedArmyCampaign(GameState world)
    {
        if (_repository is null || _loadedScenario is null || _loadedMap is null || _selectedSnapshot is null)
            throw new InvalidOperationException("Owned Army campaign source is unavailable.");
        return _ownedArmyCampaign ??= PrepareOwnedArmyCampaign(world, _loadedScenario,
            _loadedMap, _selectedSnapshot);
    }

    private OriginalOwnedArmyCampaignRules PrepareOwnedArmyCampaign(GameState world,
        LoadedScenario scenario, LoadedMap map, ScenarioCityPreview snapshot)
    {
        if (_repository is null) throw new InvalidOperationException("Campaign repository is unavailable.");
        if (world.ScenarioKey != scenario.ScenarioSource.ResourceUri ||
            world.MapSha256 != map.Source.Sha256 ||
            world.ContentHashes.GetValueOrDefault(scenario.ScenarioSource.ResourceUri) != scenario.ScenarioSource.Sha256 ||
            world.ContentHashes.GetValueOrDefault(scenario.FixedDataSource.ResourceUri) != scenario.FixedDataSource.Sha256)
            throw new InvalidDataException("Owned Army campaign differs from the selected scenario revision.");
        string revision = $"owned-campaign-army-v1:{world.ScenarioKey}:{scenario.ScenarioSource.Sha256}:{world.MapSha256}:{scenario.FixedDataSource.Sha256}";
        return OriginalOwnedArmyCampaignRules.FromOwnedCampaign(_repository,
            scenario, map, snapshot.Locale, revision);
    }

    private GameState ConstructFreshOwnedCampaign(GameState started, int confirmedRulerPersonId)
    {
        OriginalOwnedArmyCampaignRules package = PrepareOwnedArmyCampaign(started);
        GameState constructed = package.CreateFreshOwnedSimulation(started);
        constructed = package.ConfirmNewGamePlayerPerson(constructed, confirmedRulerPersonId);
        constructed = OriginalMapSeasonRuntime.CreateForFreshCampaign(_repository!, constructed,
            OriginalMapSeasonNativeSource.Load(_repository!.SourceDirectory, _selectedSnapshot!.Locale));
        SetMeta("playable_owned_army_source_sha256", package.Seed.Source.Sha256);
        SetMeta("playable_owned_army_source_revision", package.Revision);
        return constructed;
    }

    private sealed record PreparedPlayableSessionRules(PlayableRuleSet Rules,
        OriginalOwnedArmyCampaignRules? Army, OriginalCeDifficultyIncomeRates? Income, int PlayerForceId);

    private PreparedPlayableSessionRules PreparePlayableSessionRules(GameState world, int playerForceId,
        LoadedScenario scenario, LoadedMap map, ScenarioCityPreview snapshot,
        OriginalOwnedArmyCampaignRules? existingArmy = null)
    {
        if (_repository is null) throw new InvalidOperationException("Campaign repository is unavailable.");
        OriginalCeDifficultyIncomeRates? rates = snapshot.WorldParameters is null ? null :
            OriginalCeDifficultyIncomeRates.LoadCurrent(_repository.SourceDirectory,
                snapshot.Locale, world.DifficultyId, new HashSet<int> { playerForceId });
        IPlayableMonthlyIncomeRules? income = rates is null ? null : new ProvisionalAreaMonthlyIncomeRules(rates);
        var travel = new OriginalHexTravelMap(map);
        if (world.NativeClock is null || world.NativeArmySimulation is null)
        {
            // A legacy session has no source creation proof. Continuing it does
            // not invent native Army records or replace its saved epoch.
            return new(PlayableSkeleton.Bind(travel, income), null, rates, playerForceId);
        }
        OriginalOwnedArmyCampaignRules package = existingArmy ??
            PrepareOwnedArmyCampaign(world, scenario, map, snapshot);
        return new(PlayableSkeleton.Bind(travel, income, tacticRules: package.Tactics,
            armyCreationRules: package, nativeArmyPreparationRules: package,
            cityEntryResources: new OriginalOwnedArmyCityReturnResourceStage(package),
            cityEntryState: package.CreateCityEntryStateRules(),
            siegeSettlementRules: new OriginalPlayableCitySiegeSettlement(package),
            fieldSettlementRules: new OriginalPlayableArmyDestructionSettlement(package),
            nativePeriodContinuationRules: new OriginalPlayableNativePeriodContinuation(package),
            captureReturningPersonRules: package.CreateCaptureReturningPersonRules(),
            captureArmyHomeRules: package.CreateCaptureArmyHomeRules(),
            lastCityPersonnelRules: package.CreateLastCityPersonnelRules(),
            gateEntryState: package.CreateGateEntryStateRules(),
            baseContactPriorityRules: new OriginalPlayableBaseContactPriority(package),
            armyContactRules: new OriginalPlayableArmyContact(package),
            transportCreationRules: package), package, rates, playerForceId);
    }

    private void PublishPlayableSessionRules(PreparedPlayableSessionRules prepared)
    {
        _ownedArmyCampaign = prepared.Army;
        SetMeta("playable_owned_army_rules", prepared.Army is not null);
        if (prepared.Army is { } army)
        {
            SetMeta("playable_owned_army_source_sha256", army.Seed.Source.Sha256);
            SetMeta("playable_owned_army_source_revision", army.Revision);
        }
        else
        {
            RemoveMeta("playable_owned_army_source_sha256");
            RemoveMeta("playable_owned_army_source_revision");
        }
        if (prepared.Income is { } rates)
        {
            SetMeta("playable_income_difficulty_source_sha256", rates.SourceExecutableSha256);
            SetMeta("playable_income_player_gold_percent", rates.ForForce(prepared.PlayerForceId).GoldPercent);
            SetMeta("playable_income_player_supplies_percent", rates.ForForce(prepared.PlayerForceId).SuppliesPercent);
        }
        else
        {
            RemoveMeta("playable_income_difficulty_source_sha256");
            RemoveMeta("playable_income_player_gold_percent");
            RemoveMeta("playable_income_player_supplies_percent");
        }
    }
}
