using System.Text.Json.Nodes;

namespace San14.Core;

internal static class NormalTurnPresentationSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version < 181 && state["ActiveMonthlyScene"]?["Family"] is JsonValue personnel &&
            personnel.TryGetValue<int>(out int personnelFamily) && personnelFamily >= (int)MonthlySceneFamily.Personnel)
            throw new InvalidDataException("Legacy world contains a future admitted personnel scene.");
        if (version < 176 && state["ActiveMonthlyScene"]?["Family"] is JsonValue future &&
            future.TryGetValue<int>(out int futureFamily) && futureFamily >= (int)MonthlySceneFamily.Coalition)
            throw new InvalidDataException("Legacy world contains a future admitted coalition or emperor scene.");
        if (version < 174 && state["ActiveMonthlyScene"]?["Family"] is JsonValue family &&
            family.TryGetValue<int>(out int sceneFamily) && sceneFamily >= (int)MonthlySceneFamily.PersonDeath)
            throw new InvalidDataException("Legacy world contains a future admitted death scene.");
        if (version >= 169) return;
        if (state["ActiveMonthlyScene"] is not null || Notices(state).Any(notice => notice["TextAtSource"] is not null))
            throw new InvalidDataException("Legacy world contains future monthly presentation state.");
        if (Requests(state).Any(request => request["MapObjectPresentation"]?["Event"] is JsonValue value &&
            value.TryGetValue<int>(out int kind) && kind >= (int)MapObjectPresentationEvent.BaseOwnershipLostVisual))
            throw new InvalidDataException("Legacy world contains future base ownership-loss presentation.");
    }

    internal static void Migrate(JsonObject state, int version)
    {
        if (version < 169) state["ActiveMonthlyScene"] = null;
        else if (!state.ContainsKey("ActiveMonthlyScene") || state["ActiveMonthlyScene"] is JsonNode active &&
            (active is not JsonObject scene || scene["EventKind"] is not JsonValue kind || !kind.TryGetValue<int>(out int value) ||
             !(Integer(scene["Family"], (int)MonthlySceneFamily.FinalOrder) && value is 10 or 26 or 31 ||
               version >= 174 && Integer(scene["Family"], (int)MonthlySceneFamily.PersonDeath) && value is 4 or 5 or 6 ||
               version >= 176 && Integer(scene["Family"], (int)MonthlySceneFamily.Coalition) && value is 24 or 40 or 52 or 53 or 54 ||
               version >= 176 && Integer(scene["Family"], (int)MonthlySceneFamily.Emperor) && value is 6 or 45 or 55 ||
               version >= 181 && Integer(scene["Family"], (int)MonthlySceneFamily.Personnel) && value is 9 or 10 or 32 or 35 or 44 or 54)))
            throw new InvalidDataException("Saved monthly scene is missing or invalid.");
        foreach (var notice in Notices(state))
        {
            if (version < 169) notice["TextAtSource"] = null;
            else if (!notice.ContainsKey("TextAtSource") || notice["TextAtSource"] is JsonNode text &&
                (text is not JsonValue stringValue || !stringValue.TryGetValue<string>(out _)))
                throw new InvalidDataException("Saved force-resource notice text knowledge is missing or invalid.");
        }
    }

    private static IEnumerable<JsonObject> Notices(JsonObject state)
    {
        foreach (var request in Requests(state))
            if (request["NormalTurnForceResourceNotice"] is JsonObject notice) yield return notice;
    }

    private static IEnumerable<JsonObject> Requests(JsonObject state)
    {
        if (state["CampaignEffectRequests"]?["PendingBatches"] is not JsonArray batches) yield break;
        foreach (var batch in batches.OfType<JsonObject>())
            if (batch["Requests"] is JsonArray requests)
                foreach (var request in requests.OfType<JsonObject>())
                    yield return request;
    }

    private static bool Integer(JsonNode? node, int expected) => node is JsonValue value &&
        value.TryGetValue<int>(out int number) && number == expected;
}
