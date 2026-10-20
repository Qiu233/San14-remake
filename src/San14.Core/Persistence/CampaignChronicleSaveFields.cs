using System.Text.Json.Nodes;

namespace San14.Core;

internal static class CampaignChronicleSaveFields
{
    internal static void Migrate(JsonObject state, int version)
    {
        if (version < 138)
        {
            // Ignore even malformed forged future knowledge. No legacy report
            // or scenario state reconstructs this independently owned journal.
            state["CampaignChronicle"] = null;
            return;
        }
        if (!state.ContainsKey("CampaignChronicle"))
            throw new InvalidDataException("Saved campaign chronology knowledge is missing.");
        if (state["CampaignChronicle"] is not JsonNode chronicle) return;
        var fields = RequireExact(chronicle, "RecordingSuppression", "PrimaryEntries", "ImportantEntries");
        CheckEntries(fields["PrimaryEntries"], CampaignChronicleState.PrimaryCapacity);
        CheckEntries(fields["ImportantEntries"], CampaignChronicleState.ImportantCapacity);
        // The normal typed deserializer checks every nullable int, byte,
        // ushort and uint, including each fixed-window unit. Domain validation
        // then checks bounds without normalizing any text or inactive record.
    }

    private static void CheckEntries(JsonNode? node, int capacity)
    {
        if (node is not JsonArray entries || entries.Count != capacity)
            throw new InvalidDataException("Saved campaign chronology entries are incomplete.");
        foreach (var entry in entries)
        {
            var fields = RequireExact(entry, "EventCode", "Family", "CampaignDay", "MainLabel", "ColorIndex",
                "EntryKind", "Label0", "Label1", "Label2", "AreaColors", "CityForceShortNames",
                "HexIds", "Sequence", "EntryFlags");
            RequireArray(fields["MainLabel"], 16);
            RequireArray(fields["Label0"], 9);
            RequireArray(fields["Label1"], 9);
            RequireArray(fields["Label2"], 9);
            RequireArray(fields["AreaColors"], 501);
            foreach (var name in RequireArray(fields["CityForceShortNames"], 52)) RequireArray(name, 2);
            RequireArray(fields["HexIds"], 4);
        }
    }

    private static JsonArray RequireArray(JsonNode? value, int count)
    {
        if (value is not JsonArray array || array.Count != count)
            throw new InvalidDataException("Saved campaign chronology window is incomplete.");
        return array;
    }

    private static JsonObject RequireExact(JsonNode? value, params string[] keys)
    {
        if (value is not JsonObject fields || fields.Count != keys.Length || keys.Any(key => !fields.ContainsKey(key)))
            throw new InvalidDataException("Saved campaign chronology fields are missing or unexpected.");
        return fields;
    }
}
