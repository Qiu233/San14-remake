using System.Text.Json.Nodes;

namespace San14.Core;

internal static class ArmyTraitFeedbackSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version < 173 && Feedback(state).Any(feedback => feedback["Origin"] is not null))
            throw new InvalidDataException("Legacy Army feedback contains future origin knowledge.");
    }

    internal static void Migrate(JsonObject state, int version)
    {
        foreach (var feedback in Feedback(state))
        {
            if (version < 173) feedback["Origin"] = null;
            else if (!feedback.ContainsKey("Origin") || feedback["Origin"] is JsonNode node &&
                (node is not JsonValue value || !value.TryGetValue<int>(out int origin) ||
                 !Enum.IsDefined((ArmyTraitFeedbackOrigin)origin)))
                throw new InvalidDataException("Saved Army feedback origin is missing or invalid.");
        }
    }

    private static IEnumerable<JsonObject> Feedback(JsonObject state)
    {
        if (state["CampaignEffectRequests"]?["PendingBatches"] is not JsonArray batches) yield break;
        foreach (var batch in batches.OfType<JsonObject>())
            if (batch["Requests"] is JsonArray requests)
                foreach (var request in requests.OfType<JsonObject>())
                    if (request["ArmyTraitFeedback"] is JsonObject feedback) yield return feedback;
    }
}
