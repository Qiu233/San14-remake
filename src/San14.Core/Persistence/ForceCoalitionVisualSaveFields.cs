using System.Text.Json.Nodes;

namespace San14.Core;

internal static class ForceCoalitionVisualSaveFields
{
    internal static void Migrate(JsonObject state, int version)
    {
        if (state["CampaignEffectRequests"]?["PendingBatches"] is not JsonArray batches) return;
        foreach (var batch in batches.OfType<JsonObject>())
        {
            if (batch["Requests"] is not JsonArray requests) continue;
            foreach (var request in requests.OfType<JsonObject>())
            {
                if (version < 164)
                {
                    if (request["ForceCoalitionVisual"] is not null)
                        throw new InvalidDataException("Legacy request contains a future coalition presentation.");
                    request["ForceCoalitionVisual"] = null;
                    continue;
                }
                if (!request.ContainsKey("ForceCoalitionVisual"))
                    throw new InvalidDataException("Saved coalition presentation knowledge is missing.");
                if (request["ForceCoalitionVisual"] is not JsonNode saved) continue;
                if (saved is not JsonObject visual || visual["Operation"] is not JsonValue operation ||
                    !operation.TryGetValue<int>(out int kind) || !Enum.IsDefined((ForceCoalitionVisualOperation)kind) ||
                    !visual.ContainsKey("TargetHexId") || visual["TargetHexId"] is JsonNode hex &&
                    (hex is not JsonValue value || !value.TryGetValue<int>(out int id) || id is < 0 or > 48400))
                    throw new InvalidDataException("Saved coalition presentation is invalid.");
            }
        }
    }
}
