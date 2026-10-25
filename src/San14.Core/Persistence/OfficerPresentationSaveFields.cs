using System.Text.Json.Nodes;

namespace San14.Core;

internal static class OfficerPresentationSaveFields
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
                if (version < 163)
                {
                    if (talent["Speaker"] is not null)
                        throw new InvalidDataException("Legacy narration contains a future source-time speaker.");
                    talent["Speaker"] = null;
                    continue;
                }
                if (!talent.ContainsKey("Speaker"))
                    throw new InvalidDataException("Saved narration source-time speaker knowledge is missing.");
                if (talent["Speaker"] is not JsonNode saved) continue;
                if (saved is not JsonObject speaker || speaker["OfficerId"] is not JsonValue idValue ||
                    !idValue.TryGetValue<int>(out int id) || id is < 1 or > 6000 ||
                    speaker["NameAtSource"] is not JsonValue nameValue || !nameValue.TryGetValue<string>(out _) ||
                    !speaker.ContainsKey("PortraitAppearanceIndex") || speaker["PortraitAppearanceIndex"] is JsonNode appearance &&
                    (appearance is not JsonValue appearanceValue || !appearanceValue.TryGetValue<ushort>(out _)))
                    throw new InvalidDataException("Saved narration source-time speaker is invalid.");
            }
        }
    }
}
