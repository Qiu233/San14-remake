using System.Text.Json.Nodes;
namespace San14.Core;

internal static class CampaignEffectSaveFields
{
    internal static void Migrate(JsonObject state, int version)
    {
        if (version < 164 && state["CampaignEffectRequests"]?["PendingBatches"] is JsonArray strategicBatches)
            foreach (var batch in strategicBatches.OfType<JsonObject>())
                if (batch["Source"] is JsonValue value && value.TryGetValue<int>(out int source) &&
                    source >= (int)CampaignEffectSource.StrategicCommands)
                    throw new InvalidDataException("Legacy campaign batch has a future strategic command origin.");
        if (version < 164 && state["CampaignEffectRequests"]?["PendingBatches"] is JsonArray legacyBatches)
            foreach (var batch in legacyBatches.OfType<JsonObject>())
                if (batch["Requests"] is JsonArray requests)
                    foreach (var request in requests.OfType<JsonObject>())
                        if (request["TalentCommandPresentation"]?["Operation"] is JsonValue value &&
                            value.TryGetValue<int>(out int operation) &&
                            operation >= (int)TalentCommandPresentationOperation.ConditionalOfficerText)
                            throw new InvalidDataException("Legacy campaign presentation has a future operation.");
        if (version < 163 && state["CampaignEffectRequests"]?["PendingBatches"] is JsonArray researchBatches)
            foreach (var batch in researchBatches.OfType<JsonObject>())
                if (batch["Source"] is JsonValue value && value.TryGetValue<int>(out int source) &&
                    source >= (int)CampaignEffectSource.DoctrineResearchCommands)
                    throw new InvalidDataException("Legacy campaign batch has a future research command origin.");
        if (version < 159 && state["CampaignEffectRequests"]?["PendingBatches"] is JsonArray personnelBatches)
            foreach (var batch in personnelBatches.OfType<JsonObject>())
                if (batch["Source"] is JsonValue value && value.TryGetValue<int>(out int source) &&
                    source >= (int)CampaignEffectSource.PersonnelAdministrationCommands)
                    throw new InvalidDataException("Legacy campaign batch has a future personnel administration command origin.");
        if (version < 157 && state["CampaignEffectRequests"]?["PendingBatches"] is JsonArray officeBatches)
            foreach (var batch in officeBatches.OfType<JsonObject>())
                if (batch["Source"] is JsonValue value && value.TryGetValue<int>(out int source) &&
                    source >= (int)CampaignEffectSource.OfficerOfficeCommands)
                    throw new InvalidDataException("Legacy campaign batch has a future officer office command origin.");
        // Reject a future payload before any older migration can discard its
        // owner. Version113 also checks its reconstructed legacy batches below.
        MigratePersonLifespanNotificationFields(state, version);
        MigrateForcePolicyLevelUpFields(state, version);
        if (version >= 114)
        {
            if (!state.ContainsKey("CampaignEffectRequests")) throw new InvalidDataException("Saved campaign effects are missing.");
            if (state["NormalCombatRefresh"] is JsonObject current && current.ContainsKey("PendingEffects"))
                throw new InvalidDataException("Saved combat refresh contains obsolete effect ownership.");
            MigrateObjectAndAssociationFields(state, version);
            MigrateFacilityFields(state, version);
            MigrateRepairFields(state, version);
            MigrateConstructionFields(state, version);
            MigrateTraitFields(state, version);
            MigrateTrapFields(state, version);
            MigrateFireDamageFields(state, version);
            MigrateTerrainDamageFields(state, version);
            MigrateOfficerGrowthFields(state, version);
            MigratePersonAppearanceFields(state, version);
            MigratePersonDeathFields(state, version);
            return;
        }
        state["CampaignEffectRequests"] = null;
        if (version != 113 || state["NormalCombatRefresh"] is not JsonObject refresh) return;
        if (refresh["PendingEffects"] is not JsonArray oldBatches || refresh["Generation"] is not JsonValue generationValue ||
            !generationValue.TryGetValue<long>(out long generation)) throw new InvalidDataException("Legacy combat batches are incomplete.");
        var batches = new JsonArray(); long previous = 0, sequence = 0;
        foreach (var node in oldBatches)
        {
            if (node is not JsonObject old || old["Generation"] is not JsonValue value ||
                !value.TryGetValue<long>(out long revision) || revision <= previous || revision > generation ||
                old["Effects"] is not JsonArray effects) throw new InvalidDataException("Legacy combat batch order is invalid.");
            previous = revision;
            var requests = (JsonArray)effects.DeepClone();
            foreach (var request in requests)
                if (request is JsonObject fields && fields["Report"] is JsonObject report) report["Event"] = 0;
            batches.Add(new JsonObject { ["Sequence"] = ++sequence, ["Source"] = 0, ["SourceRevision"] = revision, ["Requests"] = requests });
        }
        state["CampaignEffectRequests"] = new JsonObject { ["LastBatchSequence"] = sequence, ["PendingBatches"] = batches };
        refresh.Remove("PendingEffects");
        MigrateObjectAndAssociationFields(state, version);
        MigrateFacilityFields(state, version);
        MigrateRepairFields(state, version);
        MigrateConstructionFields(state, version);
        MigrateTraitFields(state, version);
            MigrateTrapFields(state, version);
        MigrateFireDamageFields(state, version);
        MigrateTerrainDamageFields(state, version);
        MigrateOfficerGrowthFields(state, version);
            MigratePersonAppearanceFields(state, version);
        MigratePersonDeathFields(state, version);
        MigratePersonLifespanNotificationFields(state, version);
        MigrateForcePolicyLevelUpFields(state, version);
    }

    private static void MigrateForcePolicyLevelUpFields(JsonObject state, int version)
    {
        if (state["CampaignEffectRequests"]?["PendingBatches"] is not JsonArray batches) return;
        foreach (var batch in batches.OfType<JsonObject>())
        {
            if (version < 156 && batch["Source"] is JsonValue origin && origin.TryGetValue<int>(out int source) &&
                source >= (int)CampaignEffectSource.ForcePolicyLevelUp)
                throw new InvalidDataException("Legacy campaign batch has a future force policy level-up origin.");
            if (batch["Requests"] is not JsonArray requests) continue;
            foreach (var request in requests.OfType<JsonObject>())
            {
                if (version < 156)
                {
                    if (request["ForcePolicyLevelUp"] is not null)
                        throw new InvalidDataException("Legacy campaign request has a future force policy level-up payload.");
                    request["ForcePolicyLevelUp"] = null;
                }
                else if (!request.ContainsKey("ForcePolicyLevelUp"))
                    throw new InvalidDataException("Saved force policy level-up knowledge is missing.");
            }
        }
    }

    private static void MigratePersonLifespanNotificationFields(JsonObject state, int version)
    {
        if (version >= 139 || state["CampaignEffectRequests"] is not JsonObject owner ||
            owner["PendingBatches"] is not JsonArray batches) return;
        foreach (var node in batches)
            if (node is JsonObject batch && batch["Requests"] is JsonArray requests)
            {
                if (batch["Source"] is JsonValue value && value.TryGetValue<int>(out int source) &&
                    source >= (int)CampaignEffectSource.PersonLifespanNotifications)
                    throw new InvalidDataException("Legacy campaign batch has a future lifespan notification origin.");
                foreach (var entry in requests)
                    if (entry is JsonObject request)
                    {
                        if (request["PersonLifespanNotification"] is not null)
                            throw new InvalidDataException("Legacy campaign request has a future lifespan notification payload.");
                        request["PersonLifespanNotification"] = null;
                    }
            }
    }

    private static void MigratePersonDeathFields(JsonObject state, int version)
    {
        if (version >= 136 || state["CampaignEffectRequests"] is not JsonObject owner ||
            owner["PendingBatches"] is not JsonArray batches) return;
        foreach (var node in batches)
            if (node is JsonObject batch && batch["Requests"] is JsonArray requests)
            {
                if (version == 135)
                {
                    //The same field shape acquired new semantics in136.
                    //Reject future operations/controller6 before deserialization.
                    foreach (var entry in requests)
                        if (entry is JsonObject request && request["PersonDeathReport"] is JsonObject report &&
                            (report["Operation"] is not JsonValue operation ||
                             !operation.TryGetValue<int>(out int op) || op is < 0 or > 2 ||
                             report["EventKind"] is JsonValue kind && kind.TryGetValue<int>(out int eventKind) && eventKind == 6))
                            throw new InvalidDataException("Schema135 has a future person death report operation.");
                    continue;
                }
                if (batch["Source"] is JsonValue value && value.TryGetValue<int>(out int source) &&
                    source >= (int)CampaignEffectSource.PersonDeath)
                    throw new InvalidDataException("Legacy campaign batch has a future person death origin.");
                foreach (var entry in requests)
                    if (entry is JsonObject request) request["PersonDeathReport"] = null;
            }
    }

    private static void MigratePersonAppearanceFields(JsonObject state, int version)
    {
        if (version >= 133 || state["CampaignEffectRequests"] is not JsonObject owner ||
            owner["PendingBatches"] is not JsonArray batches) return;
        foreach (var node in batches)
            if (node is JsonObject batch && batch["Requests"] is JsonArray requests)
            {
                if (batch["Source"] is JsonValue value && value.TryGetValue<int>(out int source) &&
                    source >= (int)CampaignEffectSource.PersonAppearance)
                    throw new InvalidDataException("Legacy campaign batch has a future appearance origin.");
                foreach (var entry in requests)
                    if (entry is JsonObject request) request["PersonAppearance"] = null;
            }
    }

    private static void MigrateOfficerGrowthFields(JsonObject state, int version)
    {
        if (version >= 129 || state["CampaignEffectRequests"] is not JsonObject owner ||
            owner["PendingBatches"] is not JsonArray batches) return;
        foreach (var batch in batches)
            if (batch is JsonObject row && row["Requests"] is JsonArray requests)
            {
                if (row["Source"] is JsonValue value && value.TryGetValue<int>(out int source) &&
                    source >= (int)CampaignEffectSource.OfficerGrowthNotifications)
                    throw new InvalidDataException("Legacy campaign batch has a future growth notification origin.");
                foreach (var request in requests)
                    if (request is JsonObject fields) fields["OfficerGrowthFeedback"] = null;
            }
    }

    private static void MigrateTerrainDamageFields(JsonObject state, int version)
    {
        if (version >= 128 || state["CampaignEffectRequests"] is not JsonObject owner ||
            owner["PendingBatches"] is not JsonArray batches) return;
        foreach (var batch in batches)
            if (batch is JsonObject row && row["Requests"] is JsonArray requests)
            {
                if (row["Source"] is JsonValue value && value.TryGetValue<int>(out int source) &&
                    source >= (int)CampaignEffectSource.TerrainDamage)
                    throw new InvalidDataException("Legacy campaign batch has a future terrain damage origin.");
                foreach (var request in requests)
                    if (request is JsonObject fields) fields["TerrainDamagePresentation"] = null;
            }
    }

    private static void MigrateFireDamageFields(JsonObject state, int version)
    {
        if (version >= 127 || state["CampaignEffectRequests"] is not JsonObject owner ||
            owner["PendingBatches"] is not JsonArray batches) return;
        foreach (var batch in batches)
            if (batch is JsonObject row && row["Requests"] is JsonArray requests)
            {
                if (row["Source"] is JsonValue value && value.TryGetValue<int>(out int source) &&
                    source >= (int)CampaignEffectSource.FireDamage)
                    throw new InvalidDataException("Legacy campaign batch has a future fire damage origin.");
                foreach (var request in requests)
                    if (request is JsonObject fields) fields["FireDamagePresentation"] = null;
            }
    }

    private static void MigrateTrapFields(JsonObject state, int version)
    {
        if (version >= 126 || state["CampaignEffectRequests"] is not JsonObject owner ||
            owner["PendingBatches"] is not JsonArray batches) return;
        foreach (var batch in batches)
            if (batch is JsonObject row && row["Requests"] is JsonArray requests)
            {
                if (row["Source"] is JsonValue value && value.TryGetValue<int>(out int source) &&
                    source >= (int)CampaignEffectSource.FacilityTraps)
                    throw new InvalidDataException("Legacy campaign batch has a future trap/defeat origin.");
                foreach (var request in requests)
                    if (request is JsonObject fields)
                    {
                        if (fields["Speech"] is JsonObject speech && speech["Event"] is JsonValue eventValue &&
                            eventValue.TryGetValue<int>(out int kind) && kind >= (int)CombatSpeechEvent.TrapPitfall)
                            throw new InvalidDataException("Legacy campaign speech has a future trap event.");
                        if (fields["Speech"] is JsonObject legacySpeech) legacySpeech["TrapDefinitionId"] = null;
                        fields["FacilityTrapPresentation"] = null;
                        fields["ArmyTrapPresentation"] = null;
                        fields["ArmyFacilityDamagePresentation"] = null;
                        fields["FacilityTrapReport"] = null;
                        fields["ArmyMoraleDefeatReport"] = null;
                    }
            }
    }

    private static void MigrateTraitFields(JsonObject state, int version)
    {
        if (version >= 125 || state["CampaignEffectRequests"] is not JsonObject owner ||
            owner["PendingBatches"] is not JsonArray batches) return;
        foreach (var batch in batches)
            if (batch is JsonObject row && row["Requests"] is JsonArray requests)
            {
                if (row["Source"] is JsonValue value && value.TryGetValue<int>(out int source) &&
                    source >= (int)CampaignEffectSource.ArmyTraitMaintenance)
                    throw new InvalidDataException("Legacy campaign batch has a future Army trait origin.");
                foreach (var request in requests)
                    if (request is JsonObject fields)
                    { fields["ArmyTraitViewRefresh"] = null; fields["ArmyTraitFeedback"] = null; }
            }
    }

    private static void MigrateConstructionFields(JsonObject state, int version)
    {
        if (version >= 123 || state["CampaignEffectRequests"] is not JsonObject owner ||
            owner["PendingBatches"] is not JsonArray batches) return;
        foreach (var batch in batches)
            if (batch is JsonObject row && row["Requests"] is JsonArray requests)
            {
                if (row["Source"] is JsonValue sourceValue && sourceValue.TryGetValue<int>(out int source) &&
                    source >= (int)CampaignEffectSource.ArmyFacilityConstruction)
                    throw new InvalidDataException("Legacy campaign batch has a future construction origin.");
                foreach (var request in requests)
                    if (request is JsonObject fields)
                    {
                        fields["FacilityConstructionPresentation"] = null;
                        if (fields["Report"]?["Event"] is JsonValue eventValue && eventValue.TryGetValue<int>(out int report) &&
                            report >= (int)CampaignReportEvent.PlayerFacilityConstructionStarted)
                            throw new InvalidDataException("Legacy campaign report has a future construction event.");
                        if (fields["Speech"]?["Event"] is JsonValue speechValue && speechValue.TryGetValue<int>(out int speech) &&
                            speech >= (int)CombatSpeechEvent.ArmyFacilityConstructionStarted)
                            throw new InvalidDataException("Legacy campaign speech has a future construction event.");
                    }
            }
    }

    private static void MigrateRepairFields(JsonObject state, int version)
    {
        if (version >= 122 || state["CampaignEffectRequests"] is not JsonObject owner ||
            owner["PendingBatches"] is not JsonArray batches) return;
        foreach (var batch in batches)
            if (batch is JsonObject row && row["Requests"] is JsonArray requests)
            {
                if (row["Source"] is JsonValue sourceValue && sourceValue.TryGetValue<int>(out int source) &&
                    source >= (int)CampaignEffectSource.ArmyFacilityRepair)
                    throw new InvalidDataException("Legacy campaign batch has a future repair origin.");
                foreach (var request in requests)
                    if (request is JsonObject fields)
                    {
                        fields["FacilityRepairPresentation"] = null;
                        if (fields["Report"]?["Event"] is JsonValue eventValue && eventValue.TryGetValue<int>(out int id) &&
                            id >= (int)CampaignReportEvent.PlayerFacilityConstructionCompleted)
                            throw new InvalidDataException("Legacy campaign report has a future repair event.");
                        if (fields["Speech"]?["Event"] is JsonValue speechValue && speechValue.TryGetValue<int>(out int speech) &&
                            speech >= (int)CombatSpeechEvent.ArmyFacilityCompleted)
                            throw new InvalidDataException("Legacy campaign speech has a future repair event.");
                    }
            }
    }

    private static void MigrateFacilityFields(JsonObject state, int version)
    {
        if (version >= 121 || state["CampaignEffectRequests"] is not JsonObject owner ||
            owner["PendingBatches"] is not JsonArray batches) return;
        foreach (var batch in batches)
            if (batch is JsonObject row && row["Requests"] is JsonArray requests)
                foreach (var request in requests)
                    if (request is JsonObject fields)
                    {
                        fields["FacilityAuraReset"] = null;
                        fields["MilitaryMusicPulse"] = null;
                    }
    }

    private static void MigrateObjectAndAssociationFields(JsonObject state, int version)
    {
        if (version >= 120 || state["CampaignEffectRequests"] is not JsonObject owner ||
            owner["PendingBatches"] is not JsonArray batches) return;
        foreach (var batch in batches)
            if (batch is JsonObject row && row["Requests"] is JsonArray requests)
                foreach (var request in requests)
                    if (request is JsonObject fields)
                    {
                        // Old payloads cannot acquire a new semantic request or
                        // source-time name by carrying unexpected future fields.
                        fields["MapObjectPresentation"] = null;
                        fields["ForceAssociationRemovalReport"] = null;
                        if (fields["Report"] is JsonObject report)
                        {
                            report["AreaId"] = null; report["CommandChoiceId"] = null;
                            report["RulerNameSnapshot"] = null;
                        }
                    }
    }

    internal static void Check(JsonObject state)
    {
        if (state["CampaignEffectRequests"] is null) return;
        var owner = Require(state["CampaignEffectRequests"], "LastBatchSequence", "PendingBatches");
        if (owner["PendingBatches"] is not JsonArray batches) throw new InvalidDataException("Saved campaign batches are missing.");
        foreach (var node in batches)
        {
            var batch = Require(node, "Sequence", "Source", "SourceRevision", "Requests");
            if (batch["Requests"] is not JsonArray requests) throw new InvalidDataException("Saved campaign requests are missing.");
            foreach (var entry in requests)
            {
                var request = Require(entry, "HistoryNotification", "Speech", "Report", "MapObjectPresentation", "ForceAssociationRemovalReport",
                    "FacilityAuraReset", "MilitaryMusicPulse", "FacilityRepairPresentation", "FacilityConstructionPresentation",
                    "ArmyTraitViewRefresh", "ArmyTraitFeedback", "FacilityTrapPresentation", "ArmyTrapPresentation",
                    "ArmyFacilityDamagePresentation", "FacilityTrapReport", "ArmyMoraleDefeatReport", "FireDamagePresentation", "TerrainDamagePresentation", "OfficerGrowthFeedback", "PersonAppearance", "PersonDeathReport", "PersonLifespanNotification", "ForcePolicyLevelUp");
                if (request["ForcePolicyLevelUp"] is JsonNode levelUp)
                    _ = Require(levelUp, "ForceId", "PreviousLevel", "CurrentLevel", "AdditionalPolicySlots", "AdditionalCostLimit",
                        "AnnouncementTextAtSource", "ConfirmationTextAtSource", "Stage");
                if (request["PersonLifespanNotification"] is JsonNode lifespan)
                    _ = RequireExact(lifespan, "Operation", "EventKind", "PersonId", "TextAtSource");
                if (request["PersonDeathReport"] is JsonNode death)
                    _ = RequireExact(death, "Operation", "EventKind", "SpeakerId", "TextAtSource");
                if (request["PersonAppearance"] is JsonNode appearance)
                {
                    var fields = RequireExact(appearance, "Operation", "SoundId", "EventKind", "Announcement", "Speaker", "History");
                    if (fields["Speaker"] is JsonNode speaker) _ = RequireExact(speaker, "PersonId", "Text");
                    if (fields["History"] is JsonNode history)
                        _ = RequireExact(history, "PersonId", "DistrictArgument", "HexArgument", "CampaignDay", "Text");
                }
                if (request["OfficerGrowthFeedback"] is JsonNode officerGrowth)
                {
                    var fields = Require(officerGrowth, "Operation", "Narration", "History");
                    if (fields["Narration"] is JsonNode narration) _ = Require(narration, "OfficerId", "Text");
                    if (fields["History"] is JsonNode history)
                        _ = Require(history, "OfficerId", "RawDistrictArgument", "HexArgument", "CampaignDay", "Text");
                }
                if (request["TerrainDamagePresentation"] is JsonNode terrainDamage)
                    _ = Require(terrainDamage, "Operation", "ArmyId", "RequestedAmount");
                if (request["FireDamagePresentation"] is JsonNode fireDamage)
                {
                    var fields = Require(fireDamage, "Operation", "ArmyId", "BaseTarget", "GrossAmount");
                    if (fields["BaseTarget"] is JsonNode target) _ = Require(target, "Kind", "Id");
                }
                if (request["FacilityTrapPresentation"] is JsonNode trapVisual)
                {
                    var fields = Require(trapVisual, "Operation", "ReceiverId", "DefinitionId", "HexId", "Range");
                    _ = Require(fields["ReceiverId"], "Value");
                }
                if (request["ArmyTrapPresentation"] is JsonNode trapTarget)
                {
                    var fields = Require(trapTarget, "Operation", "ArmyId", "HexId", "ReceiverId", "DefinitionId");
                    _ = Require(fields["ReceiverId"], "Value");
                }
                if (request["ArmyFacilityDamagePresentation"] is JsonNode trapDamage)
                    _ = Require(trapDamage, "Operation", "ArmyId", "HexId", "GrossAmount", "DisplayModifier");
                if (request["FacilityTrapReport"] is JsonNode trapReport)
                {
                    var fields = Require(trapReport, "Operation", "ReceiverId", "DefinitionId", "HexId", "CampaignDay", "DistrictId",
                        "TrapName", "ForceId", "CounterForceName", "ArmyId", "OfficerId", "ArmyName", "TraitId", "TraitName",
                        "AbnormalIndex", "PositiveContribution", "AreaArgument0", "AreaNameAtSource");
                    _ = Require(fields["ReceiverId"], "Value");
                    if (fields["CounterForceName"] is JsonNode forceName)
                        _ = Require(forceName, "ForceArgument0", "Form", "CustomKingdomIndex", "PersonNameOperandAtSource");
                }
                if (request["ArmyMoraleDefeatReport"] is JsonNode moraleDefeat)
                    _ = Require(moraleDefeat, "ArmyId", "OfficerId", "ArmyNameSnapshot", "DistrictId", "ReportHex", "CampaignDay");
                if (request["ArmyTraitViewRefresh"] is JsonNode traitRefresh) _ = Require(traitRefresh, "ArmyId");
                if (request["ArmyTraitFeedback"] is JsonNode traitFeedback)
                    _ = Require(traitFeedback, "ArmyId", "OfficerId", "SpeakerHexId", "TraitId", "ContextHexId");
                if (request["FacilityConstructionPresentation"] is JsonNode construction)
                {
                    var fields = Require(construction, "Operation", "ReceiverId", "HexId", "DefinitionKey", "ForceSlot");
                    _ = Require(fields["ReceiverId"], "Value");
                }
                if (request["FacilityRepairPresentation"] is JsonNode repair)
                {
                    var fields = Require(repair, "Operation", "ReceiverId", "HexId", "DefinitionKey", "CommanderId");
                    _ = Require(fields["ReceiverId"], "Value");
                }
                if (request["FacilityAuraReset"] is JsonNode reset) _ = Require(reset);
                if (request["MilitaryMusicPulse"] is JsonNode pulse)
                {
                    var fields = Require(pulse, "FacilityReceiverId", "ArmyId", "HexId");
                    _ = Require(fields["FacilityReceiverId"], "Value");
                }
                if (request["FacilityProjectile"] is JsonNode projectile)
                {
                    var fields = Require(projectile, "FacilityReceiverId", "TargetReceiverId", "SourceHexOperand", "TargetHexOperand");
                    _ = Require(fields["FacilityReceiverId"], "Value");
                    _ = Require(fields["TargetReceiverId"], "Value");
                }
                if (request["Speech"] is JsonNode speech)
                {
                    var fields = Require(speech, "SpeakerKind", "SpeakerId", "OfficerId", "Event", "HexId", "TargetKind", "TargetId",
                        "RelatedOfficerId", "AdmissionForce", "AdmissionColor", "GarrisonChoice", "TrapDefinitionId");
                    if (fields["GarrisonChoice"] is JsonNode choice) _ = Require(choice, "DamageTier", "Troops", "FormationContact");
                    //Older speech has no such optional field. The new event
                    //must retain it; deserialization and semantic validation
                    //then check its type and the supported actual definition.
                    if (fields["Event"] is JsonValue eventValue && eventValue.TryGetValue<int>(out int eventKind) &&
                        eventKind == (int)CombatSpeechEvent.ArmyUnderFacilityAttack)
                        _ = Require(speech, "FacilityDefinitionId");
                }
                if (request["Report"] is JsonNode report)
                    _ = Require(report, "Event", "DistrictId", "HexId", "CampaignDay", "OfficerId", "ArmyName", "TargetName",
                        "TargetKind", "TargetId", "ForceSlot", "ForceColor", "KingdomNameIndex", "RulerId",
                        "AreaId", "CommandChoiceId", "RulerNameSnapshot");
                if (request["MapObjectPresentation"] is JsonNode map)
                {
                    var fields = Require(map, "Event", "ReceiverId", "HexId", "DefinitionKey");
                    _ = Require(fields["ReceiverId"], "Value");
                }
                if (request["ForceAssociationRemovalReport"] is JsonNode association)
                    _ = Require(association, "OldForceId", "OfficerId", "AssociationSlot", "TargetForceId", "DistrictId",
                        "LocationId", "HexId", "CampaignDay", "OfficerDisplayName", "TargetForceDisplayName");
            }
        }
    }
    private static JsonObject RequireExact(JsonNode? node, params string[] fields) => node is JsonObject value &&
        value.Count == fields.Length && fields.All(value.ContainsKey) ? value :
        throw new InvalidDataException("Saved appearance fields are missing or unexpected.");

    private static JsonObject Require(JsonNode? node, params string[] fields) => node is JsonObject value && fields.All(value.ContainsKey)
        ? value : throw new InvalidDataException("Saved campaign effect fields are incomplete.");
}
