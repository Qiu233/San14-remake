using System.Text.Json.Nodes;

namespace San14.Core;

/// <summary>Preserves historical carried and receiver stores as independent observations.</summary>
internal static class LegacyArmyStoresSaveMigration
{
    public static void Apply(JsonObject state)
    {
        if (state["Units"] is JsonObject units)
            foreach (JsonNode? node in units.Select(pair => pair.Value))
                if (node is JsonObject unit) MigrateUnit(unit);
        if (state["NativeArmyLifecycle"]?["Slots"] is not JsonArray slots) return;
        foreach (JsonNode? node in slots)
        {
            if (node is not JsonObject slot || !slot.ContainsKey("Gold5C") || !slot.ContainsKey("Supplies60"))
                throw new InvalidDataException("Legacy Army receiver stores are incomplete.");
            slot["StoredResources"] = new JsonObject
            {
                ["Gold"] = Amount(slot["Gold5C"]),
                ["Food"] = Amount(slot["Supplies60"])
            };
            slot.Remove("Gold5C");
            slot.Remove("Supplies60");
            if (slot["PendingImage"] is JsonObject pending) MigrateUnit(pending);
        }
    }

    private static void MigrateUnit(JsonObject unit)
    {
        if (unit["NativeReturnResources"] is JsonNode node)
        {
            if (node is not JsonObject stores || !stores.ContainsKey("Gold5C") || !stores.ContainsKey("Supplies60") ||
                stores["Gold5C"] is null || stores["Supplies60"] is null)
                throw new InvalidDataException("Legacy carried Army stores are incomplete.");
            unit["CarriedStores"] = new JsonObject
            {
                ["Gold"] = Amount(stores["Gold5C"]),
                ["Food"] = Amount(stores["Supplies60"])
            };
        }
        else unit["CarriedStores"] = null;
        unit.Remove("NativeReturnResources");
    }

    private static JsonNode? Amount(JsonNode? node)
    {
        if (node is null) return null;
        if (node is not JsonValue value || !value.TryGetValue<int>(out int amount))
            throw new InvalidDataException("Legacy Army stores must use signed 32-bit amounts.");
        return JsonValue.Create(amount);
    }
}
