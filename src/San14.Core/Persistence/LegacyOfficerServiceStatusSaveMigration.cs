using System.Text.Json.Nodes;

namespace San14.Core;

internal static class LegacyOfficerServiceStatusSaveMigration
{
    public static void Apply(JsonObject state)
    {
        if (state["Officers"] is not JsonObject officers) return;
        foreach (JsonNode? node in officers.Select(pair => pair.Value))
        {
            if (node is not JsonObject officer) continue;
            if (officer.ContainsKey("ServiceStatus"))
                throw new InvalidDataException("Legacy officer contains conflicting service status owners.");
            JsonNode? status = null;
            if (officer["NativeAbilityRuntime"] is JsonObject ability)
            {
                if (!ability.ContainsKey("Status11E"))
                    throw new InvalidDataException("Legacy officer service status is missing.");
                status = ability["Status11E"];
                if (status is not null && (status is not JsonValue value || !value.TryGetValue<int>(out int id) || id is < 0 or > byte.MaxValue))
                    throw new InvalidDataException("Legacy officer service status is invalid.");
                ability.Remove("Status11E");
            }
            officer["ServiceStatus"] = status is null ? null : new JsonObject { ["KindId"] = status.DeepClone() };
        }
    }
}
