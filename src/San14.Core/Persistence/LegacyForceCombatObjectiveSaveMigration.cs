using System.Text.Json.Nodes;
using System.Text.Json;

namespace San14.Core;

/// <summary>Converts the old layout-shaped force fields once at the save boundary.</summary>
internal static class LegacyForceCombatObjectiveSaveMigration
{
    public static void Apply(JsonObject state, int schemaVersion)
    {
        var forces = state["Forces"] as JsonObject ?? throw new InvalidDataException("Saved forces are missing.");
        foreach (JsonNode? node in forces.Select(pair => pair.Value))
            if (node is JsonObject force) force["CombatObjective"] = null;
        if (state["NativeArmySimulation"]?["Forces"] is not JsonObject oldForces) return;
        foreach ((string id, JsonNode? node) in oldForces)
        {
            if (node is not JsonObject old || forces[id] is not JsonObject force)
                throw new InvalidDataException("Legacy combat objective has no force owner.");
            if (schemaVersion >= 39 && (!old.ContainsKey("ResultGate15F") ||
                !old.ContainsKey("ResultDefinition160") || !old.ContainsKey("ResultValue162")))
                throw new InvalidDataException("Legacy force result fields are incomplete.");
            // Decode with the historical widths before discarding the layout.
            // Above-limit progress is valid saved knowledge, not a value to clamp.
            int? gate = Read(old["ResultGate15F"], byte.MaxValue);
            int? target = Read(old["ResultDefinition160"], byte.MaxValue);
            int? progress = Read(old["ResultValue162"], ushort.MaxValue);
            force["CombatObjective"] = new JsonObject
            {
                ["Enabled"] = gate is int value ? JsonValue.Create(value is >= 1 and <= 51) : null,
                ["TargetLocationId"] = target is int location ? JsonValue.Create(location) : null,
                ["Progress"] = progress is int current ? JsonValue.Create(current) : null,
            };
            old.Remove("ResultGate15F"); old.Remove("ResultDefinition160"); old.Remove("ResultValue162");
        }
    }

    private static int? Read(JsonNode? node, int maximum)
    {
        if (node is null) return null;
        if (node.GetValueKind() == JsonValueKind.Number && node is JsonValue scalar &&
            scalar.TryGetValue<int>(out int value) && value >= 0 && value <= maximum) return value;
        throw new InvalidDataException("Legacy combat objective scalar is invalid.");
    }
}
