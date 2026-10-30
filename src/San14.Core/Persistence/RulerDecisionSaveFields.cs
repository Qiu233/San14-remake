using System.Text.Json.Nodes;

namespace San14.Core;

internal static class RulerDecisionSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version >= 175) return;
        if (state["ArmyDefeatContinuation"] is not null || state["PersonDeathContinuation"] is not null ||
            state["NativeNormalTurnContinuation"]?["RemainingDeathPersonPhysicalSlots"] is not null ||
            state["NativePeriodContinuation"]?["Cursor"]?["StageIndex"] is JsonValue stage &&
            stage.TryGetValue<int>(out int index) && index == 25 ||
            Effects(state).Any(request => request["MapMusicIntent"] is not null || request["ApplicationExit"] is not null) || HasLegacyCaptureVoice(state) || HasLegacyMonthlyCapture(state))
            throw new InvalidDataException("Legacy save contains future ruler decision or terminal presentation state.");
    }

    internal static void Migrate(JsonObject state, int version)
    {
        if (version < 175)
        {
            state["ArmyDefeatContinuation"] = null;
            state["PersonDeathContinuation"] = null;
            if (state["NativeNormalTurnContinuation"] is JsonObject oldTurn)
                oldTurn["RemainingDeathPersonPhysicalSlots"] = null;
            foreach (var request in Effects(state))
            {
                request["MapMusicIntent"] = null;
                request["ApplicationExit"] = null;
            }
            return;
        }
        MigrateArmyDefeatHome(state, version);
        Nullable(state, "ArmyDefeatContinuation", Defeat);
        Nullable(state, "PersonDeathContinuation", Death);
        if (state["NativeNormalTurnContinuation"] is JsonObject turn)
        {
            Nullable(turn, "RemainingDeathPersonPhysicalSlots", node => node is JsonArray people &&
                people.All(person => Integer(person, 0, 6000)));
            bool deathAction = Integer(turn["NextAction"], 3, 3) || Integer(turn["NextAction"], 7, 8);
            if (deathAction != (turn["RemainingDeathPersonPhysicalSlots"] is not null)) Invalid();
        }
        foreach (var request in Effects(state))
        {
            Nullable(request, "MapMusicIntent", node => node is JsonObject music &&
                Long(music, "CalendarCycleOrdinal") && Integer(music["SourceSlot"], 0, int.MaxValue) &&
                Bool(music, "UseStoredPlaybackPosition") && Integer(music["FadeInMilliseconds"], 0, int.MaxValue) &&
                Integer(music["FadeOutMilliseconds"], 0, int.MaxValue));
            Nullable(request, "ApplicationExit", node => node is JsonObject exit &&
                Integer(exit["Operation"], (int)ApplicationExitOperation.ReturnToTitle,
                    (int)ApplicationExitOperation.ResetAudioSpecialMode));
        }
    }

    //The previous216 producer captured only an ordinary Force and City Home.
    //No post-callback Person is consulted when migrating that captured owner.
    internal static void MigrateArmyDefeatHome(JsonObject state, int version)
    {
        if (version >= 217 || state["ArmyDefeatContinuation"] is not JsonObject savedDefeat) return;
        if (!Integer(savedDefeat["HomeCityId"], 1, 51) || !Integer(savedDefeat["VictimForceId"], 1, 45) ||
            !Integer(savedDefeat["ResumePoint"], 0, 1) || savedDefeat.ContainsKey("VictimHome") ||
            savedDefeat.ContainsKey("SpecialCommanderReclaim") || savedDefeat.ContainsKey("ReclaimDefeatedCommander"))
            Invalid();
        savedDefeat["VictimHome"] = new JsonObject
            { ["Kind"] = (int)SettlementBaseKind.City, ["Id"] = savedDefeat["HomeCityId"]!.DeepClone() };
        savedDefeat["SpecialCommanderReclaim"] = false;
        savedDefeat["ReclaimDefeatedCommander"] = Integer(savedDefeat["ResumePoint"],
            (int)ArmyDefeatResumePoint.AfterPersonDeath, (int)ArmyDefeatResumePoint.AfterPersonDeath)
            ? null : JsonValue.Create(false);
    }

    private static bool Defeat(JsonNode node)
    {
        if (node is not JsonObject value ||
            !Ints(value, "SettlementIndex", "ArmyPhysicalSlot", "VictimForceId", "CommanderOfficerId", "HomeCityId", "HexId", "CaptureImmunity") ||
            value["ReceiverId"] is not JsonObject receiver || !Ints(receiver, "Value") ||
            value["Defeat"] is not JsonObject defeat || !Ints(defeat, "VictimUnitId", "Cause") ||
            !NullableInt(defeat, "WinnerUnitId") || !NullableShape(defeat, "WinnerBase", Base) ||
            !NullableInt(value, "WinnerArmyPhysicalSlot") || !NullableInt(value, "WinnerOfficerId") ||
            !NullableShape(value, "WinnerBase", Base) || !NullableInt(value, "WinnerForceId") ||
            !NullableInt(value, "SampleDay") || !NullableInt(value, "SampleSalt") ||
            !Integer(value["ResumePoint"], 0, 1) || !Bool(value, "DeathReturnedEarly") ||
            !Bool(value, "SpecialCommanderReclaim") || !DefeatHomeContext(value) ||
            !NullableShape(value, "ReclaimDefeatedCommander", node => node is JsonValue b && b.TryGetValue<bool>(out _)) ||
            (Integer(value["ResumePoint"], (int)ArmyDefeatResumePoint.AfterPersonDeath, (int)ArmyDefeatResumePoint.AfterPersonDeath)
                ? value["ReclaimDefeatedCommander"] is not null : value["ReclaimDefeatedCommander"] is null) ||
            IsTrue(value["ReclaimDefeatedCommander"]) &&
                (!IsTrue(value["SpecialCommanderReclaim"]) || Integer(defeat["Cause"], (int)UnitDefeatCause.DuelDeath, (int)UnitDefeatCause.DuelDeath)) ||
            IsTrue(value["SpecialCommanderReclaim"]) && !Integer(value["VictimForceId"], 46, 51) ||
            Integer(value["VictimForceId"], 51, 51) && !IsTrue(value["SpecialCommanderReclaim"]) ||
            !NullableShape(value, "ChosenDisposition", node => Integer(node, 0, 1)) ||
            !NullableShape(value, "PendingCapturedRulerTreatment", Captive)) return false;
        return true;
    }

    private static bool DefeatHomeContext(JsonObject value) => value.ContainsKey("VictimHome") &&
        (value["VictimHome"] is null ? IsTrue(value["SpecialCommanderReclaim"]) && Integer(value["HomeCityId"], 0, 0) :
            DefeatHome(value["VictimHome"]) && Integer(value["HomeCityId"], 1, 51) &&
            (!Integer(value["VictimHome"]?["Kind"], (int)SettlementBaseKind.City, (int)SettlementBaseKind.City) ||
                value["VictimHome"]?["Id"]?.GetValue<int>() == value["HomeCityId"]?.GetValue<int>()));
    private static bool IsTrue(JsonNode? node) => node is JsonValue value && value.TryGetValue<bool>(out bool result) && result;
    private static bool DefeatHome(JsonNode? node) => node is JsonObject value &&
        (Integer(value["Kind"], (int)SettlementBaseKind.City, (int)SettlementBaseKind.City) &&
            Integer(value["Id"], 1, 51) ||
         Integer(value["Kind"], (int)SettlementBaseKind.Gate, (int)SettlementBaseKind.Gate) &&
            Integer(value["Id"], 52, 61));

    private static bool Captive(JsonNode node) => node is JsonObject value &&
        Ints(value, "OfficerId", "CapturingForceId", "CapturingRulerId") &&
        Bool(value, "CanExecute") && Text(value, "TextAtSource");

    private static bool Death(JsonNode node) => node is JsonObject value &&
        Ints(value, "OfficerId", "PersonPhysical", "Cause", "OriginalOffice", "OriginalMeritStage") &&
        Integer(value["OriginalStatus"], 0, byte.MaxValue) && NullableInt(value, "KillerPhysical") &&
        Bool(value, "RetireAssociatedArmy") && Bool(value, "EmitReports") && Bool(value, "EmitHistory") &&
        Bool(value, "SuppressReports") && NullableShape(value, "OriginalBase", DeathBase) &&
        NullableShape(value, "OriginalCity", DeathBase) && NullableInt(value, "OriginalHexPhysical") &&
        NullableInt(value, "OriginalForcePhysical") && NullableInt(value, "OriginalSwornPersonPhysical") &&
        NullableInt(value, "AssociatedCommandId") && value["PendingSuccession"] is JsonObject request &&
        Ints(request, "ForceId", "DeceasedOfficerId") && request["CandidateOfficerIds"] is JsonArray candidates &&
        candidates.Count > 0 && candidates.All(candidate => Integer(candidate, 1, 6000)) &&
        Text(request, "PromptAtSource") && Text(request, "EmptyChoiceWarningAtSource");

    private static bool Base(JsonNode node) => node is JsonObject value && Integer(value["Kind"], 0, 1) && Ints(value, "Id");
    private static bool DeathBase(JsonNode node) => node is JsonObject value && Integer(value["Kind"], 0, 1) && NullableInt(value, "PhysicalSlot");
    private static bool Ints(JsonObject value, params string[] keys) => keys.All(key => Integer(value[key], int.MinValue, int.MaxValue));
    private static bool NullableInt(JsonObject value, string key) => NullableShape(value, key, node => Integer(node, int.MinValue, int.MaxValue));
    private static bool NullableShape(JsonObject value, string key, Func<JsonNode, bool> valid) =>
        value.ContainsKey(key) && (value[key] is not JsonNode node || valid(node));
    private static void Nullable(JsonObject value, string key, Func<JsonNode, bool> valid)
    {
        if (!NullableShape(value, key, valid)) Invalid();
    }
    private static bool Bool(JsonObject value, string key) => value[key] is JsonValue field && field.TryGetValue<bool>(out _);
    private static bool Text(JsonObject value, string key) => value[key] is JsonValue field && field.TryGetValue<string>(out _);
    private static bool Long(JsonObject value, string key) => value[key] is JsonValue field && field.TryGetValue<long>(out long number) && number >= 0;
    private static bool Integer(JsonNode? node, int min, int max) => node is JsonValue field && field.TryGetValue<int>(out int number) && number >= min && number <= max;
    private static void Invalid() => throw new InvalidDataException("Saved ruler decision continuation or terminal presentation is incomplete or invalid.");
    private static bool HasLegacyMonthlyCapture(JsonObject state)
    {
        if (state["CampaignEffectRequests"]?["PendingBatches"] is not JsonArray batches) return false;
        return batches.OfType<JsonObject>().Any(batch =>
            Integer(batch["Source"], (int)CampaignEffectSource.MonthlyMaintenance, (int)CampaignEffectSource.MonthlyMaintenance) &&
            batch["Requests"] is JsonArray requests && requests.OfType<JsonObject>().Any(request =>
                Integer(request["MapObjectPresentation"]?["Event"], (int)MapObjectPresentationEvent.BaseOwnershipLostVisual,
                    (int)MapObjectPresentationEvent.BaseOwnershipLostSound)));
    }
    private static bool HasLegacyCaptureVoice(JsonObject state)
    {
        if (state["CampaignEffectRequests"]?["PendingBatches"] is not JsonArray batches) return false;
        return batches.OfType<JsonObject>().Any(batch =>
            Integer(batch["Source"], (int)CampaignEffectSource.ArmyBattleDefeat, (int)CampaignEffectSource.ArmyBattleDefeat) &&
            batch["Requests"] is JsonArray requests && requests.OfType<JsonObject>().Any(request =>
                request["TalentCommandPresentation"]?["Voice"] is not null ||
                Integer(request["MonthlyMaintenance"]?["Operation"], (int)MonthlyMaintenanceOperation.EmperorSceneEnter,
                    (int)MonthlyMaintenanceOperation.EmperorSceneExit)));
    }
    private static IEnumerable<JsonObject> Effects(JsonObject state)
    {
        if (state["CampaignEffectRequests"]?["PendingBatches"] is not JsonArray batches) yield break;
        foreach (var batch in batches.OfType<JsonObject>())
            if (batch["Requests"] is JsonArray requests)
                foreach (var request in requests.OfType<JsonObject>()) yield return request;
    }
}
