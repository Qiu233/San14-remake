using System.Text.Json.Nodes;

namespace San14.Core;

internal static class BaseCombatSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version < 180 && (Captures(state).Any(request => request["WinnerDistrictId"] is not null) ||
            Effects(state).Any(request => request["BaseAbnormalFeedback"] is not null)))
            throw new InvalidDataException("Legacy world contains future Base battle operands.");
    }

    internal static void Migrate(JsonObject state, int version)
    {
        foreach (var capture in Captures(state))
        {
            if (version < 180) { capture["WinnerDistrictId"] = null; continue; }
            if (!capture.ContainsKey("WinnerDistrictId") || capture["WinnerDistrictId"] is not null &&
                (!Integer(capture["WinnerDistrictId"], 0, 51) || capture["WinnerUnitId"] is not null)) throw Invalid();
        }
        foreach (var request in Effects(state))
        {
            if (version < 180) { request["BaseAbnormalFeedback"] = null; continue; }
            if (!request.ContainsKey("BaseAbnormalFeedback") || request["BaseAbnormalFeedback"] is JsonNode feedback &&
                (feedback is not JsonObject value || !Integer(value["BaseLocationId"], 1, 61) ||
                 !Integer(value["OfficerId"], 0, 6000) || !Integer(value["HexId"], 0, 48400) ||
                 !Integer(value["Action"], 15, 16))) throw Invalid();
        }
    }

    private static IEnumerable<JsonObject> Captures(JsonObject state)
    {
        if (state["PendingCombatSettlements"]?["Requests"] is JsonArray queue)
            foreach (var value in queue.OfType<JsonObject>())
                if (value["SettlementKind"] is JsonValue kind && kind.TryGetValue<string>(out string? name) && name == "base-capture")
                    yield return value;
        if (state["BaseCaptureContinuation"]?["Operation"]?["Request"] is JsonObject retained) yield return retained;
    }
    private static IEnumerable<JsonObject> Effects(JsonObject state)
    {
        if (state["CampaignEffectRequests"]?["PendingBatches"] is not JsonArray batches) yield break;
        foreach (var batch in batches.OfType<JsonObject>())
            if (batch["Requests"] is JsonArray requests)
                foreach (var request in requests.OfType<JsonObject>()) yield return request;
    }
    private static bool Integer(JsonNode? node, int min, int max) => node is JsonValue scalar && scalar.TryGetValue<int>(out int number) && number >= min && number <= max;
    private static InvalidDataException Invalid() => new("Saved Base battle operands are missing or invalid.");
}
