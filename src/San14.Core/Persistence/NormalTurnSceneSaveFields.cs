using System.Text.Json.Nodes;

namespace San14.Core;

internal static class NormalTurnSceneSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version >= 165) return;
        if (state["ActiveDisasterScene"] is not null)
            throw new InvalidDataException("Legacy save contains a future admitted disaster scene.");
        if (state["CampaignEffectRequests"]?["PendingBatches"] is not JsonArray batches) return;
        foreach (var batch in batches.OfType<JsonObject>())
            if (batch["Requests"] is JsonArray requests)
                foreach (var request in requests.OfType<JsonObject>())
                    if (request["ForceEmergenceScene"] is not null || request["ForceCoalitionVisual"]?["SceneKind"] is not null)
                        throw new InvalidDataException("Legacy request contains a future normal Turn scene.");
    }

    internal static void Migrate(JsonObject state, int version)
    {
        if (version < 165) state["ActiveDisasterScene"] = null;
        else if (!state.ContainsKey("ActiveDisasterScene") || state["ActiveDisasterScene"] is JsonNode active &&
            (active is not JsonObject admittedScene || admittedScene["EventKind"] is not JsonValue admittedKindValue ||
             !admittedKindValue.TryGetValue<int>(out int admittedSceneKind) || admittedSceneKind is not (14 or 15 or 16)))
            throw new InvalidDataException("Saved admitted disaster scene is missing or invalid.");
        if (state["CampaignEffectRequests"]?["PendingBatches"] is not JsonArray batches) return;
        foreach (var batch in batches.OfType<JsonObject>())
        {
            if (batch["Requests"] is not JsonArray requests) continue;
            foreach (var request in requests.OfType<JsonObject>())
            {
                if (version < 165)
                {
                    request["ForceEmergenceScene"] = null;
                    if (request["ForceCoalitionVisual"] is JsonObject visual) visual["SceneKind"] = null;
                    continue;
                }
                if (!request.ContainsKey("ForceEmergenceScene"))
                    throw new InvalidDataException("Saved force emergence scene knowledge is missing.");
                if (request["ForceEmergenceScene"] is JsonNode saved &&
                    (saved is not JsonObject scene || !Integer(scene["ForceId"], 0, 51) ||
                     !Integer(scene["RulerOfficerId"], 0, 6000) || !Integer(scene["HomeLocationId"], 0, 61) ||
                     !Integer(scene["SourceDay"], int.MinValue, int.MaxValue)))
                    throw new InvalidDataException("Saved force emergence scene is incomplete.");
                if (request["ForceCoalitionVisual"] is JsonObject current &&
                    (!current.ContainsKey("SceneKind") || current["SceneKind"] is JsonNode kind && !Integer(kind, 0, int.MaxValue)))
                    throw new InvalidDataException("Saved force narration scene knowledge is missing or invalid.");
            }
        }
    }

    private static bool Integer(JsonNode? field, int minimum, int maximum) => field is JsonValue value &&
        value.TryGetValue<int>(out int number) && number >= minimum && number <= maximum;
}
