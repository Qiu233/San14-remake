using System.Text.Json.Nodes;

namespace San14.Core;

internal static class CampaignCrtRandomSaveFields
{
    internal static void Migrate(JsonObject state, int version)
    {
        if (version < 160)
        {
            if (state["CampaignCrtRandom"] is not null)
                throw new InvalidDataException("Legacy save contains a future campaign CRT random snapshot.");
            state["CampaignCrtRandom"] = null;
            return;
        }

        if (!state.ContainsKey("CampaignCrtRandom"))
            throw new InvalidDataException("Saved campaign CRT random knowledge is missing.");
        if (state["CampaignCrtRandom"] is JsonNode saved &&
            (saved is not JsonObject snapshot || snapshot.Count != 1 ||
             snapshot["State"] is not JsonValue bits || !bits.TryGetValue<uint>(out _)))
            throw new InvalidDataException("Saved campaign CRT random snapshot is invalid.");
    }
}
