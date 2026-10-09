using System.Text.Json.Nodes;

namespace San14.Core;

/// <summary>Splits historical source envelopes at the save boundary. Core keeps
/// decoded domain owners; the resource codec admits the untouched source channels.</summary>
internal static class LegacyArmyLifecycleSaveMigration
{
    internal static JsonObject? Apply(JsonObject state)
    {
        if (state.ContainsKey("ArmyLifecycle")) throw new InvalidDataException("Historical lifecycle contains a current owner.");
        JsonNode? node = state["NativeArmyLifecycle"];
        state.Remove("NativeArmyLifecycle");
        if (node is null) { state["ArmyLifecycle"] = null; return null; }
        if (node is not JsonObject old || old["Slots"] is not JsonArray slots ||
            old["Receivers"]?["Entries"] is not JsonArray entries)
            throw new InvalidDataException("Historical army lifecycle is incomplete.");
        foreach (string key in new[] { "Revision", "Language", "RuleVersion", "SourceScenarioUri", "SourceScenarioSha256",
            "Indexes", "ArmyGroups", "ArmyCautions", "Targets" })
            if (!old.ContainsKey(key)) throw new InvalidDataException("Historical army lifecycle channel is missing: " + key);
        var byId = new Dictionary<int, JsonObject>();
        foreach (var entry in entries)
        {
            if (entry is not JsonObject row || row["ReceiverId"]?["Value"] is not JsonValue value ||
                !value.TryGetValue<int>(out int id) || id <= 0 || !byId.TryAdd(id, row))
                throw new InvalidDataException("Historical receiver identities are invalid.");
            if (new[] { "StoredResources", "OverrideTarget", "GroupAssignmentReady", "OccupancyBindingId" }.Any(row.ContainsKey))
                throw new InvalidDataException("Historical receiver contains current ownership fields.");
        }
        var sourceSlots = new JsonArray();
        foreach (var item in slots)
        {
            if (item is not JsonObject slot || slot["ReceiverId"]?["Value"] is not JsonValue identity ||
                !identity.TryGetValue<int>(out int id) || !byId.TryGetValue(id, out var receiver))
                throw new InvalidDataException("Historical source receiver has no domain owner.");
            var source = slot.DeepClone().AsObject();
            foreach (string field in new[] { "StoredResources", "OverrideTarget", "GroupAssignmentReady", "OccupancyBindingId" })
            {
                if (!source.ContainsKey(field)) throw new InvalidDataException("Historical receiver channel is missing: " + field);
                receiver[field] = source[field]?.DeepClone(); source.Remove(field);
            }
            foreach (string cache in new[] { "Direction36", "MapUiByte13C", "CommandControl140", "RouteHold56", "RouteGoal1F8" })
                if (!source.ContainsKey(cache)) source[cache] = null;
            sourceSlots.Add(source);
        }
        var domain = new JsonObject();
        foreach (string field in new[] { "Receivers", "Indexes", "ArmyGroups", "ArmyCautions", "Targets" })
            domain[field] = old[field]?.DeepClone();
        state["ArmyLifecycle"] = domain;
        var knowledge = new JsonObject { ["Slots"] = sourceSlots, ["ArmyFlags150"] = old["ArmyFlags150"]?.DeepClone() };
        foreach (string field in new[] { "Revision", "Language", "RuleVersion", "SourceScenarioUri", "SourceScenarioSha256" })
            knowledge[field] = old[field]?.DeepClone();
        return knowledge;
    }
}
