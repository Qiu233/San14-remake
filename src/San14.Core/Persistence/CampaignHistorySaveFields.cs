using System.Text.Json.Nodes;

namespace San14.Core;

internal static class CampaignHistorySaveFields
{
    internal static void Migrate(JsonObject state, int version)
    {
        if (version < 137)
        {
            state["CampaignHistory"] = null;
            state["DefaultForceReportColorIndex"] = null;
            if (state["CampaignEventEligibility"] is JsonObject oldService)
                oldService["HistoryRecordingSuppressed"] = null;
            if (state["Forces"] is JsonObject oldForces)
                foreach (var pair in oldForces)
                    if (pair.Value is JsonObject force) force["ReportColorIndex"] = null;
        }
        else
        {
            RequireKeys(state, "CampaignHistory", "DefaultForceReportColorIndex");
            if (state["CampaignHistory"] is JsonNode history)
            {
                var fields = RequireExact(history, "Cursor", "Records", "DefaultSupplementalText");
                if (fields["Records"] is not JsonArray records || records.Count != CampaignHistoryState.Capacity)
                    throw new InvalidDataException("Saved campaign history records are incomplete.");
                foreach (var record in records) CheckRecord(record);
            }
            if (state["CampaignEventEligibility"] is JsonObject service)
                RequireKeys(service, "HistoryRecordingSuppressed");
            if (state["Forces"] is JsonObject forces)
                foreach (var pair in forces)
                    if (pair.Value is JsonObject force) RequireKeys(force, "ReportColorIndex");
        }
        if (state["CampaignEffectRequests"]?["PendingBatches"] is not JsonArray batches) return;
        foreach (var batch in batches)
        {
            if (version < 137 && batch?["Source"] is JsonValue source && source.TryGetValue<int>(out int origin) &&
                origin >= (int)CampaignEffectSource.LandmarkInvestment)
                throw new InvalidDataException("Legacy campaign effects have a future history origin.");
            if (batch?["Requests"] is JsonArray requests)
                foreach (var node in requests)
                    if (node is JsonObject request)
                    {
                        if (version < 137) request["HistoryNotification"] = null;
                        else
                        {
                            RequireKeys(request, "HistoryNotification");
                            if (request["HistoryNotification"] is JsonNode notification)
                            {
                                var fields = RequireExact(notification, "Slot", "Record");
                                CheckRecord(fields["Record"]);
                            }
                        }
                    }
        }
    }

    private static void CheckRecord(JsonNode? node)
    {
        var value = RequireExact(node, "Category", "DistrictId", "HexId", "CampaignDay", "PersonId",
            "Flags", "ColorIndex", "Text", "SupplementalText", "ForceShortName");
        foreach (var (key, count) in new[] { ("Text", 55), ("SupplementalText", 85), ("ForceShortName", 2) })
            if (value[key] is not JsonArray array || array.Count != count)
                throw new InvalidDataException("Saved history text window is incomplete.");
    }
    private static void RequireKeys(JsonObject value, params string[] keys)
    {
        if (keys.Any(key => !value.ContainsKey(key)))
            throw new InvalidDataException("Saved history knowledge is missing.");
    }
    private static JsonObject RequireExact(JsonNode? value, params string[] keys)
    {
        if (value is not JsonObject fields || fields.Count != keys.Length || keys.Any(key => !fields.ContainsKey(key)))
            throw new InvalidDataException("Saved history fields are missing or unexpected.");
        return fields;
    }
}
