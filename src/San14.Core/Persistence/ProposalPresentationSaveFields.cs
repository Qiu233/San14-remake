using System.Text.Json.Nodes;

namespace San14.Core;

internal static class ProposalPresentationSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version >= 181) return;
        foreach (var batch in Batches(state))
            if (Integer(batch["Source"], (int)CampaignEffectSource.ProposalCommands, int.MaxValue) ||
                batch["Requests"] is JsonArray requests && requests.OfType<JsonObject>().Any(request => request["ProposalResultTable"] is not null || request["ProposalOfficerDetail"] is not null))
                throw new InvalidDataException("Legacy world contains future proposal presentation work.");
    }
    internal static void Migrate(JsonObject state, int version)
    {
        foreach (var batch in Batches(state))
            if (batch["Requests"] is JsonArray requests)
                foreach (var request in requests.OfType<JsonObject>())
                {
                    const string detailKey = "ProposalOfficerDetail";
                    if (version < 181) request[detailKey] = null;
                    else if (!request.ContainsKey(detailKey) || request[detailKey] is JsonNode detail &&
                        (detail is not JsonObject detailFields || detailFields.Count != 1 || !Integer(detailFields["OfficerId"], 1, 6000))) throw Invalid();
                    const string key = "ProposalResultTable";
                    if (version < 181) { request[key] = null; continue; }
                    if (!request.ContainsKey(key)) throw Invalid();
                    if (request[key] is not JsonNode node) continue;
                    if (node is not JsonObject table || table.Count != 2 || !Integer(table["ForceId"], 1, 51) ||
                        table["Rows"] is not JsonArray rows || rows.Count > 12 || rows.Any(row => row is not JsonObject entry ||
                            entry.Count != 4 || !String(entry["NameAtSource"]) || !String(entry["BeforeText"]) || !String(entry["AfterText"]) ||
                            !entry.ContainsKey("OfficerAtSource") || entry["OfficerAtSource"] is JsonNode officer &&
                            (officer is not JsonObject speaker || speaker.Count != 3 || !Integer(speaker["OfficerId"], 0, 6000) ||
                             !String(speaker["NameAtSource"]) || !speaker.ContainsKey("PortraitAppearanceIndex") ||
                             speaker["PortraitAppearanceIndex"] is JsonNode appearance && !Integer(appearance, 0, ushort.MaxValue)))) throw Invalid();
                }
    }
    private static IEnumerable<JsonObject> Batches(JsonObject state) =>
        (state["CampaignEffectRequests"]?["PendingBatches"] as JsonArray)?.OfType<JsonObject>() ?? [];
    private static bool String(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out _);
    private static bool Integer(JsonNode? node, int min, int max) => node is JsonValue value && value.TryGetValue<int>(out int number) && number >= min && number <= max;
    private static InvalidDataException Invalid() => new("Saved proposal result table is missing or invalid.");
}
