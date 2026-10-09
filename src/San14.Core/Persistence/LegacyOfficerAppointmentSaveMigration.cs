using System.Text.Json.Nodes;

namespace San14.Core;

internal static class LegacyOfficerAppointmentSaveMigration
{
    public static void Apply(JsonObject state)
    {
        if (state["Officers"] is not JsonObject officers) return;
        foreach (JsonNode? node in officers.Select(pair => pair.Value))
        {
            if (node is not JsonObject officer) continue;
            if (officer.ContainsKey("Appointment"))
                throw new InvalidDataException("Legacy officer contains conflicting appointment owners.");
            JsonNode? declared = officer["OfficeId"];
            Validate(declared, int.MaxValue);
            JsonNode? observed = null;
            if (officer["NativeAbilityRuntime"] is JsonObject ability)
            {
                if (!ability.ContainsKey("Office11F"))
                    throw new InvalidDataException("Legacy officer office observation is missing.");
                observed = ability["Office11F"];
                Validate(observed, byte.MaxValue);
                ability.Remove("Office11F");
            }
            officer["Appointment"] = new JsonObject
            {
                ["AssignedOfficeId"] = declared?.DeepClone(),
                ["ObservedOfficeId"] = observed?.DeepClone()
            };
            officer.Remove("OfficeId");
        }
    }

    private static void Validate(JsonNode? node, int maximum)
    {
        if (node is not null && (node is not JsonValue value || !value.TryGetValue<int>(out int id) || id < 0 || id > maximum))
            throw new InvalidDataException("Legacy officer appointment identity is invalid.");
    }
}
