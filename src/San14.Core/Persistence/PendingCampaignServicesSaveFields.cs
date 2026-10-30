using System.Text.Json.Nodes;

namespace San14.Core;

internal static class PendingCampaignServicesSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version < 168 && state["PendingCampaignServices"] is not null)
            throw new InvalidDataException("Legacy world contains a future pending service outbox.");
        if (version < 181)
            foreach (var service in new[] { state["PendingCampaignServices"], state["NativeNormalTurnContinuation"]?["StoryPlayback"]?["ServiceChanges"] }.OfType<JsonObject>())
                if (new[] { "CompletedScenarios", "CompletedEndingTracks", "UnlockedBonusOfficerIndices", "CompletedTutorialLessons" }.Any(key => service[key] is JsonNode node &&
                    (node is not JsonArray array || array.Count != 0)))
                    throw new InvalidDataException("Legacy world contains future Ending service writes.");
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
        if (version < 181)
        {
            service["CompletedScenarios"] = new JsonArray();
            service["CompletedEndingTracks"] = new JsonArray();
            service["UnlockedBonusOfficerIndices"] = new JsonArray();
            service["CompletedTutorialLessons"] = new JsonArray();
        }
        if (!Indices(service["CompletedScenarios"], 199) || !Indices(service["CompletedEndingTracks"], 2) ||
            !Indices(service["UnlockedBonusOfficerIndices"], 60) || !Indices(service["CompletedTutorialLessons"], 7))
            throw new InvalidDataException("Saved pending Ending service writes are missing or invalid.");
        story?.Remove("ServiceChanges");
    }

    private static bool Indices(JsonNode? node, int max) => node is JsonArray array &&
        array.All(item => item is JsonValue value && value.TryGetValue<int>(out int index) && index >= 0 && index <= max);
}
