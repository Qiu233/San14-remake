using System.Text.Json.Nodes;

namespace San14.Core;

internal static class OfficerSpecialVoiceSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version < 181 && People(state).Any(person => person["SpecialVoiceType"] is not null))
            throw new InvalidDataException("Legacy person contains a future special voice type.");
    }

    internal static void Migrate(JsonObject state, int version)
    {
        foreach (var person in People(state))
        {
            if (version < 181) { person["SpecialVoiceType"] = null; continue; }
            if (!person.ContainsKey("SpecialVoiceType") || person["SpecialVoiceType"] is JsonNode node &&
                (node is not JsonValue value || !value.TryGetValue<byte>(out _)))
                throw new InvalidDataException("Saved special voice knowledge is missing or invalid.");
        }
    }

    private static IEnumerable<JsonObject> People(JsonObject state)
    {
        if (state["Officers"] is JsonObject officers)
            foreach (var person in officers.Select(row => row.Value).OfType<JsonObject>()) yield return person;
        if (state["PersonnelHire"]?["Templates"] is JsonObject templates)
            foreach (var template in templates.Select(row => row.Value).OfType<JsonObject>())
                if (template["Person"] is JsonObject person) yield return person;
    }
}
