using System.Text.Json.Nodes;

namespace San14.Core;

internal static class ForceIndependenceSceneSaveFields
{
    internal static void Migrate(JsonObject state, int version)
    {
        if (state["CampaignEffectRequests"]?["PendingBatches"] is not JsonArray batches) return;
        foreach (var batch in batches.OfType<JsonObject>())
        {
            if (batch["Requests"] is not JsonArray requests) continue;
            foreach (var request in requests.OfType<JsonObject>())
            {
                const string key = "ForceIndependenceScene";
                if (version < 164)
                {
                    if (request[key] is not null)
                        throw new InvalidDataException("Legacy request contains a future independence scene.");
                    request[key] = null;
                    continue;
                }
                if (!request.ContainsKey(key))
                    throw new InvalidDataException("Saved independence scene knowledge is missing.");
                if (request[key] is not JsonNode saved) continue;
                if (saved is not JsonObject scene || !Integer(scene, "OriginalForceId", 0, 51) ||
                    !Integer(scene, "RulerOfficerId", 0, 6000) || !Integer(scene, "HomeLocationId", 0, 61) ||
                    !Integer(scene, "SourceDay", int.MinValue, int.MaxValue))
                    throw new InvalidDataException("Saved independence scene is invalid.");
            }
        }
    }

    private static bool Integer(JsonObject owner, string key, int minimum, int maximum) =>
        owner[key] is JsonValue field && field.TryGetValue<int>(out int value) && value >= minimum && value <= maximum;
}
