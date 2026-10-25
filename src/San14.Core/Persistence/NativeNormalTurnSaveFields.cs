using System.Text.Json.Nodes;

namespace San14.Core;

internal static class NativeNormalTurnSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version < 165 && state["NativeNormalTurnContinuation"] is not null)
            throw new InvalidDataException("Legacy save contains a future normal Turn continuation.");
        RejectFuturePresentation(state, version);
    }

    internal static void Migrate(JsonObject state, int version)
    {
        const string key = "NativeNormalTurnContinuation";
        if (version < 165)
        {
            state[key] = null;
            return;
        }
        if (!state.ContainsKey(key))
            throw new InvalidDataException("Saved normal Turn continuation knowledge is missing.");
        if (state[key] is not JsonNode saved) return;
        if (saved is not JsonObject turn || !SupportedAction(turn["NextAction"], version) ||
            turn["RestoreAppRandomState"] is not JsonValue random || !random.TryGetValue<uint>(out _) ||
            turn["RemainingOfficerIds"] is not JsonArray remaining || remaining.Any(person => !Integer(person, 0, 6000)) ||
            !turn.ContainsKey("PendingSelfRecommendation") ||
            turn["Grouping"] is not JsonObject grouping || grouping["Roots"] is not JsonArray roots || roots.Count != 1503 ||
            roots.Any(root => root is not null && (root is not JsonValue value || !value.TryGetValue<ushort>(out _))) ||
            grouping["Changed"] is not JsonValue changed || !changed.TryGetValue<bool>(out _))
            throw new InvalidDataException("Saved normal Turn continuation is incomplete or invalid.");
        if (turn["PendingSelfRecommendation"] is JsonNode offer &&
            (offer is not JsonObject request || !Integer(request, "OfficerId", 0, 6000) ||
             !Integer(request, "ForceId", 0, 51) || !Integer(request, "DistrictId", 0, 51) ||
             !Integer(request, "RulerOfficerId", 0, 6000) || request["TextAtSource"] is not JsonValue text ||
             !text.TryGetValue<string>(out _)))
            throw new InvalidDataException("Saved self recommendation is incomplete or invalid.");
    }

    private static void RejectFuturePresentation(JsonObject state, int version)
    {
        if (version >= 165 || state["CampaignEffectRequests"]?["PendingBatches"] is not JsonArray batches) return;
        foreach (var batch in batches.OfType<JsonObject>())
        {
            if (batch["Requests"] is not JsonArray requests) continue;
            foreach (var request in requests.OfType<JsonObject>())
            {
                if (request["MonthlyMaintenance"]?["Operation"] is JsonNode operation &&
                    Integer(operation, (int)MonthlyMaintenanceOperation.PersonnelSceneEnter, int.MaxValue))
                    throw new InvalidDataException("Legacy monthly request contains a future personnel scene.");
                if (batch["Source"] is not JsonValue source || !source.TryGetValue<int>(out int kind) ||
                    kind != (int)CampaignEffectSource.MonthlyMaintenance) continue;
                if (request["ForceIndependenceScene"] is not null ||
                    request["TalentCommandPresentation"] is JsonObject talent &&
                    (talent["Speaker"] is not null || Integer(talent["SoundId"], 21, 21) ||
                     Integer(talent["Operation"], (int)TalentCommandPresentationOperation.ConditionalOfficerText, int.MaxValue)))
                    throw new InvalidDataException("Legacy monthly request contains a future source-time personnel presentation.");
            }
        }
    }

    private static bool Integer(JsonObject owner, string key, int min, int max) => Integer(owner[key], min, max);
    private static bool SupportedAction(JsonNode? node, int version) => node is JsonValue field &&
        field.TryGetValue<int>(out int action) &&
        (action == 16 || version >= 175 && action is (3 or 7 or 8) || version >= 166 && action is (25 or 26) || version >= 167 && action is (48 or 49));
    private static bool Integer(JsonNode? value, int min, int max) => value is JsonValue field &&
        field.TryGetValue<int>(out int result) && result >= min && result <= max;
}
