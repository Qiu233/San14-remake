using System.Text.Json.Nodes;

namespace San14.Core;

internal static class PersonnelCreditEpisodeSaveFields
{
    internal static void Migrate(JsonObject state, int version)
    {
        if (state["CampaignEffectRequests"]?["PendingBatches"] is not JsonArray batches) return;
        foreach (var batch in batches.OfType<JsonObject>())
        {
            if (batch["Requests"] is not JsonArray requests) continue;
            foreach (var request in requests.OfType<JsonObject>())
            {
                if (request["TalentCommandPresentation"] is not JsonObject talent) continue;
                if (version < 226)
                {
                    if (talent["CreditEpisodeSlot"] is not null)
                        throw new InvalidDataException("Legacy narration contains a future personnel credit episode.");
                    //Earlier products rejected these episodes before publication.
                    //A legacy line therefore has no episode identity to infer.
                    talent["CreditEpisodeSlot"] = null;
                    continue;
                }
                if (!talent.ContainsKey("CreditEpisodeSlot") || talent["CreditEpisodeSlot"] is JsonNode episode &&
                    (episode is not JsonValue value || !value.TryGetValue<int>(out int slot) || slot is < 1 or > 30))
                    throw new InvalidDataException("Saved personnel credit episode knowledge is missing or invalid.");
            }
        }
    }
}
