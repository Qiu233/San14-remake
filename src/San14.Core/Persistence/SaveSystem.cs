using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace San14.Core;

public interface ISaveCodec
{
    string FormatId { get; }
    byte[] Encode(GameState state);
    GameState Decode(ReadOnlySpan<byte> bytes);
}

public sealed record SaveEnvelope(int SchemaVersion, string FormatId, GameState State);

/// <summary>Local save format. A future original-save adapter can implement ISaveCodec.</summary>
public sealed class JsonGzipSaveCodec : ICampaignOperationSaveCodec
{
    public const int CurrentSchemaVersion = 229;
    public string FormatId => "san14-remake-json-gzip";
    private readonly ILegacyOfficerAbilitySource? _legacyAbilitySource;
    private readonly ICampaignResourceStateCodec? _resourceStateCodec;
    private readonly CampaignOperationWorldContext? _operationContext;

    public JsonGzipSaveCodec(ILegacyOfficerAbilitySource? legacyAbilitySource = null,
        ICampaignResourceStateCodec? resourceStateCodec = null, CampaignOperationWorldContext? operationContext = null)
    {
        _legacyAbilitySource = legacyAbilitySource;
        _resourceStateCodec = resourceStateCodec;
        _operationContext = operationContext;
    }

    public byte[] EncodeOperationWorld(GameState state, CampaignOperationWorldContext context) =>
        new JsonGzipSaveCodec(_legacyAbilitySource, _resourceStateCodec, context).Encode(state);
    public GameState DecodeOperationWorld(ReadOnlySpan<byte> bytes, CampaignOperationWorldContext context) =>
        new JsonGzipSaveCodec(_legacyAbilitySource, _resourceStateCodec, context).Decode(bytes);

    public byte[] Encode(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _operationContext?.Validate(state);
        state = ArmyIdentityAllocation.Initialize(state);
        // Serialize the owned world directly. Whole-world correctness checks belong
        // to offline validation; decoding checks data entering the game.
        JsonObject document = JsonSerializer.SerializeToNode(new SaveEnvelope(CurrentSchemaVersion, FormatId, state))!.AsObject();
        ArmyViewRetirementSaveFields.ValidateWorld((JsonObject)document["State"]!, CurrentSchemaVersion);
        PersonnelIntelligenceNarrationSaveFields.ValidateWorld((JsonObject)document["State"]!);
        CampaignStorySelectionSaveFields.ValidateWorld((JsonObject)document["State"]!);
        ArmyGroupObjectiveSaveFields.ValidateWorld((JsonObject)document["State"]!);
        CampaignStoryMessageBankSaveFields.ValidateWorld((JsonObject)document["State"]!);
        OriginalAiStrategyMemorySaveFields.ValidateWorld((JsonObject)document["State"]!);
        OriginalAiPolicyExecutionSaveFields.ValidateWorld((JsonObject)document["State"]!);
        OriginalAiStrategyScheduleSaveFields.ValidateWorld((JsonObject)document["State"]!);
        OriginalAiNativeStateSaveFields.ValidateWorld((JsonObject)document["State"]!);
        ItemNameSortSaveFields.ValidateWorld((JsonObject)document["State"]!);
        DuelPresentationSnapshotSaveFields.ValidateWorld((JsonObject)document["State"]!);
        DuelCaptureAnnouncementSaveFields.ValidateWorld((JsonObject)document["State"]!);
        UnassignedForcePolicyEffectsSaveFields.ValidateWorld((JsonObject)document["State"]!);
        CampaignStoryInvocationPresentationSaveFields.ValidateWorld((JsonObject)document["State"]!);
        CampaignStoryPresentationSaveFields.ValidateWorld((JsonObject)document["State"]!);
        if (state.ResourceState is { } resourceState)
            document["ResourceState"] = (_resourceStateCodec ?? throw new InvalidDataException(
                "This campaign requires its resource-state save codec.")).Encode(resourceState);
        using var output = new MemoryStream();
        using (var zip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
            JsonSerializer.Serialize(zip, document);
        return output.ToArray();
    }

    public GameState Decode(ReadOnlySpan<byte> bytes)
    {
        using var input = new MemoryStream(bytes.ToArray());
        using var zip = new GZipStream(input, CompressionMode.Decompress);
        JsonObject document = JsonNode.Parse(zip) as JsonObject
            ?? throw new InvalidDataException("Save data is empty.");
        int version = document["SchemaVersion"]?.GetValue<int>()
            ?? throw new InvalidDataException("Save schema version is missing.");
        // Historical schemas are migrated once; normal simulation uses the current domain state.
        if (version is < 1 or > CurrentSchemaVersion ||
            document["FormatId"]?.GetValue<string>() != FormatId)
            throw new InvalidDataException("Save format or schema version is unsupported.");
        if (document["State"] is not JsonObject)
            throw new InvalidDataException("Saved game state is missing or invalid.");
        ArmyViewRetirementSaveFields.ValidateWorld((JsonObject)document["State"]!, version);
        ArmyDistrictSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        PersonnelStrategicSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        NativeNormalTurnSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        NormalTurnSceneSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        NormalTurnCoalitionSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        NormalTurnEconomySaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        NormalTurnStorySaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        NormalTurnCompletionSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        CampaignStoryPreparationSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        CampaignStoryPresentationSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        CampaignStoryInvocationPresentationSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        PendingCampaignServicesSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        ApplicationCommandAccountingSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        NormalTurnPresentationSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        PersonnelGroupsSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        TutorialForceDefeatSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        BaseCapturePresentationSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        ArmyTraitFeedbackSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        PersonDeathPresentationSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        RulerDecisionSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        NormalCombatImmediateSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        DuelAftermathSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        DuelPresentationSnapshotSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        DuelCaptureAnnouncementSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        BaseCaptureContinuationSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        ArmyReturnCaptiveSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        NativeArmyMovementSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        AutomaticEmperorCaptureSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        BaseTacticPresentationSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        BaseCombatSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        OrdinaryTacticPlaybackSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        ManualTacticExecutionSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        OfficerSpecialVoiceSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        NativePeriodStopSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        CampaignEndingSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        TutorialLessonSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        ProposalSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        ProposalPresentationSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        ArmyDeparturePresentationSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        PersonnelCaravanSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        PersonnelIntelligenceSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        SceneryInvestmentCommandSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        FacilityArmySpeechSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        FacilityEffectsPresentationSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        FacilityConstructionConnectionSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        FirePresentationSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        ArmyCommandSpeechSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        PlaceholderAreaDevelopmentSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        UnassignedForcePolicyEffectsSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        ItemAppearanceSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        CreatedPersonalitySaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        EditedEventPlaybackSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        CampaignEventCommandSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        CampaignStorySelectionSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        ArmyGroupObjectiveSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        OrdinaryArmyCreationSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        CampaignStoryMessageBankSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        OriginalAiStrategyMemorySaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        OriginalAiPolicyExecutionSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        OriginalAiStrategyScheduleSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        OriginalAiNativeStateSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        ItemNameSortSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        CoalitionCameraPresentationSaveFields.RejectLegacyFields((JsonObject)document["State"]!, version);
        ICampaignResourceState? resourceState = null;
        if (document.ContainsKey("ResourceState"))
        {
            if (document["ResourceState"] is not JsonObject resourceDocument)
                throw new InvalidDataException("Saved resource compatibility state is invalid.");
            resourceState = (_resourceStateCodec ?? throw new InvalidDataException(
                "Saved resource compatibility knowledge requires its resource-state codec.")).Decode(resourceDocument);
        }
        // Schema142 adds typed duel pair history, prepared result and aftermath owner.
        // Old KnownEmpty history becomes an actual empty list. Unknown stays unknown;
        // old Active gates do not invent a missing active contest or numeric result.
        // Schema141 adds current Turn City/Gate gold and food expenditures.
        // Older saves preserve null until the real Turn reset writes zero.
        // Schema59 replaces EXE proof hashes with the version of our own rules.
        // Migrate identity metadata only; all current field values and owner links remain untouched.
        if (version < 59) MigrateRuleIdentities((JsonObject)document["State"]!);
        // Schema60 names saved parameters by their meaning. This is the only
        // boundary that recognizes historical source-address keys; values stay unchanged.
        if (version < 60) LegacySimulationParameterSaveMigration.Apply((JsonObject)document["State"]!);
        if (version < 61) LegacyAreaStatisticsSaveMigration.Apply((JsonObject)document["State"]!);
        if (version < 62)
        {
            LegacyCombatSettlementSaveMigration.Apply((JsonObject)document["State"]!);
            LegacyPersonnelCommandTasksMigration.Apply((JsonObject)document["State"]!);
        }
        if (version < 115) PersonnelCommandTasksSaveMigration.Apply((JsonObject)document["State"]!);
        if (version < 63) LegacyWorldIndexesSaveMigration.Apply((JsonObject)document["State"]!);
        if (version < 64) LegacyArmyGroupsSaveMigration.Apply((JsonObject)document["State"]!);
        if (version < 65) LegacyArmyCautionsSaveMigration.Apply((JsonObject)document["State"]!);
        if (version < 66) LegacyArmyMapOccupancySaveMigration.Apply((JsonObject)document["State"]!);
        if (version < 67) LegacyOfficeBonusEncodingSaveMigration.Apply((JsonObject)document["State"]!);
        if (version < 68) LegacyForceCombatObjectiveSaveMigration.Apply((JsonObject)document["State"]!, version);
        if (version < 69) LegacyForcePolicyEffectsSaveMigration.Apply((JsonObject)document["State"]!);
        if (version < 70) LegacyArmyStoresSaveMigration.Apply((JsonObject)document["State"]!);
        if (version < 71) LegacyOfficerTraitActivationSaveMigration.Apply((JsonObject)document["State"]!);
        if (version < 72) LegacyOfficerHealthSaveMigration.Apply((JsonObject)document["State"]!);
        if (version < 73) LegacyOfficerAppointmentSaveMigration.Apply((JsonObject)document["State"]!);
        if (version < 74) LegacyOfficerServiceStatusSaveMigration.Apply((JsonObject)document["State"]!);
        if (version < 75) LegacyOfficerBehaviorSaveMigration.Apply((JsonObject)document["State"]!);
        if (version < 76) LegacyOfficerMeritSaveMigration.Apply((JsonObject)document["State"]!);
        if (version < 77) LegacyOfficerGrowthSaveMigration.Apply((JsonObject)document["State"]!);
        if (version < 78) LegacyUnusedOfficerFreedomSaveMigration.Apply((JsonObject)document["State"]!);
        if (version < 79) LegacyCommanderSelectionSaveMigration.Apply((JsonObject)document["State"]!);
        if (version < 80) LegacyOfficerAbilitySaveMigration.Apply((JsonObject)document["State"]!, _legacyAbilitySource);
        if (version < 81)
        {
            var locations = LegacyOfficerLocationSaveMigration.Apply((JsonObject)document["State"]!);
            if (locations.Count != 0)
                resourceState = (_resourceStateCodec ?? throw new InvalidDataException(
                    "Historical unresolved officer locations require the resource-state codec.")).MigrateLocations(resourceState, locations);
        }
        if (version < 82)
        {
            var aftermath = LegacyOfficerAftermathSaveMigration.Apply((JsonObject)document["State"]!);
            if (aftermath.Count != 0)
                resourceState = (_resourceStateCodec ?? throw new InvalidDataException(
                    "Historical officer aftermath encodings require the resource-state codec.")).MigrateAftermath(resourceState, aftermath);
        }
        if (version < 83)
        {
            var targets = LegacyArmyTargetsSaveMigration.Apply((JsonObject)document["State"]!);
            if (targets.Count != 0)
                resourceState = (_resourceStateCodec ?? throw new InvalidDataException(
                    "Historical unresolved army targets require the resource-state codec.")).MigrateArmyTargets(resourceState, targets);
        }
        if (version < 84)
        {
            var overrides = LegacyArmyOverrideSaveMigration.Apply((JsonObject)document["State"]!);
            if (overrides.Count != 0)
                resourceState = (_resourceStateCodec ?? throw new InvalidDataException(
                    "Historical unresolved army overrides require the resource-state codec.")).MigrateArmyOverrides(resourceState, overrides);
        }
        if (version < 85)
        {
            var positions = LegacyArmyMarchSaveMigration.Apply((JsonObject)document["State"]!);
            if (positions.Count != 0)
                resourceState = (_resourceStateCodec ?? throw new InvalidDataException(
                    "Historical conflicting army positions require the resource-state codec.")).MigrateArmyPositions(resourceState, positions);
        }
        if (version < 87) LegacyArmyReceiverRosterSaveMigration.Apply((JsonObject)document["State"]!);
        if (version < 88 && LegacyArmyLifecycleSaveMigration.Apply((JsonObject)document["State"]!) is { } armySource)
            resourceState = (_resourceStateCodec ?? throw new InvalidDataException(
                "Historical army source knowledge requires its resource-state codec.")).MigrateArmyLifecycle(resourceState, armySource);
        JsonObject stateDocument = (JsonObject)document["State"]!;
        OfficerSpecialCapacitySaveMigration.Apply(stateDocument, version);
        ArmyOverrideObjectSaveMigration.Apply(stateDocument, version);
        TalentSaveFields.Migrate(stateDocument, version);
        TalentDiscoverySaveFields.Migrate(stateDocument, version);
        CampaignExpeditionSaveFields.Migrate(stateDocument, version);
        ForceCreditReserveSaveFields.Migrate(stateDocument, version);
        PlaceholderAreaDevelopmentSaveFields.Migrate(stateDocument, version);
        UnassignedForcePolicyEffectsSaveFields.Migrate(stateDocument, version);
        ItemAppearanceSaveFields.Migrate(stateDocument, version);
        CreatedPersonalitySaveFields.Migrate(stateDocument, version);
        CampaignStorySelectionSaveFields.Migrate(stateDocument, version);
        ArmyGroupObjectiveSaveFields.Migrate(stateDocument, version);
        CampaignStoryMessageBankSaveFields.Migrate(stateDocument, version);
        OriginalAiStrategyMemorySaveFields.Migrate(stateDocument, version);
        OriginalAiPolicyExecutionSaveFields.Migrate(stateDocument, version);
        OriginalAiStrategyScheduleSaveFields.Migrate(stateDocument, version);
        OriginalAiNativeStateSaveFields.Migrate(stateDocument, version);
        ItemNameSortSaveFields.Migrate(stateDocument, version);
        CampaignStoryInvocationPresentationSaveFields.Migrate(stateDocument, version);
        EditedEventPlaybackSaveFields.Migrate(stateDocument, version);
        CampaignEventCommandSaveFields.Migrate(stateDocument, version);
        PersonnelHireSaveFields.Migrate(stateDocument, version);
        CampaignCrtRandomSaveFields.Migrate(stateDocument, version);
        MonthlyMaintenanceSaveFields.Migrate(stateDocument, version);
        DistrictSettingsSaveFields.Migrate(stateDocument, version);
        ArmyDistrictSaveFields.Migrate(stateDocument, version);
        ArmyMapSpeechSaveFields.Migrate(stateDocument, version);
        DoctrineResearchSaveFields.Migrate(stateDocument, version);
        CampaignTurnStopSaveFields.Migrate(stateDocument, version);
        OfficerPresentationSaveFields.Migrate(stateDocument, version);
        PersonnelCreditEpisodeSaveFields.Migrate(stateDocument, version);
        ForeignRelationsSaveFields.Migrate(stateDocument, version);
        CoalitionCooldownSaveFields.Migrate(stateDocument, version);
        ForceFactionSaveFields.Migrate(stateDocument, version);
        PersonnelStrategicSaveFields.Migrate(stateDocument, version);
        ForceCoalitionVisualSaveFields.Migrate(stateDocument, version);
        ForceIndependenceSceneSaveFields.Migrate(stateDocument, version);
        NativeNormalTurnSaveFields.Migrate(stateDocument, version);
        NormalTurnSceneSaveFields.Migrate(stateDocument, version);
        NormalTurnCoalitionSaveFields.Migrate(stateDocument, version);
        NormalTurnEconomySaveFields.Migrate(stateDocument, version);
        NormalTurnStorySaveFields.Migrate(stateDocument, version);
        NormalTurnCompletionSaveFields.Migrate(stateDocument, version);
        CampaignStoryPresentationSaveFields.Migrate(stateDocument, version);
        CampaignStoryPreparationSaveFields.Migrate(stateDocument, version);
        PendingCampaignServicesSaveFields.Migrate(stateDocument, version);
        ApplicationCommandAccountingSaveFields.Migrate(stateDocument, version);
        NormalTurnPresentationSaveFields.Migrate(stateDocument, version);
        PersonnelGroupsSaveFields.Migrate(stateDocument, version);
        TutorialForceDefeatSaveFields.Migrate(stateDocument, version);
        BaseCapturePresentationSaveFields.Migrate(stateDocument, version);
        ArmyTraitFeedbackSaveFields.Migrate(stateDocument, version);
        PersonDeathPresentationSaveFields.Migrate(stateDocument, version);
        RulerDecisionSaveFields.Migrate(stateDocument, version);
        BaseCombatSaveFields.Migrate(stateDocument, version);
        OrdinaryTacticPlaybackSaveFields.Migrate(stateDocument, version);
        ManualTacticExecutionSaveFields.Migrate(stateDocument, version);
        OfficerSpecialVoiceSaveFields.Migrate(stateDocument, version);
        NativePeriodStopSaveFields.Migrate(stateDocument, version);
        CampaignEndingSaveFields.Migrate(stateDocument, version);
        TutorialLessonSaveFields.Migrate(stateDocument, version);
        ProposalSaveFields.Migrate(stateDocument, version);
        ProposalPresentationSaveFields.Migrate(stateDocument, version);
        ArmyDeparturePresentationSaveFields.Migrate(stateDocument, version);
        PersonnelCaravanSaveFields.Migrate(stateDocument, version);
        PersonnelIntelligenceSaveFields.Migrate(stateDocument, version);
        FacilityArmySpeechSaveFields.Migrate(stateDocument, version);
        FacilityEffectsPresentationSaveFields.Migrate(stateDocument, version);
        FacilityConstructionConnectionSaveFields.Migrate(stateDocument, version);
        NormalCombatImmediateSaveFields.Migrate(stateDocument, version, _operationContext);
        DuelAftermathSaveFields.Migrate(stateDocument, version, _operationContext);
        BaseCaptureContinuationSaveFields.Migrate(stateDocument, version, _operationContext);
        ArmyReturnCaptiveSaveFields.Migrate(stateDocument, version);
        NativeArmyMovementSaveFields.Migrate(stateDocument, version);
        AutomaticEmperorCaptureSaveFields.Migrate(stateDocument, version);
        BaseTacticPresentationSaveFields.Migrate(stateDocument, version);
        CoalitionCameraPresentationSaveFields.Migrate(stateDocument, version);
        AreaCitySaveFields.Migrate(stateDocument, version);
        DiplomacySaveFields.Migrate(stateDocument, version);
        if (version < 149) stateDocument["NativePeriodContinuation"] = null;
        else if (!stateDocument.ContainsKey("NativePeriodContinuation"))
            throw new InvalidDataException("Saved native period continuation knowledge is missing.");
        if (stateDocument["PersonnelCommands"]?["Returns"] is JsonObject personnelJourneys)
            foreach (var entry in personnelJourneys)
            {
                if (entry.Value is not JsonObject journey) throw new InvalidDataException("Saved personnel journey is incomplete.");
                if (version < 150) journey["CancellationRequested"] = false;
                else if (journey["CancellationRequested"] is not JsonValue cancellationValue || !cancellationValue.TryGetValue<bool>(out _))
                    throw new InvalidDataException("Saved personnel cancellation knowledge is missing.");
                if (version < 146) journey["IsReassignment"] = false;
                else if (journey["IsReassignment"] is not JsonValue journeyKindValue || !journeyKindValue.TryGetValue<bool>(out _))
                    throw new InvalidDataException("Saved personnel journey kind is missing.");
            }
        if (stateDocument["Cities"] is JsonObject merchantCities)
            foreach (var entry in merchantCities)
            {
                if (entry.Value is not JsonObject city) throw new InvalidDataException("Saved City is incomplete.");
                if (version < 145) city["MerchantFoodPerTenGold"] = null;
                else if (!city.ContainsKey("MerchantFoodPerTenGold") || city["MerchantFoodPerTenGold"] is JsonNode price &&
                    (price is not JsonValue number || !number.TryGetValue<byte>(out _)))
                    throw new InvalidDataException("Saved merchant price is incomplete.");
            }
        // Before schema144 every unit-defeat request represented ordinary
        // battle destruction. No historical request is inferred to be a duel.
        if (stateDocument["PendingCombatSettlements"] is JsonObject settlements &&
            settlements["Requests"] is JsonArray requests)
            foreach (var entry in requests)
            {
                if (entry is not JsonObject request ||
                    request["SettlementKind"]?.GetValue<string>() != "unit-defeat") continue;
                if (version < 144) request["Cause"] = (int)UnitDefeatCause.OrdinaryBattle;
                else if (!request.ContainsKey("Cause"))
                    throw new InvalidDataException("Saved unit defeat cause is missing.");
            }
        if (version < 133) stateDocument["CampaignAppRandom32"] = null;
        else
        {
            if (!stateDocument.ContainsKey("CampaignAppRandom32"))
                throw new InvalidDataException("Saved campaign App random snapshot is missing.");
            if (stateDocument["CampaignAppRandom32"] is JsonNode randomNode &&
                (randomNode is not JsonObject random || random.Count != 1 ||
                 random["State"] is not JsonValue bits || !bits.TryGetValue<uint>(out _)))
                throw new InvalidDataException("Saved campaign App random snapshot is invalid.");
        }

        if (version <= 129) stateDocument["CampaignDuelService"] = null;
        else
        {
            if (!stateDocument.ContainsKey("CampaignDuelService"))
                throw new InvalidDataException("Saved campaign duel service knowledge is missing.");
            if (stateDocument["CampaignDuelService"] is JsonNode duelNode)
            {
                if (duelNode is not JsonObject duel || new[] { "Active", "Busy", "History" }.Any(k => !duel.ContainsKey(k)))
                    throw new InvalidDataException("Saved campaign duel service fields are missing.");
                foreach (string key in new[] { "Active", "Busy" })
                    if (duel[key] is JsonNode gate && (gate is not JsonValue duelGateValue || !duelGateValue.TryGetValue<bool>(out _)))
                        throw new InvalidDataException("Saved campaign duel service gate is invalid.");
                if (duel["History"] is not JsonValue duelHistoryValue || !duelHistoryValue.TryGetValue<int>(out int duelHistoryTag) ||
                    !Enum.IsDefined((CampaignDuelHistoryKnowledge)duelHistoryTag))
                    throw new InvalidDataException("Saved campaign duel service history is invalid.");
                if (version < 142)
                {
                    if (duelHistoryTag == (int)CampaignDuelHistoryKnowledge.KnownEntries)
                        throw new InvalidDataException("Historical save cannot claim an unavailable duel history list.");
                    duel["HistoricalPairs"] = duelHistoryTag == (int)CampaignDuelHistoryKnowledge.KnownEmpty ? new JsonArray() : null;
                    duel["LastSequence"] = 0;
                    duel["Registered"] = null;
                    duel["CompletedPresentations"] = new JsonArray();
                }
                else if (new[] { "HistoricalPairs", "LastSequence", "Registered", "CompletedPresentations" }
                    .Any(key => !duel.ContainsKey(key)))
                    throw new InvalidDataException("Saved duel registration fields are missing.");
                var presentations = duel["CompletedPresentations"] as JsonArray
                    ?? throw new InvalidDataException("Saved duel presentation queue is missing.");
                foreach (var item in presentations.Prepend(duel["Registered"]))
                {
                    if (item is null) continue;
                    if (item is not JsonObject registered)
                        throw new InvalidDataException("Saved registered duel is invalid.");
                    if (version < 143) registered["AftermathPresentation"] = null;
                    else if (!registered.ContainsKey("AftermathPresentation"))
                        throw new InvalidDataException("Saved duel aftermath presentation knowledge is missing.");
                }
            }
        }
        DuelPresentationSnapshotSaveFields.Migrate(stateDocument, version);
        if (version <= 130) stateDocument["DuelOpportunityCount"] = null;
        else
        {
            if (!stateDocument.ContainsKey("DuelOpportunityCount"))
                throw new InvalidDataException("Saved duel opportunity count knowledge is missing.");
            if (stateDocument["DuelOpportunityCount"] is JsonNode countNode &&
                (countNode is not JsonValue countValue || !countValue.TryGetValue<int>(out int count) || count is < 0 or > 255))
                throw new InvalidDataException("Saved duel opportunity count is invalid.");
        }
        if (stateDocument["Officers"] is JsonObject duelPeople)
            foreach (var entry in duelPeople)
            {
                if (entry.Value is not JsonObject person)
                    throw new InvalidDataException("Saved duel condition person is invalid.");
                if (version <= 130) person["DuelCondition"] = null;
                else
                {
                    if (!person.ContainsKey("DuelCondition"))
                        throw new InvalidDataException("Saved person duel condition knowledge is missing.");
                    if (person["DuelCondition"] is not JsonNode conditionNode) continue;
                    if (conditionNode is not JsonObject condition || !condition.ContainsKey("QualificationAdjustment") ||
                        !condition.ContainsKey("Readiness"))
                        throw new InvalidDataException("Saved person duel condition fields are missing.");
                    foreach (string key in new[] { "QualificationAdjustment", "Readiness" })
                        if (condition[key] is JsonNode valueNode &&
                            (valueNode is not JsonValue value || !value.TryGetValue<int>(out int scalar) ||
                            (key == "Readiness" ? scalar is < 0 or > 255 : scalar is < short.MinValue or > short.MaxValue)))
                            throw new InvalidDataException("Saved person duel condition field is invalid.");
                }
            }
        if (version < 120)
        {
            stateDocument["MapObjects"] = null;
            if (stateDocument["ArmyLifecycle"]?["Indexes"] is JsonObject oldIndexes)
                oldIndexes["PendingObjectRetirements"] = null;
        }
        if (stateDocument["ArmyLifecycle"]?["Receivers"]?["Entries"] is JsonArray savedReceivers)
            foreach (var receiverNode in savedReceivers)
                if (receiverNode is JsonObject receiver)
                {
                    if (version < 123) receiver["FacilityConstruction"] = null;
                    else if (!receiver.ContainsKey("FacilityConstruction"))
                        throw new InvalidDataException("Saved Army facility construction knowledge is missing.");
                    if (version < 117) receiver["SupplyInterdiction"] = null;
                    else if (!receiver.ContainsKey("SupplyInterdiction") || receiver["SupplyInterdiction"] is JsonObject interdiction &&
                        (!interdiction.ContainsKey("IsActive") || !interdiction.ContainsKey("TargetReceiverId")))
                        throw new InvalidDataException("Saved Army supply interdiction knowledge is missing or incomplete.");
                    if (version < 114) receiver["FalseReportDestination"] = null;
                    else if (!receiver.ContainsKey("FalseReportDestination"))
                        throw new InvalidDataException("Saved Army false-report destination is missing.");
                    if (version < 112)
                    {
                        JsonNode? image = receiver["PendingImage"];
                        if (receiver["ActiveUnitId"] is JsonValue active && active.TryGetValue<int>(out int id))
                            image = stateDocument["Units"]?[id.ToString(System.Globalization.CultureInfo.InvariantCulture)];
                        //Zero proves the inactive branch; a positive scalar
                        //cannot reconstruct the missing saved target.
                        receiver["SavedCommand"] = image?["NativeCommandControl140"] is JsonValue control &&
                            control.TryGetValue<byte>(out byte known) && known == 0
                            ? JsonSerializer.SerializeToNode(ArmySavedCommandState.Cleared) : null;
                    }
                    else if (!receiver.ContainsKey("SavedCommand"))
                        throw new InvalidDataException("Saved Army replaced-command knowledge is missing.");
                }
        if (version < 107) stateDocument["DefaultForceKingdomNameIndex"] = null;
        else if (!stateDocument.ContainsKey("DefaultForceKingdomNameIndex"))
            throw new InvalidDataException("Saved default force kingdom name index is missing.");
        // Older worlds did not own the current draft preferences. Do not
        // manufacture the fresh reset values when continuing those saves.
        if (version < 105) stateDocument["DefaultDeploymentBehavior"] = null;
        else if (!stateDocument.ContainsKey("DefaultDeploymentBehavior") ||
            stateDocument["DefaultDeploymentBehavior"] is JsonNode defaultBehavior &&
            (defaultBehavior is not JsonObject defaultFields ||
             new[] { "AttackNearbyEnemies", "AllowRetreat", "PursueRetreatingEnemies", "AfterAction" }
                 .Any(key => !defaultFields.ContainsKey(key))))
            throw new InvalidDataException("Saved default deployment behavior is missing or incomplete.");
        if (stateDocument["Forces"] is JsonObject kingdomForces)
            foreach (JsonNode? node in kingdomForces.Select(pair => pair.Value))
                if (node is JsonObject force)
                {
                    if (version < 103) force["KingdomNameIndex"] = null;
                    else if (!force.ContainsKey("KingdomNameIndex"))
                        throw new InvalidDataException("Saved force kingdom name index is missing.");
                }
        foreach (string basesKey in new[] { "Cities", "Gates" })
            if (stateDocument[basesKey] is JsonObject savedBases)
                foreach (var row in savedBases)
                    if (row.Value is JsonObject entry)
                    {
                        if (version < 113)
                        {
                            entry["DamageTier"] = null;
                            //Move the earlier exact City writer, when present;
                            //absence is not reconstructed from current capacity.
                            string legacyKey = "ce-advisor-refresh-current:city:" + row.Key + ":66";
                            if (basesKey == "Cities" && stateDocument["NativeArmySimulation"]?["Parameters"]?["Int32Values"] is JsonObject values &&
                                values[legacyKey] is JsonNode savedTier)
                            {
                                int tier = savedTier.GetValue<int>();
                                if (tier is < 0 or > 255) throw new InvalidDataException("Legacy saved City damage tier is invalid.");
                                entry["DamageTier"] = tier;
                                values.Remove(legacyKey);
                            }
                        }
                        else if (!entry.ContainsKey("DamageTier"))
                            throw new InvalidDataException("Saved Base damage tier is missing.");
                    }
        if (stateDocument["Officers"] is JsonObject eventOfficers)
            foreach (JsonNode? node in eventOfficers.Select(pair => pair.Value))
                if (node is JsonObject person)
                {
                    if (version < 133) person["AppearanceNotificationPersonId"] = null;
                    else if (!person.ContainsKey("AppearanceNotificationPersonId") ||
                        person["AppearanceNotificationPersonId"] is JsonNode notice &&
                        (notice is not JsonValue value || !value.TryGetValue<int>(out _)))
                        throw new InvalidDataException("Saved appearance notification identity is missing or invalid.");
                    // A historical combined key does not identify its component boundary.
                    foreach (string key in new[] { "CurrentSurnameSortKey", "CurrentGivenNameSortKey" })
                    {
                        if (version < 132) person[key] = null;
                        else if (!person.ContainsKey(key) || person[key] is JsonNode component &&
                            (component is not JsonValue text || !text.TryGetValue<string>(out _)))
                            throw new InvalidDataException("Saved person name-sort component is missing or invalid.");
                    }
                    // Historical full/given names do not prove their missing current surname.
                    if (version < 124) person["CurrentSurname"] = null;
                    else if (!person.ContainsKey("CurrentSurname"))
                        throw new InvalidDataException("Saved person current surname is missing.");
                    foreach (string key in new[] { "DialogueStyle", "BirthYear", "FamilyGeneration", "CurrentGivenName", "CurrentCourtesyName" })
                    {
                        if (version < 116) person[key] = null;
                        else if (!person.ContainsKey(key)) throw new InvalidDataException("Saved person dialogue fields are missing.");
                    }
                    if (version < 147) person["VoiceType"] = null;
                    else if (!person.ContainsKey("VoiceType") || person["VoiceType"] is JsonNode voice &&
                        (voice is not JsonValue scalar || !scalar.TryGetValue<byte>(out _)))
                        throw new InvalidDataException("Saved person voice type is missing or invalid.");
                    if (version < 110) person["Lifespan"] = null;
                    else if (!person.ContainsKey("Lifespan") || person["Lifespan"] is JsonObject lifespan &&
                        (!lifespan.ContainsKey("TerminalYear") || !lifespan.ContainsKey("Kind")))
                        throw new InvalidDataException("Saved person lifespan is incomplete.");
                    if (version < 118) person["CurrentTraitIds"] = null;
                    else if (!person.ContainsKey("CurrentTraitIds"))
                        throw new InvalidDataException("Saved person current trait slots are missing.");
                    if (version < 106) person["CurrentTacticSlots"] = null;
                    else if (!person.ContainsKey("CurrentTacticSlots"))
                        throw new InvalidDataException("Saved person current tactic slots are missing.");
                    if (version < 104) person["CurrentFormationMask"] = null;
                    else if (!person.ContainsKey("CurrentFormationMask"))
                        throw new InvalidDataException("Saved person current formation capabilities are missing.");
                    if (version < 102) person["AppearanceYear"] = null;
                    else if (!person.ContainsKey("AppearanceYear"))
                        throw new InvalidDataException("Saved person appearance year is missing.");
                    if (version < 108)
                    {
                        person["CurrentNameSortKey"] = null;
                        person["PortraitAppearanceIndex"] = null;
                    }
                    else if (!person.ContainsKey("CurrentNameSortKey") || !person.ContainsKey("PortraitAppearanceIndex"))
                        throw new InvalidDataException("Saved person current presentation fields are missing.");
                }
        if (version < 119) stateDocument["ManualTacticIntervention"] = null;
        else if (!stateDocument.ContainsKey("ManualTacticIntervention"))
            throw new InvalidDataException("Saved manual tactic manager is missing.");
        if (stateDocument["ManualTacticIntervention"] is JsonNode interventionNode)
        {
            if (interventionNode is not JsonObject manualManager || new[] { "Pending", "EffectBusy", "LastPresentationSequence", "Effects", "Directions" }.Any(k => !manualManager.ContainsKey(k)))
                throw new InvalidDataException("Saved manual tactic manager fields are missing.");
            if (manualManager["Pending"] is JsonObject pending && new[] { "Actor", "TacticId", "Target" }.Any(k => !pending.ContainsKey(k)))
                throw new InvalidDataException("Saved manual tactic request fields are missing.");
            static void ValidateManualActor(JsonObject fields)
            {
                if (fields["Actor"] is not JsonObject actorReference || !actorReference.ContainsKey("Value"))
                    throw new InvalidDataException("Saved manual tactic receiver is incomplete.");
                if (fields["Target"] is JsonNode target && (target is not JsonObject destination ||
                    !destination.ContainsKey("Kind") || !destination.ContainsKey("Id")))
                    throw new InvalidDataException("Saved manual tactic target is incomplete.");
            }
            if (manualManager["Pending"] is JsonNode savedPending)
            {
                if (savedPending is not JsonObject pendingFields) throw new InvalidDataException("Saved manual tactic request is invalid.");
                ValidateManualActor(pendingFields);
            }
            foreach (string queueKey in new[] { "Effects", "Directions" })
            {
                if (manualManager[queueKey] is not JsonArray queue) throw new InvalidDataException("Saved tactic presentation queue is missing.");
                foreach (var item in queue)
                {
                    if (item is not JsonObject request || new[] { "Sequence", "Kind", "Actors" }.Any(k => !request.ContainsKey(k)) || request["Actors"] is not JsonArray actors)
                        throw new InvalidDataException("Saved tactic presentation request is incomplete.");
                    foreach (var actor in actors)
                    {
                        if (actor is not JsonObject fields || new[] { "Actor", "TacticId", "Target", "InstructionArmyReceivers", "Effects" }.Any(k => !fields.ContainsKey(k)))
                            throw new InvalidDataException("Saved tactic presentation actor is incomplete.");
                        ValidateManualActor(fields);
                        if (fields["Effects"] is not JsonArray effects)
                            throw new InvalidDataException("Saved tactic numerical receipts are missing.");
                        foreach (var effect in effects)
                        {
                            if (effect is not JsonObject receipt || new[] { "Instruction", "Target", "Kind", "Amount", "Healing", "Abnormal", "MoraleAfter" }.Any(k => !receipt.ContainsKey(k)) ||
                                receipt["Target"] is not JsonObject targetReceiver || !targetReceiver.ContainsKey("Value"))
                                throw new InvalidDataException("Saved tactic numerical receipt is incomplete.");
                            if (receipt["Healing"] is JsonNode healing && (healing is not JsonObject healed ||
                                !healed.ContainsKey("TroopsAfter") || !healed.ContainsKey("WoundedAfter")))
                                throw new InvalidDataException("Saved tactic healing receipt is incomplete.");
                            if (receipt["Abnormal"] is JsonNode abnormal)
                            {
                                if (abnormal is not JsonObject assigned || new[] { "Abnormal23", "Abnormal24", "Abnormal25", "ProvocationOwner", "OverrideTarget" }.Any(k => !assigned.ContainsKey(k)))
                                    throw new InvalidDataException("Saved tactic abnormal receipt is incomplete.");
                                if (assigned["ProvocationOwner"] is JsonNode owner && (owner is not JsonObject provocation ||
                                    new[] { "ForceId", "Kind", "Id" }.Any(k => !provocation.ContainsKey(k))))
                                    throw new InvalidDataException("Saved tactic provocation receipt is incomplete.");
                                if (assigned["OverrideTarget"] is JsonNode overridden)
                                {
                                    if (overridden is not JsonObject selected || !selected.ContainsKey("Kind") || !selected.ContainsKey("Destination"))
                                        throw new InvalidDataException("Saved tactic override receipt is incomplete.");
                                    if (selected["Destination"] is JsonNode destination && (destination is not JsonObject destinationFields ||
                                        new[] { "Kind", "EntityId", "ReceiverId", "MapObjectReceiverId" }.Any(k => !destinationFields.ContainsKey(k))))
                                        throw new InvalidDataException("Saved tactic override destination receipt is incomplete.");
                                }
                            }
                        }
                    }
                }
            }
        }
        if (version < 113) stateDocument["NormalCombatRefresh"] = null;
        else if (!stateDocument.ContainsKey("NormalCombatRefresh"))
            throw new InvalidDataException("Saved normal combat refresh owner is missing.");
        if (version >= 115 && !stateDocument.ContainsKey("PersonnelCommands"))
            throw new InvalidDataException("Saved personnel command owner is missing.");
        if (stateDocument["PersonnelCommands"] is JsonNode commandNode)
        {
            if (commandNode is not JsonObject commands || new[] { "NextTaskId", "AvailableDispatchSlots", "Returns", "PendingRetirements", "Disappearances" }
                .Any(key => !commands.ContainsKey(key))) throw new InvalidDataException("Saved personnel command fields are missing.");
            if (commands["Disappearances"] is JsonNode absences)
            {
                if (absences is not JsonObject rows) throw new InvalidDataException("Saved personnel disappearance collection is invalid.");
                foreach (var row in rows)
                {
                    if (row.Value is not JsonObject fields || new[] { "Id", "DispatchSlot", "MemberIds", "Home", "DistrictId", "StationHex", "PreviousPosition",
                        "StartedOn", "DueOn", "Started", "NeedsRefresh", "IsPlayer", "CancellationRequested", "IsRetiring", "IsAssociated" }.Any(key => !fields.ContainsKey(key)))
                        throw new InvalidDataException("Saved personnel disappearance fields are missing.");
                    foreach (var (name, required) in new[] { ("Home", new[] { "Kind", "BaseId" }),
                        ("PreviousPosition", new[] { "Kind", "EntityId" }),
                        ("StartedOn", new[] { "Year", "Month", "Day", "Hour" }), ("DueOn", new[] { "Year", "Month", "Day", "Hour" }) })
                        if (fields[name] is not JsonObject leaf || required.Any(key => !leaf.ContainsKey(key)))
                            throw new InvalidDataException("Saved personnel disappearance " + name + " is incomplete.");
                }
            }
        }
        CampaignEffectSaveFields.Migrate(stateDocument, version);
        DuelCaptureAnnouncementSaveFields.Migrate(stateDocument, version);
        ArmyCommandSpeechSaveFields.Migrate(stateDocument, version);
        FirePresentationSaveFields.Migrate(stateDocument, version);
        CampaignHistorySaveFields.Migrate(stateDocument, version);
        AreaOccupationClaimSaveFields.Migrate(stateDocument, version);
        CampaignChronicleSaveFields.Migrate(stateDocument, version);
        if (version < 198 && stateDocument["CampaignEventWorld"] is JsonObject legacyEventWorld &&
            legacyEventWorld.ContainsKey("HistoricalEventControl"))
            throw new InvalidDataException("Legacy campaign event world contains a future historical-event control.");
        if (version < 100) stateDocument["CampaignEventWorld"] = null;
        else if (!stateDocument.ContainsKey("CampaignEventWorld") ||
            stateDocument["CampaignEventWorld"] is JsonNode eventWorld && (eventWorld is not JsonObject worldFields ||
                new[] { "ScenarioId", "ScenarioMode", "ElapsedPeriodOffset" }.Any(key => !worldFields.ContainsKey(key))))
            throw new InvalidDataException("Saved campaign event world is missing or incomplete.");
        if (stateDocument["CampaignEventWorld"] is JsonObject currentEventWorld)
        {
            if (version < 198) currentEventWorld["HistoricalEventControl"] = null;
            else if (!currentEventWorld.ContainsKey("HistoricalEventControl") ||
                currentEventWorld["HistoricalEventControl"] is JsonNode historicalControl &&
                (historicalControl is not JsonValue signedControl || !signedControl.TryGetValue<int>(out _)))
                throw new InvalidDataException("Saved historical-event control is missing or invalid.");
            if (version < 111) currentEventWorld["EventAchievementFlags"] = null;
            else if (!currentEventWorld.ContainsKey("EventAchievementFlags"))
                throw new InvalidDataException("Saved campaign event achievement flags are missing.");
            if (version < 109) currentEventWorld["SpecialPersonEncounterFlags"] = null;
            else if (!currentEventWorld.ContainsKey("SpecialPersonEncounterFlags"))
                throw new InvalidDataException("Saved special person encounter flags are missing.");
            if (version < 101) currentEventWorld["CurrentUserIndex"] = null;
            else if (!currentEventWorld.ContainsKey("CurrentUserIndex"))
                throw new InvalidDataException("Saved campaign event user index is missing.");
        }
        if (version < 99) { stateDocument["CampaignEvents"] = null; stateDocument["CampaignEventEligibility"] = null; }
        else if (!stateDocument.ContainsKey("CampaignEvents")) throw new InvalidDataException("Saved campaign event history is missing.");
        if (stateDocument["CampaignEvents"] is JsonNode eventHistory && (eventHistory is not JsonObject history ||
            new[] { "Occurred", "Acknowledged", "OccurrenceDates", "InitialStrategyProcessed" }.Any(key => !history.ContainsKey(key))))
            throw new InvalidDataException("Saved campaign event history fields are missing.");
        if (stateDocument["CampaignEvents"]?["OccurrenceDates"] is JsonArray occurrenceDates)
            foreach (var date in occurrenceDates)
                if (date is not null && (date is not JsonObject fields ||
                    new[] { "Year", "Month", "Day" }.Any(key => !fields.ContainsKey(key))))
                    throw new InvalidDataException("Saved campaign event occurrence date is incomplete.");
        if (!stateDocument.ContainsKey("CampaignEventEligibility") ||
            stateDocument["CampaignEventEligibility"] is JsonNode manager && (manager is not JsonObject eligibility || !eligibility.ContainsKey("Blocked")))
            throw new InvalidDataException("Saved event eligibility session is missing or incomplete.");
        // Earlier schemas could not execute progress stage1. They have no
        // pending theme selection; current saves must explicitly own this field.
        if (version < 97) stateDocument["LandmarkInvestments"] = null;
        else if (!stateDocument.ContainsKey("LandmarkInvestments"))
            throw new InvalidDataException("Saved landmark investment owner is missing.");
        if (stateDocument["LandmarkInvestments"] is JsonNode investments)
        {
            if (investments is not JsonObject rows) throw new InvalidDataException("Saved landmark investments are invalid.");
            foreach (var row in rows)
            {
                if (row.Value is not JsonObject fields || new[] { "Phase", "AssignedOfficerId", "CompletionDay" }.Any(key => !fields.ContainsKey(key)))
                    throw new InvalidDataException("Saved landmark investment fields are missing.");
                if (version < 98) fields["Development"] = null;
                else if (!fields.ContainsKey("Development")) throw new InvalidDataException("Saved landmark development owner is missing.");
                if (fields["Development"] is JsonNode development && (development is not JsonObject values ||
                    new[] { "Level", "EffectKind", "Effect" }.Any(key => !values.ContainsKey(key))))
                    throw new InvalidDataException("Saved landmark development fields are missing.");
                if (version < 99) fields["Flags"] = null;
                else if (!fields.ContainsKey("Flags")) throw new InvalidDataException("Saved landmark flags are missing.");
            }
        }
        if (version < 96) stateDocument["PendingMapMusicIntent"] = null;
        else if (!stateDocument.ContainsKey("PendingMapMusicIntent"))
            throw new InvalidDataException("Saved map music intent owner is missing.");
        if (stateDocument["PendingMapMusicIntent"] is JsonNode music)
            if (music is not JsonObject intent || !intent.ContainsKey("CalendarCycleOrdinal") ||
                !intent.ContainsKey("SourceSlot") || !intent.ContainsKey("UseStoredPlaybackPosition") ||
                !intent.ContainsKey("FadeInMilliseconds") || !intent.ContainsKey("FadeOutMilliseconds"))
                throw new InvalidDataException("Saved map music intent is incomplete.");
        if (version < 93) stateDocument["PendingArmyFireAnnouncements"] = new JsonArray();
        else if (stateDocument["PendingArmyFireAnnouncements"] is not JsonArray)
            throw new InvalidDataException("Saved army fire announcement owner is missing.");
        foreach (JsonNode? request in (JsonArray)stateDocument["PendingArmyFireAnnouncements"]!)
            if (request is not JsonObject item || !item.ContainsKey("UnitId") || !item.ContainsKey("OfficerId") || !item.ContainsKey("PersonalityId"))
                throw new InvalidDataException("Saved army fire announcement is incomplete.");
        if (version < 95) stateDocument["PendingForceRelationAnnouncements"] = new JsonArray();
        if (stateDocument["PendingForceRelationAnnouncements"] is not JsonArray relationAnnouncements)
            throw new InvalidDataException("Saved force relation announcement owner is missing.");
        foreach (JsonNode? request in relationAnnouncements)
            if (request is not JsonObject item || !item.ContainsKey("FirstForceId") || !item.ContainsKey("SecondForceId") || !item.ContainsKey("Level"))
                throw new InvalidDataException("Saved force relation announcement is incomplete.");
        if (version < 94)
        {
            stateDocument["PendingArmyFireCasts"] = new JsonArray();
            foreach (JsonNode? request in (JsonArray)stateDocument["PendingArmyFireAnnouncements"]!)
                request!["FromFireCommand"] = false;
        }
        if (stateDocument["PendingArmyFireCasts"] is not JsonArray casts)
            throw new InvalidDataException("Saved army fire cast owner is missing.");
        foreach (JsonNode? request in casts)
            if (request is not JsonObject item || !item.ContainsKey("UnitId") || !item.ContainsKey("OfficerId") || !item.ContainsKey("HexId"))
                throw new InvalidDataException("Saved army fire cast is incomplete.");
        foreach (JsonNode? request in (JsonArray)stateDocument["PendingArmyFireAnnouncements"]!)
            if (request?["FromFireCommand"] is not JsonValue flag || !flag.TryGetValue<bool>(out _))
                throw new InvalidDataException("Saved army fire cause is missing.");
        if (version < 89)
            resourceState = LegacyTransportRouteSaveMigration.Apply(stateDocument, resourceState, _resourceStateCodec);
        if (version < 90)
            resourceState = LegacyArmyTraitRosterSaveMigration.Apply(stateDocument, resourceState, _resourceStateCodec);
        if (version < 91)
            resourceState = LegacyArmyFormationSaveMigration.Apply(stateDocument, resourceState, _resourceStateCodec);
        if (version < 92) LegacyArmyMoraleSaveMigration.Apply(stateDocument, _resourceStateCodec);
        if (version < 86 && stateDocument.ContainsKey("ArmyIdentities"))
            throw new InvalidDataException("Historical save contains a current army identity sequence.");
        if (version >= 86 && (stateDocument["ArmyIdentities"] is not JsonObject identities ||
            identities.Count != 1 || identities["NextId"] is not JsonValue next ||
            !next.TryGetValue<long>(out long cursor) || cursor is < 1 or > ArmyIdentitySequence.Exhausted))
            throw new InvalidDataException("Saved army identity sequence is missing or invalid.");
        NormalizeItemLifecycle(stateDocument, version);
        ValidateModuleSnapshotShape(stateDocument, version);
        // A present options record must contain all choices. JSON constructor
        // defaults must not turn a missing boolean into a confirmed false choice.
        if (document["State"]?["NewGameOptions"] is JsonNode optionsNode &&
            (optionsNode is not JsonObject options ||
             !options.ContainsKey("DifficultyId") ||
             options["InitialCreditEnabled"] is not JsonValue initialCredit ||
             !initialCredit.TryGetValue<bool>(out _) ||
             options["CapturedOfficersEnabled"] is not JsonValue capturedOfficers ||
             !capturedOfficers.TryGetValue<bool>(out _)))
            throw new InvalidDataException("Saved new-game options are incomplete or invalid.");
        if (version < 3 && document["State"]?["Officers"] is JsonObject officers)
            foreach (JsonNode? node in officers.Select(entry => entry.Value))
                if (node is JsonObject officer && officer.ContainsKey("CityId"))
                {
                    officer["LocationId"] = officer["CityId"]?.DeepClone();
                    officer.Remove("CityId");
                }
        // Earlier saves had only an overseer field. Preserve an explicit known
        // assignment, but never invent a focus or interpret null as verified vacancy.
        // Schema 5 district budgets also remain unknown in old saves. Legacy force
        // budgets are retained verbatim, never copied into one or several districts.
        if (document["State"]?["Areas"] is JsonObject areas)
            foreach (JsonNode? node in areas.Select(entry => entry.Value))
                if (node is JsonObject area && area.ContainsKey("OverseerId"))
                {
                    if (version >= 5)
                        throw new InvalidDataException("Current save contains a legacy overseer field.");
                    if (area.ContainsKey("Administration"))
                        throw new InvalidDataException("Legacy save contains conflicting administration fields.");
                    area["Administration"] = area["OverseerId"] is JsonNode overseer
                        ? new JsonObject { ["OverseerId"] = overseer.DeepClone(), ["DevelopmentFocus"] = null }
                        : null;
                    area.Remove("OverseerId");
                }
        ForceAttackRequestSaveFields.Apply((JsonObject)document["State"]!, version);
        SaveEnvelope envelope;
        try
        {
            envelope = document.Deserialize<SaveEnvelope>()
                ?? throw new InvalidDataException("Save data is empty.");
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("Saved state contains an invalid typed field or collection.", error);
        }
        GameState state = (version < 23 ? MigrateCombatFood(envelope.State) : envelope.State) with { ResourceState = resourceState };
        StateValidator.Validate(state, _operationContext);
        if (version < 86) state = ArmyIdentityAllocation.Initialize(state);
        ArmyIdentityAllocation.ValidateSaved(state);
        ArmyTargetsSaveValidation.Validate(state);
        MapObjectLifecycleSaveValidation.Validate(state);
        ArmyOverrideSaveValidation.Validate(state);
        ArmyMarchSaveValidation.Validate(state);
        TransportRouteSaveValidation.Validate(state);
        ArmyTraitRosterSaveValidation.Validate(state);
        ArmyFormationSaveValidation.Validate(state);
        ArmyMoraleSaveValidation.Validate(state);
        return state;
    }

    private static void MigrateRuleIdentities(JsonNode node)
    {
        if (node is JsonObject owner)
        {
            foreach (string oldKey in new[] { "ExecutableSha256", "SourceExecutableSha256" })
                if (owner.ContainsKey(oldKey))
                {
                    owner["RuleVersion"] = owner[oldKey] is null ? null : SimulationRuleIdentity.Version;
                    owner.Remove(oldKey);
                }
            foreach (JsonNode? child in owner.Select(pair => pair.Value).ToArray())
                if (child is not null) MigrateRuleIdentities(child);
        }
        else if (node is JsonArray children)
            foreach (JsonNode? child in children)
                if (child is not null) MigrateRuleIdentities(child);
    }

    // An absent nullable module is legacy/unknown. A present module is a complete
    // snapshot: missing constructor fields must not quietly become zero, false,
    // or an empty task queue through JSON constructor defaults.
    private static void NormalizeItemLifecycle(JsonObject state, int version)
    {
        if (state["Items"] is null) return;
        if (state["Items"] is not JsonObject items) throw new InvalidDataException("Saved item table is invalid.");
        foreach (var pair in items)
        {
            if (pair.Value is not JsonObject item) throw new InvalidDataException("Saved item is invalid.");
            if (version < 134)
            {
                // A forged future key never upgrades old predicate knowledge.
                JsonNode? status = null;
                if (item["IsPresent"] is JsonNode presence)
                {
                    if (presence is not JsonValue scalar || !scalar.TryGetValue<bool>(out bool known))
                        throw new InvalidDataException("Legacy item presence is invalid.");
                    status = new JsonObject { ["Kind"] = known ? 0 : 1, ["Code"] = known ? JsonValue.Create((byte)1) : null };
                }
                item["Lifecycle"] = status is null ? null : new JsonObject
                { ["Status"] = status, ["RetainedLocationCode"] = null, ["RetainedFlags"] = null };
                item.Remove("IsPresent");
                continue;
            }
            if (item.ContainsKey("IsPresent") || !item.ContainsKey("Lifecycle"))
                throw new InvalidDataException("Saved item lifecycle is missing or duplicates presence.");
            if (item["Lifecycle"] is null) continue;
            if (item["Lifecycle"] is not JsonObject life || life.Count != 3 ||
                !life.ContainsKey("Status") || !life.ContainsKey("RetainedLocationCode") || !life.ContainsKey("RetainedFlags") ||
                life["RetainedLocationCode"] is JsonNode location && (location is not JsonValue l || !l.TryGetValue<ushort>(out _)) ||
                life["RetainedFlags"] is JsonNode flags && (flags is not JsonValue f || !f.TryGetValue<byte>(out _)))
                throw new InvalidDataException("Saved item lifecycle fields are invalid.");
            if (life["Status"] is null) continue;
            if (life["Status"] is not JsonObject s || s.Count != 2 || !s.ContainsKey("Code") ||
                s["Kind"] is not JsonValue k || !k.TryGetValue<int>(out int kind) || kind is < 0 or > 1 ||
                (kind == 0 ? s["Code"] is not JsonValue c || !c.TryGetValue<byte>(out _) : s["Code"] is not null))
                throw new InvalidDataException("Saved item status knowledge is invalid.");
        }
    }

    private static void ValidateModuleSnapshotShape(JsonObject state, int version)
    {
        if (version >= 120)
        {
            if (!state.ContainsKey("MapObjects")) throw new InvalidDataException("Saved map object lifecycle owner is missing.");
            if (state["MapObjects"] is JsonNode mapNode)
            {
                var objects = Require(mapNode, "map object lifecycle", "Receivers", "HexBindings");
                if (objects["Receivers"] is not JsonArray mapReceivers)
                    throw new InvalidDataException("Saved map object receiver collection is invalid.");
                foreach (var row in mapReceivers)
                {
                    var receiver = Require(row, "map object receiver", "Id", "Subject", "Pose", "BaseMapFaction");
                    _ = Require(receiver["Id"], "map object receiver identity", "Value");
                    if (receiver["Subject"] is JsonNode subject)
                        _ = Require(subject, "map object subject", "Category", "DefinitionKey", "LocationId", "StructureId");
                    if (receiver["Pose"] is JsonNode pose) _ = Require(pose, "map object pose", "Kind", "HexId");
                    if (receiver["BaseMapFaction"] is JsonNode faction)
                        _ = Require(faction, "Base map faction", "Kind", "ForceId");
                }
                if (objects["HexBindings"] is JsonNode bindingsNode)
                {
                    var bindings = Require(bindingsNode, "map object Hex bindings", "TileCount", "Occupied");
                    if (bindings["Occupied"] is not JsonObject occupied)
                        throw new InvalidDataException("Saved map object occupied cells are invalid.");
                    foreach (var entry in occupied)
                        _ = Require(entry.Value, "map object cell receiver", "Value");
                }
            }
        }
        if (state["Forces"] is JsonObject domainForces)
            foreach (JsonNode? force in domainForces.Select(pair => pair.Value))
            {
                JsonObject record = Require(force, "force state", "CombatObjective", "PolicyEffects");
                if (record["CombatObjective"] is JsonNode objective)
                    _ = Require(objective, "force combat objective", "Enabled", "TargetLocationId", "Progress");
                if (record["PolicyEffects"] is JsonNode effects)
                    _ = Require(effects, "force policy effects", "ActiveEffectIds", "SuppressedEffectIds");
            }
        if (!state.ContainsKey("ArmyMapOccupancy"))
            throw new InvalidDataException("Saved map occupancy owner is missing.");
        if (state["ArmyMapOccupancy"] is JsonNode occupancyNode)
        {
            JsonObject occupancy = Require(occupancyNode, "map occupancy", "DefaultCellKnowledge", "Cells", "Bindings");
            if (occupancy["Cells"] is not JsonObject cells || occupancy["Bindings"] is not JsonObject bindings)
                throw new InvalidDataException("Saved map occupancy directories are invalid.");
            foreach (JsonNode? cell in cells.Select(row => row.Value))
                _ = Require(cell, "map occupancy cell", "Kind", "BindingId");
            foreach (JsonNode? target in bindings.Select(row => row.Value))
                _ = Require(target, "map occupancy target", "Kind", "UnitId");
        }
        if (state["NativeBaseCaptureCurrent"] is JsonNode captureNode)
        {
            JsonObject capture = Require(captureNode, "native Base capture current", "Revision", "HexWords16", "Cities", "World1562");
            if (capture["HexWords16"] is not JsonArray || capture["Cities"] is not JsonObject captureCities)
                throw new InvalidDataException("Saved Base capture current directories are missing.");
            foreach (JsonNode? city in captureCities.Select(row => row.Value))
                _ = Require(city, "native City capture current", "C9", "CF");
        }
        if (state["NativePlayableTerminal"] is JsonNode terminal)
            _ = Require(terminal, "native playable terminal", "Kind", "PlayerForcePhysical", "TriggerVictimPhysical", "RuleId");
        if (version >= 42 || state.ContainsKey("ForceDefeats"))
        {
            if (state["ForceDefeats"] is not JsonObject defeats)
                throw new InvalidDataException("Saved force defeat ledger is missing or invalid.");
            foreach (JsonNode? entry in defeats.Select(pair => pair.Value))
            {
                JsonObject record = Require(entry, "force defeat", "ForceId", "Period", "CapturedCityId", "CapturingForceId", "RuleId",
                    "WithdrawnUnits", "UnresolvedOfficerIds", "MissingRuleKeys");
                if (record["WithdrawnUnits"] is not JsonArray withdrawals || record["UnresolvedOfficerIds"] is not JsonArray || record["MissingRuleKeys"] is not JsonArray)
                    throw new InvalidDataException("Saved force defeat disposition collections are missing.");
                foreach (JsonNode? unitNode in withdrawals)
                {
                    JsonObject unit = Require(unitNode, "withdrawn Army", "Id", "ForceId", "OfficerId", "HexId", "DestinationHexId", "Troops", "Supplies", "MoraleState",
                        "SourceLocationId", "Formations", "TacticIds", "Behavior", "Mission", "WoundedTroops", "MilitaryPlan", "TacticRuntime");
                    CheckUnitModules(unit);
                }
            }
        }
        if (version >= 41 || state.ContainsKey("PendingCommandReports"))
        {
            if (state["PendingCommandReports"] is not JsonArray pendingReports)
                throw new InvalidDataException("Saved pending command reports are missing or invalid.");
            foreach (JsonNode? report in pendingReports)
                _ = Require(report, "pending command report", "Kind", "ForceId", "CityId", "OfficerId",
                    "Amount", "FromHexId", "ToHexId", "LocationId", "RuleId", "MissingRuleKeys");
        }
        if (state["Officers"] is JsonObject officers)
            foreach (JsonNode? officerNode in officers.Select(pair => pair.Value))
            {
                if (officerNode is JsonObject aftermathOwner)
                {
                    if (aftermathOwner.ContainsKey("NativeDestructionRuntime") ||
                        !aftermathOwner.ContainsKey("Resentment") || !aftermathOwner.ContainsKey("AllegianceDisposition"))
                        throw new InvalidDataException("Saved officer aftermath knowledge is missing or obsolete.");
                    if (aftermathOwner["Resentment"] is JsonNode resentment)
                    {
                        var remembered = Require(resentment, "officer resentment", "TargetOfficerId", "Intensity");
                        foreach (string field in new[] { "TargetOfficerId", "Intensity" })
                            if (remembered[field] is JsonNode known &&
                                (known is not JsonValue scalar || !scalar.TryGetValue<int>(out int value) || value < 0))
                                throw new InvalidDataException("Saved officer resentment knowledge is invalid.");
                    }
                    if (aftermathOwner["AllegianceDisposition"] is JsonNode disposition)
                    {
                        var observed = Require(disposition, "officer allegiance disposition", "Value");
                        if (observed["Value"] is not JsonValue scalar || !scalar.TryGetValue<int>(out _))
                            throw new InvalidDataException("Saved officer allegiance disposition is invalid.");
                    }
                }
                if (officerNode is JsonObject meritOwner)
                {
                    if (!meritOwner.ContainsKey("MeritState") || meritOwner.ContainsKey("NativeCreditRuntime"))
                        throw new InvalidDataException("Saved officer merit owner is missing or obsolete.");
                    if (meritOwner["MeritState"] is JsonNode merit)
                        _ = Require(merit, "officer merit cycle", "Merit", "PendingMerit", "RankStageId",
                            "FirstSelectionCount", "SecondSelectionCount", "ResetCode");
                }
                if (officerNode is JsonObject growthOwner)
                {
                    if (!growthOwner.ContainsKey("Growth") || growthOwner.ContainsKey("NativeGrowthRuntime"))
                        throw new InvalidDataException("Saved officer growth owner is missing or obsolete.");
                    if (growthOwner["Growth"] is JsonNode growthNode)
                    {
                        JsonObject growth = Require(growthNode, "officer growth", "Experience", "Notifications");
                        if (growth["Experience"] is JsonNode experience && experience is not JsonArray)
                            throw new InvalidDataException("Saved officer experience channels are invalid.");
                        if (growth["Notifications"] is JsonNode noticesNode)
                        {
                            if (noticesNode is not JsonArray notices)
                                throw new InvalidDataException("Saved officer growth notifications are invalid.");
                            foreach (JsonNode? notice in notices)
                                _ = Require(notice, "ability growth notification", "AbilityIndex", "AfterAbility",
                                    "BeforeAbility", "ReportContext", "UninterpretedCode", "UninterpretedState");
                        }
                    }
                }
                if (officerNode is JsonObject locationOwner)
                {
                    if (!locationOwner.ContainsKey("Behavior"))
                        throw new InvalidDataException("Saved officer behavior owner is missing.");
                    if (locationOwner["Behavior"] is JsonNode behavior)
                        _ = Require(behavior, "officer behavior", "UsesSpecialPersonRules", "PreservesMaximumLoyalty",
                            "ReleaseAfterAiCapture", "UninterpretedFlags", "SpecialValidityMarker");
                    if (!locationOwner.ContainsKey("ServiceStatus"))
                        throw new InvalidDataException("Saved officer service status owner is missing.");
                    if (locationOwner["ServiceStatus"] is JsonNode service)
                        _ = Require(service, "officer service status", "KindId");
                    _ = Require(locationOwner["Appointment"], "officer appointment", "AssignedOfficeId", "ObservedOfficeId");
                    if (locationOwner.ContainsKey("OfficeId"))
                        throw new InvalidDataException("Saved officer contains an obsolete declared appointment owner.");
                    if (!locationOwner.ContainsKey("Health"))
                        throw new InvalidDataException("Saved officer health owner is missing.");
                    if (locationOwner["Health"] is JsonNode health)
                        _ = Require(health, "officer health", "StateId");
                    if (!locationOwner.ContainsKey("TraitActivation"))
                        throw new InvalidDataException("Saved officer activation owner is missing.");
                    if (locationOwner["TraitActivation"] is JsonNode activation)
                        _ = Require(activation, "officer trait activation", "ActiveSlots", "RemainingDurations");
                    if (!locationOwner.ContainsKey("LocationState") || locationOwner.ContainsKey("NativeLocationRuntime"))
                        throw new InvalidDataException("Saved officer location owner is missing or obsolete.");
                    if (locationOwner["LocationState"] is JsonNode location)
                    {
                        JsonObject fields = Require(location, "officer location knowledge", "Position", "Membership");
                        if (fields["Position"] is JsonNode position)
                            _ = Require(position, "officer position", "Kind", "EntityId");
                        if (fields["Membership"] is JsonNode membership)
                            _ = Require(membership, "officer membership", "Kind", "DistrictId");
                    }
                }
                if (officerNode is JsonObject freedomOwner && freedomOwner.ContainsKey("NativeFreedomRuntime"))
                    throw new InvalidDataException("Saved officer contains an obsolete freedom snapshot.");
                if (officerNode is JsonObject abilityOwner)
                {
                    if (!abilityOwner.ContainsKey("AbilityState") || abilityOwner.ContainsKey("NativeAbilityRuntime"))
                        throw new InvalidDataException("Saved officer ability owner is missing or obsolete.");
                    if (abilityOwner["AbilityState"] is JsonNode ability)
                        _ = Require(ability, "officer ability limits", "OrdinaryLimit", "CharmLimit", "ObservedDoctrineId");
                }
            }
        if (state["Cities"] is JsonObject cities)
            foreach (JsonNode? cityNode in cities.Select(pair => pair.Value))
                if (cityNode is JsonObject city && city["NativeReturnRuntime"] is JsonNode fields)
                    _ = Require(fields, "current City return", "Revision", "Morale44", "Wounded40", "Governor2E");
        if (state["Gates"] is JsonObject gates)
            foreach (JsonNode? gateNode in gates.Select(pair => pair.Value))
                if (gateNode is JsonObject gate && gate["NativeCombatRuntime"] is JsonNode fields)
                    _ = Require(fields, "current Gate combat", "Revision", "Morale44", "Wounded40");
        if (state["Areas"] is JsonObject areas)
            foreach (JsonNode? areaNode in areas.Select(pair => pair.Value))
            {
                if (areaNode is not JsonObject area) continue;
                if (area["Development"] is JsonNode development)
                    _ = Require(development, "area development", "Agriculture", "Commerce", "Barracks");
                if (area["EconomySource"] is JsonNode source)
                    _ = Require(source, "area economy source", "RawField44", "RawField46", "RawField52",
                        "FixedRemainingBytesHex", "ScenarioRemainingBytesHex", "FixedDataSha256", "ScenarioSha256");
                if (area["NativeNormalAftermath"] is JsonNode aftermath)
                    _ = Require(aftermath, "current Area normal aftermath", "Quantity52");
            }
        if (state["MonthlyIncomeReports"] is JsonNode incomeReportsNode)
        {
            if (incomeReportsNode is not JsonArray incomeReports)
                throw new InvalidDataException("Saved monthly income reports are invalid.");
            foreach (JsonNode? reportNode in incomeReports)
            {
                JsonObject report = Require(reportNode, "monthly income report", "CityId", "ForceId", "Period", "Quote");
                JsonObject quote = Require(report["Quote"], "monthly income quote", "Status", "RuleId", "MissingInputs", "Areas",
                    "Gold", "Supplies", "RuleVersion", "SettlementMonth", "CityId",
                    "GrossGold", "GrossSupplies", "GoldCapacity", "SuppliesCapacity", "CurrentGoldStock",
                    "CurrentSuppliesStock", "NextGoldStock", "NextSuppliesStock");
                if (quote["Areas"] is not JsonArray receipts)
                    throw new InvalidDataException("Saved monthly area receipts are invalid.");
                foreach (JsonNode? receiptNode in receipts)
                {
                    JsonObject receipt = Require(receiptNode, "monthly area receipt", "AreaId", "Status", "Inputs", "Steps", "MissingInputs");
                    if (receipt["Inputs"] is JsonArray inputs)
                        foreach (JsonNode? input in inputs) _ = Require(input, "economy input", "Name", "Value");
                    if (receipt["Steps"] is JsonArray steps)
                        foreach (JsonNode? step in steps) _ = Require(step, "economy step", "Name", "Expression", "Value");
                }
            }
        }
        if (state["Units"] is JsonObject units)
            foreach (JsonNode? unitNode in units.Select(pair => pair.Value))
            {
                if (unitNode is not JsonObject unit) continue;
                CheckUnitModules(unit);
            }
        if (state["NativeForceUnregisterCurrent"] is JsonNode forceUnregister)
            _ = Require(forceUnregister, "current Force unregister", "Revision", "DistrictWindows10",
                "ForceRankings", "ForceResetImages", "PlaceholderRelations13", "PlaceholderRelationsEA", "OtherForce1657");
        if (state["NormalCombatRefresh"] is JsonNode combatRefresh)
        {
            var refresh = Require(combatRefresh, "normal combat refresh", "Generation", "Engagements");
            if (refresh["Engagements"] is JsonArray engagements)
                foreach (var row in engagements)
                {
                    var pair = Require(row, "combat engagement", "Actor", "Target");
                    _ = Require(pair["Actor"], "engagement actor", "Kind", "ForceTableIndex");
                    _ = Require(pair["Target"], "engagement target", "Kind", "ForceTableIndex");
                }
        }
        CampaignEffectSaveFields.Check(state);
        if (version >= 120 && state["Structures"] is JsonObject structures)
            foreach (var item in structures)
                _ = Require(item.Value, "structure current durability capacity", "MaxDurability");

        if (state.ContainsKey("NativeArmyLifecycle"))
            throw new InvalidDataException("Saved state contains an obsolete source army lifecycle envelope.");
        if (!state.ContainsKey("ArmyLifecycle")) throw new InvalidDataException("Saved army lifecycle owner is missing.");
        if (state["ArmyLifecycle"] is JsonNode lifecycleNode)
        {
            JsonObject lifecycle = Require(lifecycleNode, "army lifecycle", "Receivers", "Indexes", "ArmyGroups", "ArmyCautions", "Targets");
            if (version >= 120)
            {
                var indexes = Require(lifecycle["Indexes"], "world indexes", "PendingObjectRetirements");
                if (indexes["PendingObjectRetirements"] is JsonNode pendingNode)
                {
                    if (pendingNode is not JsonArray pendingObjects)
                        throw new InvalidDataException("Saved pending map object retirements are invalid.");
                    foreach (var receiver in pendingObjects)
                        if (receiver is not null) _ = Require(receiver, "pending map object receiver", "Value");
                }
            }
            if (lifecycle.ContainsKey("Slots") || lifecycle.ContainsKey("ArmyFlags150") || lifecycle.ContainsKey("Revision") ||
                lifecycle.ContainsKey("Language") || lifecycle.ContainsKey("RuleVersion") || lifecycle.ContainsKey("SourceScenarioSha256") ||
                lifecycle.ContainsKey("SourceScenarioUri") || lifecycle.ContainsKey("Pool") || lifecycle.ContainsKey("HexRaw1C"))
                throw new InvalidDataException("Saved domain lifecycle contains obsolete source fields.");
            var roster = Require(lifecycle["Receivers"], "army receiver roster", "Entries");
            if (roster["Entries"] is not JsonArray receiverEntries)
                throw new InvalidDataException("Saved army receiver entries are invalid.");
            foreach (var entryNode in receiverEntries)
            {
                var entry = Require(entryNode, "army receiver", "ReceiverId", "CanAssign", "Phase", "ActiveUnitId", "PendingImage",
                    "StoredResources", "OverrideTarget", "GroupAssignmentReady", "OccupancyBindingId", "SavedCommand", "FalseReportDestination");
                if (version >= 123)
                {
                    _ = Require(entry, "army construction owner", "FacilityConstruction");
                    if (entry["FacilityConstruction"] is JsonNode selection)
                    {
                        var construction = Require(selection, "current construction selection", "DefinitionKey", "InitialSite", "ContinuationSite");
                        foreach (string key in new[] { "InitialSite", "ContinuationSite" })
                            if (construction[key] is JsonNode site) _ = Require(site, "construction site", "Kind", "HexId");
                    }
                }
                if (entry["SavedCommand"] is JsonNode savedCommand)
                {
                    var saved = Require(savedCommand, "saved replaced Army command", "Kind", "Command");
                    if (saved["Command"] is JsonNode snapshot)
                    {
                        var command = Require(snapshot, "replaced Army command snapshot", "Action", "Target");
                        _ = Require(command["Action"], "saved Army action", "Id");
                        var target = Require(command["Target"], "saved Army command target", "Kind", "Destination");
                        var savedDestination = Require(target["Destination"], "saved Army command destination", "Kind", "CityId", "ReceiverId", "HexId");
                        if (version >= 120)
                        {
                            _ = Require(savedDestination, "saved Army command map destination", "MapObjectReceiverId");
                            if (savedDestination["MapObjectReceiverId"] is JsonNode mapId)
                                _ = Require(mapId, "saved Army command map receiver", "Value");
                        }
                    }
                }
                _ = Require(entry["ReceiverId"], "army receiver identity", "Value");
                _ = Require(entry["StoredResources"], "retained army stores", "Gold", "Food");
                if (entry["OverrideTarget"] is JsonNode receiverOverride) CheckOverrideTarget(receiverOverride);
                if (entry["CanAssign"] is not JsonValue canAssign || !canAssign.TryGetValue<bool>(out _))
                    throw new InvalidDataException("Saved army receiver assignment knowledge is invalid.");
                if (entry["PendingImage"] is JsonNode pendingNode)
                {
                    JsonObject pending = Require(pendingNode, "pending army image", "Id", "ForceId", "OfficerId", "HexId",
                        "DestinationHexId", "Troops", "Supplies", "MoraleState");
                    CheckUnitModules(pending);
                }
            }
            if (lifecycle.ContainsKey("Targets38"))
                throw new InvalidDataException("Saved army targets contain an obsolete source table.");
            if (!lifecycle.ContainsKey("Targets")) throw new InvalidDataException("Saved army target owner is missing.");
            if (lifecycle["Targets"] is JsonNode targetsNode)
            {
                var targets = Require(targetsNode, "army target owner", "Assignments");
                if (targets["Assignments"] is not JsonArray assignments)
                    throw new InvalidDataException("Saved army target assignments are invalid.");
                foreach (var node in assignments)
                {
                    var row = Require(node, "army target assignment", "ReceiverId", "Target");
                    _ = Require(row["ReceiverId"], "army target receiver", "Value");
                    var target = Require(row["Target"], "army target knowledge", "Kind", "Destination");
                    if (target["Destination"] is JsonNode destination)
                    {
                        var targetDestination = Require(destination, "army target destination", "Kind", "CityId", "ReceiverId");
                        if (version >= 120)
                        {
                            _ = Require(targetDestination, "army map object target destination", "HexId", "MapObjectReceiverId");
                            if (targetDestination["MapObjectReceiverId"] is JsonNode mapId)
                                _ = Require(mapId, "army target map receiver", "Value");
                        }
                    }
                }
            }
            if (lifecycle["DeferredDestructionQueue"] is not null)
                throw new InvalidDataException("Saved state contains an obsolete combat queue owner.");
            if (lifecycle["ArmyGroups"] is JsonNode groupsNode)
            {
                var groups = Require(groupsNode, "army groups", "UnitAssignments", "Leaders", "ResetLeader");
                _ = Require(groups["ResetLeader"], "group reset leader", "Kind", "OfficerId");
                if (groups["UnitAssignments"] is not JsonObject assignments || groups["Leaders"] is not JsonObject leaders)
                    throw new InvalidDataException("Saved army group maps are invalid.");
                foreach (var row in assignments)
                    _ = Require(row.Value, "group assignment", "IsKnown", "GroupId");
                foreach (var row in leaders)
                {
                    var leader = Require(row.Value, "group leader", "IsKnown", "Officer");
                    _ = Require(leader["Officer"], "group officer", "Kind", "OfficerId");
                }
            }
            if (lifecycle.ContainsKey("Auxiliaries"))
                throw new InvalidDataException("Saved state contains obsolete army auxiliary tables.");
            if (lifecycle["ArmyCautions"] is JsonNode cautionsNode)
            {
                var cautions = Require(cautionsNode, "army cautions", "Relations", "GroupLabels");
                if (cautions["Relations"] is not JsonObject relations || cautions["GroupLabels"] is not JsonObject labels)
                    throw new InvalidDataException("Saved army caution maps are invalid.");
                foreach (var row in relations)
                {
                    var relation = Require(row.Value, "caution relation", "Unit", "Base");
                    _ = Require(relation["Unit"], "caution unit reference", "Kind", "UnitId");
                    _ = Require(relation["Base"], "caution base resource reference", "IsKnown", "MapObjectId");
                }
                foreach (var row in labels) _ = Require(row.Value, "caution group label", "IsKnown", "Label");
            }
        }
        if (state.ContainsKey("NativePersonCaptureWorldCandidates"))
            throw new InvalidDataException("Saved state contains an obsolete commander selection window.");
        if (!state.ContainsKey("CommanderSelection"))
            throw new InvalidDataException("Saved commander selection knowledge is missing.");
        if (state["CommanderSelection"] is JsonNode captureCandidatesNode)
        {
            JsonObject candidates = Require(captureCandidatesNode, "commander selection", "ActiveCount", "Candidates");
            if (candidates["Candidates"] is JsonNode entries)
            {
                if (entries is not JsonArray array)
                    throw new InvalidDataException("Saved commander selection candidates are invalid.");
                foreach (JsonNode? entry in array)
                    _ = Require(entry, "commander selection candidate", "OfficerId", "UninterpretedValue");
            }
        }
        if (state.ContainsKey("PersonnelReturns"))
            throw new InvalidDataException("Saved obsolete personnel owner was not migrated.");
        if (state["NativePersonReturnCommands"] is not null)
            throw new InvalidDataException("Saved state contains an obsolete personnel command owner.");
        static void CheckUnitModules(JsonObject unit)
        {
                if (!unit.ContainsKey("OverrideTarget")) throw new InvalidDataException("Saved army override knowledge is missing.");
                if (unit["MilitaryPlan"] is JsonNode planNode)
                {
                    JsonObject plan = Require(planNode, "military plan", "WaypointHexIds", "NextWaypointIndex", "Action");
                    JsonObject action = Require(plan["Action"], "military action", "ActionKey", "Target");
                    _ = Require(action["Target"], "military target", "Kind", "Id");
                }
                if (unit["TacticRuntime"] is JsonNode runtimeNode)
                {
                    JsonObject runtime = Require(runtimeNode, "tactic runtime", "Pending", "ReadyAtPeriod", "Statuses");
                    if (runtime["Pending"] is JsonNode pending)
                        _ = Require(pending, "queued tactic", "TacticId", "TargetUnitId", "IssuedAtPeriod", "ProfileRevision");
                    if (runtime["Statuses"] is JsonArray statuses)
                        foreach (JsonNode? status in statuses)
                            _ = Require(status, "tactic status", "Kind", "Amount", "SourceUnitId", "TacticId",
                                "ExpiresAtPeriod", "ProfileRevision");
                }
                if (unit["NativeTacticRuntime"] is JsonNode nativeNode)
                {
                    JsonObject native = Require(nativeNode, "native Army tactic runtime", "Status10", "Flags26",
                        "Waiting20", "Waiting21", "Waiting22", "Abnormal23", "Abnormal24", "Abnormal25",
                        "StatModifiers4A", "StatDurations50", "ProvocationOwner", "PrimaryWaitingReadyAtUpdateOrdinal");
                    if (native["ProvocationOwner"] is JsonNode owner)
                        _ = Require(owner, "native provocation owner", "ForceId", "Kind", "Id");
                }
                if (unit["NativeArmyOrigin"] is JsonNode armyOrigin)
                    _ = Require(armyOrigin, "source-created Army origin", "Revision", "Language", "RuleVersion", "NativeSlotIndex");
                if (unit.ContainsKey("NativeOverrideTarget")) throw new InvalidDataException("Saved army contains an obsolete override owner.");
                if (unit["OverrideTarget"] is JsonNode overrideTarget) CheckOverrideTarget(overrideTarget);
                if (unit.ContainsKey("NativeMarch") || unit.ContainsKey("NativeCurrentHex2A"))
                    throw new InvalidDataException("Saved army contains an obsolete position owner.");
                if (!unit.ContainsKey("March") || !unit.ContainsKey("PositionKnowledge"))
                    throw new InvalidDataException("Saved army marching knowledge is missing.");
                if (unit["March"] is JsonNode marchNode)
                {
                    JsonObject march = Require(marchNode, "army marching knowledge", "MovementCredit", "Target", "NextStep");
                    foreach (string key in new[] { "Target", "NextStep" })
                        if (march[key] is JsonNode destination) _ = Require(destination, "march destination", "Kind", "HexId");
                }
                if (unit.ContainsKey("Morale") || unit.ContainsKey("NativeMorale1A"))
                    throw new InvalidDataException("Saved army contains an obsolete morale owner.");
                var morale = Require(unit["MoraleState"], "army morale", "ProvisionalValue", "CurrentValue");
                if (morale.Count != 2) throw new InvalidDataException("Saved army morale ownership is invalid.");
                if (unit.ContainsKey("NativeArmyConfiguration") || unit.ContainsKey("FormationId"))
                    throw new InvalidDataException("Saved army contains an obsolete configuration owner.");
                if (!unit.ContainsKey("Formations") || !unit.ContainsKey("FieldAttackPolicy"))
                    throw new InvalidDataException("Saved army formation or attack policy is missing.");
                if (unit["Formations"] is JsonNode formationNode)
                {
                    var formations = Require(formationNode, "army formations", "SelectedLandFormationId", "CurrentLandFormationId", "NavalFormationId");
                    if (formations.Count != 3) throw new InvalidDataException("Saved army formation ownership is invalid.");
                }
                if (unit["FieldAttackPolicy"] is JsonNode policyNode)
                {
                    var policy = Require(policyNode, "army field-attack policy", "UsesConfusionGate");
                    if (policy.Count != 1) throw new InvalidDataException("Saved army field-attack policy is invalid.");
                }
                if (!unit.ContainsKey("TraitRoster")) throw new InvalidDataException("Saved army trait roster is missing.");
                if (unit["TraitRoster"] is JsonNode traitNode)
                {
                    var roster = Require(traitNode, "army trait roster", "TraitIds");
                    if (roster.Count != 1 || roster["TraitIds"] is not JsonArray) throw new InvalidDataException("Saved army trait roster is invalid.");
                }
                if (unit.ContainsKey("NativeReturnResources"))
                    throw new InvalidDataException("Saved Army contains obsolete carried stores.");
                if (!unit.ContainsKey("CarriedStores"))
                    throw new InvalidDataException("Saved Army stores observation is missing.");
                if (unit["CarriedStores"] is JsonNode stores)
                    _ = Require(stores, "carried Army stores", "Gold", "Food");
                if (unit.ContainsKey("NativeTransport") || !unit.ContainsKey("TransportRoute"))
                    throw new InvalidDataException("Saved transport route owner is missing or obsolete.");
                if (unit["TransportRoute"] is JsonNode transport)
                {
                    JsonObject route = Require(transport, "transport route", "Waypoints");
                    if (route.Count != 1 || route["Waypoints"] is not JsonArray waypoints)
                        throw new InvalidDataException("Saved transport waypoints are invalid.");
                    foreach (var waypoint in waypoints)
                        if (waypoint is not null) _ = Require(waypoint, "transport waypoint", "Kind", "HexId");
                }
        }
        if (state["NativeClock"] is JsonNode nativeClock)
        {
            JsonObject clock = Require(nativeClock, "native clock", "AnchorDate", "UpdateOrdinal", "UpdatesIntoPeriod", "Revision");
            _ = Require(clock["AnchorDate"], "native period anchor", "Year", "Month", "Day");
        }
        if (state["NativeSubdayReports"] is JsonNode nativeReportNode)
        {
            if (nativeReportNode is not JsonArray nativeReports)
                throw new InvalidDataException("Native subday report collection is invalid.");
            foreach (JsonNode? report in nativeReports)
                _ = Require(report, "native subday report", "Kind", "UpdateOrdinal", "UnitId", "ForceId", "NativeIndex", "Amount");
        }
        if (state["NativeArmySimulation"] is JsonNode nativeSimulationNode)
        {
            JsonObject simulation = Require(nativeSimulationNode, "owned native Army simulation", "Parameters", "Forces", "World");
            _ = Require(simulation["Parameters"], "owned native parameters", "Revision", "Language", "RuleVersion",
                "Int32Values", "Float32Values", "OfficeBonusEncoding");
            if (simulation["Forces"] is not JsonObject forceFields)
                throw new InvalidDataException("Owned native force field collection is invalid.");
            foreach (JsonNode? forceFieldsNode in forceFields.Select(pair => pair.Value))
            {
                _ = Require(forceFieldsNode, "owned native force fields", "Flags193", "Flags12", "Flags194", "RelationsEA");
                if (forceFieldsNode is JsonObject obsoletePolicy && (obsoletePolicy.ContainsKey("ActivePolicyBitmap1CB") ||
                    obsoletePolicy.ContainsKey("SuppressedPolicyBitmap1D5")))
                    throw new InvalidDataException("Saved force contains an obsolete policy effect owner.");
                if (forceFieldsNode is JsonObject oldForce && (oldForce.ContainsKey("ResultGate15F") ||
                    oldForce.ContainsKey("ResultDefinition160") || oldForce.ContainsKey("ResultValue162")))
                    throw new InvalidDataException("Saved force contains an obsolete combat objective owner.");
                if (forceFieldsNode is JsonObject forceFieldsObject && forceFieldsObject["NormalAftermath"] is JsonNode aftermath)
                    _ = Require(aftermath, "current Force normal aftermath", "Relations13", "Development1BE");
            }
            JsonObject world = Require(simulation["World"], "owned native world fields");
            if (world.ContainsKey("SelectedCommanderCount165D"))
                throw new InvalidDataException("Saved world contains an obsolete commander selection count.");
            if (version >= 39) _ = Require(world, "owned native world result selection", "SelectedForce1656");
            if (world["SamplerEpoch"] is JsonNode sampler)
                _ = Require(sampler, "owned native sampler epoch", "SourceScenarioUri", "SourceScenarioSha256",
                    "SourceWorldSha256", "Day450", "Salt45C", "NativeClockRevision", "Revision");
            if (world["ObjectPairs"] is JsonArray objectPairs)
                foreach (JsonNode? objectPair in objectPairs)
                    _ = Require(objectPair, "owned native object pair", "FirstArmyId", "BaseLocationId", "SecondArmyId");
        }
        if (state["NativeMapSeason"] is JsonNode nativeMapSeasonNode)
        {
            JsonObject season = Require(nativeMapSeasonNode, "owned native map season", "SourceScenarioUri", "SourceScenarioSha256",
                "SourceWorldSha256", "SecondDate", "ThirdDate", "ProcessedNativeOrdinal", "NativeClockRevision", "WriterRevision",
                "Phase", "AdvancedCalendarSteps", "AdvancedDays", "AdvancedPeriods", "AdvancedMonths");
            _ = Require(season["SecondDate"], "native second date", "Year", "Month", "Day", "Hour");
            _ = Require(season["ThirdDate"], "native third date", "Year", "Month", "Day", "Hour");
        }
        if (state["NativeNormalArmyCombatCalendar"] is JsonNode combatCalendarNode)
        {
            JsonObject cursor = Require(combatCalendarNode, "normal Army combat calendar cursor",
                "ProfileId", "SourceScenarioUri", "SourceScenarioSha256", "SourceWorldSha256", "NativeClockRevision",
                "StartCalendarCycleOrdinal", "NextCalendarCycleOrdinal", "ReachedDuePhaseCount",
                "LastAdvanceDiagnostics", "Revision");
            if (cursor["LastAdvanceDiagnostics"] is not JsonArray diagnostics)
                throw new InvalidDataException("Normal Army phase diagnostics are invalid.");
            foreach (JsonNode? node in diagnostics)
            {
                JsonObject diagnostic = Require(node, "normal Army phase diagnostic", "CalendarCycleOrdinal",
                    "PreWriterThirdDate", "Outcome", "OrderedArmyPairCount", "Reason", "SourceRevision");
                _ = Require(diagnostic["PreWriterThirdDate"], "normal Army pre-calendar date", "Year", "Month", "Day", "Hour");
            }
        }
        if (state["Diplomacy"] is JsonNode diplomacyNode)
        {
            JsonObject diplomacy = Require(diplomacyNode, "diplomacy", "Origin", "Relationships", "Treaties",
                "Missions", "AttackRequests", "NextMissionId");
            CheckRows(diplomacy["Relationships"], "diplomatic relationship",
                ["FirstForceId", "SecondForceId", "Level", "Origin"]);
            CheckRows(diplomacy["Treaties"], "diplomatic treaty",
                ["FirstForceId", "SecondForceId", "Kind", "StartedPeriod", "ExpiresPeriod", "Origin"]);
            CheckRows(diplomacy["Missions"], "diplomatic mission",
                ["Id", "ForceId", "SourceCityId", "DistrictId", "ExecutorOfficerId", "TargetForceId", "Kind",
                    "SubmittedPeriod", "ArrivalPeriod", "TreatyPeriods", "RequestPeriods", "TargetCityId",
                    "TargetCityForceId", "PaidGold", "PaidCommandBooks", "PolicyId"]);
            CheckRows(diplomacy["AttackRequests"], "diplomatic attack request",
                ["Id", "RequestingForceId", "RequestedForceId", "TargetCityId", "TargetForceId",
                    "AcceptedPeriod", "ExpiresPeriod", "Origin"]);
        }
        if (state["TacticReports"] is JsonArray reports)
            foreach (JsonNode? report in reports)
                _ = Require(report, "tactic report", "Kind", "Period", "ForceId", "UnitId", "OfficerId", "TacticId");
        if (state["EventRuntime"] is JsonNode eventRuntime)
        {
            JsonObject runtime = Require(eventRuntime, "event runtime", "LastSettledPeriod", "SourceRevisions",
                "Executions", "Flags", "Counters");
            CheckRows(runtime["Executions"], "event execution", ["Revision", "LastPeriod", "ExecutionCount"]);
        }
        if (state["EventReports"] is JsonArray eventReports)
            foreach (JsonNode? eventReport in eventReports)
            {
                JsonObject report = Require(eventReport, "event report", "Kind", "EventKey", "Revision", "Period");
                if (report["Source"] is JsonNode source)
                    _ = Require(source, "event source", "ResourceUri", "ResourceSha256", "DirectoryIndex", "EventId");
            }
        if (state["PersonnelReports"] is JsonArray personnelReports)
            foreach (JsonNode? report in personnelReports)
                _ = Require(report, "personnel report", "Kind", "Period", "ForceId", "OfficerId", "ItemId",
                    "PreviousHolderId", "NextHolderId", "PreviousOfficeId", "NextOfficeId", "CommandBooks",
                    "Gold", "LoyaltyDelta", "PolicyRevision", "DistrictId", "GoldCityId", "ReportedInPeriod");

        static void CheckOverrideTarget(JsonNode node)
        {
            JsonObject target = Require(node, "army override target", "Kind", "Destination");
            if (target.ContainsKey("Kind3C") || target.ContainsKey("Identity3E"))
                throw new InvalidDataException("Saved override contains obsolete source fields.");
            if (target["Destination"] is JsonNode destination)
                _ = Require(destination, "army override destination", "Kind", "EntityId", "ReceiverId", "MapObjectReceiverId");
        }
        static JsonObject Require(JsonNode? node, string label, params string[] keys)
        {
            if (node is not JsonObject record || keys.Any(key => !record.ContainsKey(key)))
                throw new InvalidDataException($"Saved {label} is incomplete or invalid.");
            return record;
        }
        static void CheckRows(JsonNode? node, string label, string[] keys)
        {
            if (node is not JsonObject rows)
                throw new InvalidDataException($"Saved {label} collection is missing or invalid.");
            foreach (JsonNode? row in rows.Select(pair => pair.Value)) _ = Require(row, label, keys);
        }
    }

    private static GameState MigrateCombatFood(GameState state)
    {
        if (!PlayableSkeleton.IsPlayableRuleset(state.RulesetId)) return state;
        var cities = new Dictionary<int, CityState>(state.Cities);
        var units = new Dictionary<int, UnitState>(state.Units);
        foreach (UnitState unit in state.Units.Values.OrderBy(unit => unit.Id))
        {
            if (unit.Mission?.Kind == UnitMissionKind.Transport || unit.Supplies == 0)
                continue;
            if (unit.SourceLocationId is not int sourceId ||
                !cities.TryGetValue(sourceId, out CityState? source) ||
                source.ForceId != unit.ForceId)
                throw new InvalidDataException($"Legacy combat food for unit {unit.Id} has no owned source city; migration cannot refund it safely.");
            cities[sourceId] = source with { Supplies = checked(source.Supplies + unit.Supplies) };
            units[unit.Id] = unit with { Supplies = 0 };
        }
        return state with { Cities = cities, Units = units };
    }
}

public sealed class SaveStore(ISaveCodec codec)
{
    public void Save(string path, GameState state)
    {
        byte[] bytes = codec.Encode(state);
        AtomicSaveFiles.Write(path, bytes);
    }

    public GameState Load(string path) => codec.Decode(File.ReadAllBytes(path));
}

/// <summary>Flushes a same-directory temporary file before atomic publication.
/// First-creation callers can forbid replacement of an existing file.</summary>
public static class AtomicSaveFiles
{
    public static void Write(string path, byte[] bytes, bool overwrite = true)
    {
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        string temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        int replacementAttempts = 0;
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            if (overwrite && File.Exists(fullPath))
            {
                while (true)
                {
                    replacementAttempts++;
                    try
                    {
                        File.Replace(temporary, fullPath, null);
                        break;
                    }
                    catch (IOException failure) when (CanRetryReplacement(failure, temporary, fullPath, replacementAttempts))
                    {
                        // Reuse the same closed, durable payload. Never delete the target
                        // or retry replacement failures that can have moved either file.
                        Thread.Sleep(replacementAttempts switch { 1 => 25, 2 => 75, _ => 150 });
                    }
                }
            }
            else File.Move(temporary, fullPath);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            // Retain failed publication inputs. Cleanup must not discard the durable
            // payload or mask the original OS failure with a second delete failure.
            failure.Data["AtomicSave.Target"] = fullPath;
            failure.Data["AtomicSave.Temporary"] = temporary;
            failure.Data["AtomicSave.Win32Error"] = failure.HResult & 0xffff;
            failure.Data["AtomicSave.ReplacementAttempts"] = replacementAttempts;
            failure.Data["AtomicSave.TargetExists"] = File.Exists(fullPath);
            failure.Data["AtomicSave.TemporaryExists"] = File.Exists(temporary);
            throw;
        }
    }

    private static bool CanRetryReplacement(IOException failure, string temporary, string target, int attempts)
    {
        if (!OperatingSystem.IsWindows() || (uint)failure.HResult >> 16 != 0x8007 || attempts >= 4 ||
            (failure.HResult & 0xffff) is not (32 or 33 or 1175))
            return false;
        // These errors leave both names in place. Missing/renamed files, directories
        // and access/attribute failures are not a transient replacement retry.
        try
        {
            const FileAttributes rejected = FileAttributes.Directory | FileAttributes.ReadOnly | FileAttributes.ReparsePoint;
            return (File.GetAttributes(temporary) & rejected) == 0 &&
                (File.GetAttributes(target) & rejected) == 0;
        }
        catch (Exception observationFailure) when (observationFailure is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
