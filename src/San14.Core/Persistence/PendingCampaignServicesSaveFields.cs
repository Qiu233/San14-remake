using System.Text.Json.Nodes;

namespace San14.Core;

internal static class PendingCampaignServicesSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version < 168 && state["PendingCampaignServices"] is not null)
            throw new InvalidDataException("Legacy world contains a future pending service outbox.");
    }

    internal static void Migrate(JsonObject state, int version)
    {
        const string key = "PendingCampaignServices";
        var story = state["NativeNormalTurnContinuation"]?["StoryPlayback"] as JsonObject;
        if (version >= 168 && story?["ServiceChanges"] is not null)
            throw new InvalidDataException("Saved story duplicates the campaign-service outbox.");
        if (version < 168)
            state[key] = story?["ServiceChanges"]?.DeepClone() ?? new JsonObject
                { ["Events"] = new JsonArray(), ["Achievements"] = new JsonArray(), ["ClockReads"] = new JsonArray(), ["Dirty"] = false };
        if (state[key] is not JsonObject service || service["Events"] is not JsonArray ||
            service["Achievements"] is not JsonArray || service["ClockReads"] is not JsonArray ||
            service["Dirty"] is not JsonValue dirty || !dirty.TryGetValue<bool>(out _))
            throw new InvalidDataException("Saved pending campaign-service writes are missing or invalid.");
        story?.Remove("ServiceChanges");
    }
}
