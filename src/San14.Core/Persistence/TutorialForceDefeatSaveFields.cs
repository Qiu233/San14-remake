using System.Text.Json.Nodes;

namespace San14.Core;

internal static class TutorialForceDefeatSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version >= 171) return;
        if (state["TutorialForceDefeat"] is not null || Requests(state).Any(request => request["TutorialForceDefeatPresentation"] is not null) ||
            state["CampaignTurnStop"]?["Reason"] is JsonValue value && value.TryGetValue<int>(out int reason) &&
            reason >= (int)CampaignTurnStopReason.TutorialForceDefeat)
            throw new InvalidDataException("Legacy world contains future tutorial failure state.");
    }

    internal static void Migrate(JsonObject state, int version)
    {
        const string key = "TutorialForceDefeat";
        if (version < 171) state[key] = null;
        else if (!state.ContainsKey(key) || state[key] is JsonNode node &&
            (node is not JsonObject owner || !NonnegativeInt(owner["ExitParagraphIndex"]) ||
             owner["FailureHandled"] is not JsonValue value || !value.TryGetValue<bool>(out _)))
            throw new InvalidDataException("Saved tutorial failure progress is missing or invalid.");
        foreach (var request in Requests(state))
        {
            const string payloadKey = "TutorialForceDefeatPresentation";
            if (version < 171) request[payloadKey] = null;
            else if (!request.ContainsKey(payloadKey) || request[payloadKey] is JsonNode payload &&
                (payload is not JsonObject presentation || presentation["Operation"] is not JsonValue operation ||
                 !operation.TryGetValue<int>(out int code) || !Enum.IsDefined((TutorialForceDefeatPresentationOperation)code) ||
                 !presentation.ContainsKey("ExitParagraphIndex") || presentation["ExitParagraphIndex"] is JsonNode index && !NonnegativeInt(index)))
                throw new InvalidDataException("Saved tutorial failure presentation is missing or invalid.");
        }
    }

    private static bool NonnegativeInt(JsonNode? node) => node is JsonValue value && value.TryGetValue<int>(out int number) && number >= 0;
    private static IEnumerable<JsonObject> Requests(JsonObject state)
    {
        if (state["CampaignEffectRequests"]?["PendingBatches"] is not JsonArray batches) yield break;
        foreach (var batch in batches.OfType<JsonObject>())
            if (batch["Requests"] is JsonArray requests)
                foreach (var request in requests.OfType<JsonObject>()) yield return request;
    }
}
