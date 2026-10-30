using System.Text.Json.Nodes;

namespace San14.Core;

internal static class ItemAppearanceSaveFields
{
    private static IEnumerable<JsonObject> Definitions(JsonObject state) =>
        (state["ItemDefinitions"] as JsonObject)?.Select(pair => pair.Value).OfType<JsonObject>() ?? [];

    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version < 188 && Definitions(state).Any(definition => definition["Appearance"] is not null))
            throw new InvalidDataException("Legacy world contains future item appearance knowledge.");
    }

    internal static void Migrate(JsonObject state, int version)
    {
        foreach (var definition in Definitions(state))
        {
            if (version < 188)
            {
                definition["Appearance"] = null;
                continue;
            }
            if (!definition.ContainsKey("Appearance")) throw Invalid();
            if (definition["Appearance"] is not JsonNode saved) continue;
            if (saved is not JsonObject fields || fields.Count != 7 ||
                !Integer(fields, "Kind", 26) || !Integer(fields, "AbilityKind", 5) ||
                !Integer(fields, "AbilityValue", 10) || !Integer(fields, "EffectKind", 2) ||
                !Integer(fields, "EffectValue", 10) || !Integer(fields, "ImageIndex", 456) ||
                fields["Description"] is not JsonValue description || !description.TryGetValue<string>(out string? text) ||
                text is null || text.Contains('\0'))
                throw Invalid();
        }
    }

    private static bool Integer(JsonObject fields, string name, int maximum) => fields[name] is JsonValue value &&
        value.TryGetValue<int>(out int number) && number >= 0 && number <= maximum;

    private static InvalidDataException Invalid() => new("Saved item appearance is missing or invalid.");
}
