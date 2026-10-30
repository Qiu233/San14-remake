using System.Text.Json.Nodes;

namespace San14.Core;

internal static class CampaignHistorySaveFields
{
    internal static void Migrate(JsonObject state, int version)
    {
        if (version < 212 && state["CampaignHistory"] is JsonObject legacyHistory &&
            legacyHistory["EmperorReceptionChronicleSuppressed"] is not null)
            throw new InvalidDataException("Legacy history contains a future emperor reception gate.");
        const string restoreKey = "CampaignTemporaryHexRestoreEntries";
        if (version < 205)
        {
            if (state[restoreKey] is not null)
                throw new InvalidDataException("Legacy world contains a future temporary Hex restoration owner.");
            state[restoreKey] = null;
        }
        else
        {
            if (!state.ContainsKey(restoreKey))
                throw new InvalidDataException("Saved temporary Hex restoration knowledge is missing.");
            if (state[restoreKey] is JsonNode restore)
            {
                if (restore is not JsonArray entries)
                    throw new InvalidDataException("Saved temporary Hex restoration storage is invalid.");
                int previous = -1;
                foreach (var entry in entries)
                {
                    var fields = RequireExact(entry, "HexPhysicalSlot", "ForcePhysicalSlot");
                    if (fields["HexPhysicalSlot"] is not JsonValue hex || !hex.TryGetValue<int>(out int physical) ||
                        physical is < 0 or >= 48400 || physical <= previous ||
                        fields["ForcePhysicalSlot"] is not JsonValue force || !force.TryGetValue<int>(out int owner) ||
                        owner is < 0 or > 51)
                        throw new InvalidDataException("Saved temporary Hex restoration entry is invalid.");
                    previous = physical;
                }
            }
        }
        if (state["CampaignEventEligibility"] is JsonObject availableOwner)
        {
            const string available = "HasAvailableResult";
            if (version < 205)
            {
                if (availableOwner[available] is not null)
                    throw new InvalidDataException("Legacy event manager contains a future available-result fact.");
                availableOwner[available] = null;
            }
            else if (!availableOwner.ContainsKey(available) || availableOwner[available] is JsonNode value &&
                (value is not JsonValue boolean || !boolean.TryGetValue<bool>(out _)))
                throw new InvalidDataException("Saved event manager available-result knowledge is missing or invalid.");
        }

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
                if (history is not JsonObject historyFields)
                    throw new InvalidDataException("Saved campaign history owner is invalid.");
                const string reception = "EmperorReceptionChronicleSuppressed";
                if (version < 212)
                {
                    if (historyFields[reception] is not null)
                        throw new InvalidDataException("Legacy history contains a future emperor reception gate.");
                    historyFields[reception] = null;
                }
                else if (!historyFields.ContainsKey(reception) || historyFields[reception] is JsonNode known &&
                    (known is not JsonValue boolean || !boolean.TryGetValue<bool>(out _)))
                    throw new InvalidDataException("Saved emperor reception gate knowledge is missing or invalid.");
                var fields = RequireExact(historyFields, "Cursor", "Records", "DefaultSupplementalText", reception);
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
