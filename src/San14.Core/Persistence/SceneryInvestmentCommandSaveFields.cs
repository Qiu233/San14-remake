using System.Text.Json.Nodes;

namespace San14.Core;

internal static class SceneryInvestmentCommandSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version < 183 && state["CampaignEffectRequests"]?["PendingBatches"] is JsonArray batches &&
            batches.OfType<JsonObject>().Any(batch => batch["Source"] is JsonValue value &&
                value.TryGetValue<int>(out int source) && source == (int)CampaignEffectSource.SceneryInvestmentCommands))
            throw new InvalidDataException("Legacy world contains future scenery investment command presentation.");
    }
}
