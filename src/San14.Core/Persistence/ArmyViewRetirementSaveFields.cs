using System.Text.Json.Nodes;

namespace San14.Core;

/// <summary>The explicit semantic retirement payload starts at Main schema229.
/// Older saves retain their existing pending work; no past retirement is inferred.</summary>
internal static class ArmyViewRetirementSaveFields
{
    internal static void ValidateWorld(JsonObject state, int version)
    {
        if (state["CampaignEffectRequests"]?["PendingBatches"] is not JsonArray batches) return;
        foreach (var batch in batches.OfType<JsonObject>())
        {
            bool retirementSource = batch["Source"] is JsonValue source && source.TryGetValue<int>(out int value) &&
                value == (int)CampaignEffectSource.ArmyViewRetirement;
            if (retirementSource && version < 229)
                throw new InvalidDataException("Legacy save contains a future Army view retirement source.");
            if (batch["Requests"] is not JsonArray requests) continue;
            if (retirementSource && requests.Count == 0)
                throw new InvalidDataException("Army view retirement has no saved semantic request.");
            foreach (var request in requests.OfType<JsonObject>())
            {
                JsonNode? payload = request["ArmyViewRetirement"];
                if (payload is null && !retirementSource) continue;
                if (version < 229)
                    throw new InvalidDataException("Legacy save contains a future Army view retirement payload.");
                if (!retirementSource || payload is not JsonObject retirement || retirement.Count != 2 ||
                    retirement["ArmyId"] is not JsonValue army || !army.TryGetValue<int>(out int id) || id <= 0 ||
                    retirement["AnimateRetirement"] is not JsonValue animate || !animate.TryGetValue<bool>(out _))
                    throw new InvalidDataException("Army view retirement needs its actual identity and explicit animation knowledge.");
            }
        }
    }
}
