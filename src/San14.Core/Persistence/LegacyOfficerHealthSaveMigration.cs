using System.Text.Json.Nodes;

namespace San14.Core;

/// <summary>Retains historical health knowledge while moving it out of the
/// source ability record. No resource or executable is read during migration.</summary>
internal static class LegacyOfficerHealthSaveMigration
{
    public static void Apply(JsonObject state)
    {
        if (state["Officers"] is not JsonObject officers) return;
        foreach (JsonNode? node in officers.Select(pair => pair.Value))
        {
            if (node is not JsonObject officer) continue;
            if (officer.ContainsKey("Health"))
                throw new InvalidDataException("Legacy officer contains conflicting health owners.");
            officer["Health"] = null;
            if (officer["NativeAbilityRuntime"] is not JsonObject ability) continue;
            if (!ability.ContainsKey("Health199"))
                throw new InvalidDataException("Legacy officer health knowledge is missing.");
            if (ability["Health199"] is JsonNode health)
            {
                if (health is not JsonValue value || !value.TryGetValue<int>(out int identity) || identity is < 0 or > byte.MaxValue)
                    throw new InvalidDataException("Legacy officer health identity is invalid.");
                officer["Health"] = new JsonObject { ["StateId"] = identity };
            }
            ability.Remove("Health199");
        }
    }
}
