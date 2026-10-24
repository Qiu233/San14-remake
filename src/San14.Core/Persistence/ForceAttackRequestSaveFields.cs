using System.Text.Json.Nodes;

namespace San14.Core;

/// <summary>World140 adds the two actual request fields omitted by the old
/// objective projection. It never reconstructs them from Enabled or resources.</summary>
internal static class ForceAttackRequestSaveFields
{
    internal static void Apply(JsonObject state, int version)
    {
        if (version < 140) state["PreparedActionReport"] = null;
        else if (!state.ContainsKey("PreparedActionReport")) throw new InvalidDataException("Saved action report owner is missing.");
        if (state["Forces"] is not JsonObject forces) throw new InvalidDataException("Saved Forces are missing.");
        foreach (JsonNode? node in forces.Select(pair => pair.Value))
        {
            if (node is not JsonObject force) throw new InvalidDataException("Saved Force is invalid.");
            if (force["CombatObjective"] is not JsonObject objective) continue;
            foreach (string field in new[] { "RequestingForceId", "RemainingMonths" })
            {
                if (version < 140) objective[field] = null;
                else if (!objective.ContainsKey(field) || objective[field] is JsonNode value &&
                    (value is not JsonValue scalar || !scalar.TryGetValue<int>(out int number) || number is < 0 or > 255))
                    throw new InvalidDataException("Saved Force attack request fields are missing or invalid.");
            }
        }
    }
}
