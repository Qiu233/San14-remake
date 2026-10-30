using System.Text.Json.Nodes;

namespace San14.Core;

internal static class PlaceholderAreaDevelopmentSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version < 187 && (state["PlaceholderAreaDevelopment"] is not null ||
            state["PlaceholderForceAdministrationFlags"] is not null))
            throw new InvalidDataException("Legacy world contains future placeholder area or Force state.");
    }

    internal static void Migrate(JsonObject state, int version)
    {
        if (version < 187)
        {
            state["PlaceholderAreaDevelopment"] = null;
            state["PlaceholderForceAdministrationFlags"] = null;
            return;
        }
        if (!state.ContainsKey("PlaceholderForceAdministrationFlags") ||
            state["PlaceholderForceAdministrationFlags"] is JsonNode flags &&
            (flags is not JsonValue value || !value.TryGetValue<int>(out int number) || number is < 0 or > byte.MaxValue))
            throw new InvalidDataException("Saved placeholder Force administration flags are missing or invalid.");
        if (!state.ContainsKey("PlaceholderAreaDevelopment")) throw Invalid();
        if (state["PlaceholderAreaDevelopment"] is not JsonNode saved) return;
        if (saved is not JsonObject fields || fields.Count != 3 ||
            !Word(fields, "Commerce") || !Word(fields, "Agriculture") || !Word(fields, "Barracks"))
            throw Invalid();
    }

    private static bool Word(JsonObject fields, string name) => fields[name] is JsonValue value &&
        value.TryGetValue<int>(out int number) && number is >= 0 and <= ushort.MaxValue;

    private static InvalidDataException Invalid() =>
        new("Saved placeholder area development is missing or invalid.");
}
