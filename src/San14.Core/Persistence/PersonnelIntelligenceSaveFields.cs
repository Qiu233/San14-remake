using System.Text.Json.Nodes;

namespace San14.Core;

internal static class PersonnelIntelligenceSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        PersonnelIntelligenceNarrationSaveFields.RejectLegacyFields(state, version);
        if (version < 183 && (state["PersonnelCommands"]?["Intelligences"] is not null ||
            state["CampaignEffectRequests"]?["PendingBatches"] is JsonArray batches && batches.OfType<JsonObject>().Any(batch =>
                batch["Source"] is JsonValue value && value.TryGetValue<int>(out int source) && source == (int)CampaignEffectSource.IntelligenceCommands)))
            throw new InvalidDataException("Legacy world contains future intelligence journeys.");
    }

    internal static void Migrate(JsonObject state, int version)
    {
        PersonnelIntelligenceNarrationSaveFields.Migrate(state, version);
        if (state["PersonnelCommands"] is not JsonObject owner) return;
        if (version < 183) { owner["Intelligences"] = null; return; }
        if (!owner.ContainsKey("Intelligences")) throw Invalid();
        if (owner["Intelligences"] is not JsonNode node) return;
        if (node is not JsonObject tasks) throw Invalid();
        foreach (var pair in tasks)
        {
            if (!int.TryParse(pair.Key, out int id) || id < 0 || pair.Value is not JsonObject task ||
                task.Count != 21 || !new[] { "Id", "DispatchSlot", "MemberIds", "Kind", "ForceId", "SourceLocationId",
                    "SourceDistrictId", "TargetId", "TargetOriginalForceId", "StartHexId", "EndHexId", "StartDate", "DueDate",
                    "DurationOrLevel", "SavedResult", "Started", "Cancelled", "IsRetiring", "IsAssociated", "ReportToPlayer", "NarrationProgress" }.All(task.ContainsKey) ||
                task["Id"] is not JsonValue value || !value.TryGetValue<int>(out int taskId) || taskId != id)
                throw Invalid();
        }
    }

    private static InvalidDataException Invalid() => new("Saved intelligence journey state is missing or invalid.");
}
