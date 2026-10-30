using System.Text.Json.Nodes;

namespace San14.Core;

internal static class ItemNameSortSaveFields
{
    private static IEnumerable<JsonObject> Definitions(JsonObject state) =>
        (state["ItemDefinitions"] as JsonObject)?.Select(row => row.Value).OfType<JsonObject>() ?? [];

    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version < 212 && Definitions(state).Any(row => row.ContainsKey("NameSortKey")))
            throw new InvalidDataException("Legacy world contains future item name-order knowledge.");
    }

    internal static void Migrate(JsonObject state, int version)
    {
        if (version < 212)
            foreach (var row in Definitions(state)) row["NameSortKey"] = null;
        ValidateWorld(state);
    }

    internal static void ValidateWorld(JsonObject state)
    {
        foreach (var row in Definitions(state))
        {
            if (!row.ContainsKey("NameSortKey")) throw Invalid();
            if (row["NameSortKey"] is null) continue;
            if (row["NameSortKey"] is not JsonValue value || !value.TryGetValue<string>(out string? text) ||
                text is null || text.Contains('\0')) throw Invalid();
        }
    }

    private static InvalidDataException Invalid() => new("Saved item name-order knowledge is missing or invalid.");
}
