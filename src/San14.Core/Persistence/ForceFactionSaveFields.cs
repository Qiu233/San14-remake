using System.Text.Json.Nodes;

namespace San14.Core;

internal static class ForceFactionSaveFields
{
    internal static void Migrate(JsonObject state, int version)
    {
        if (state["NativeArmySimulation"]?["Forces"] is not JsonObject forces) return;
        foreach (var pair in forces)
        {
            if (pair.Value is not JsonObject force)
                throw new InvalidDataException("Saved native force is invalid.");
            MigratePopulation(force, version);
            const string key = "FactionOfficerId";
            if (version < 164)
            {
                if (force[key] is not null)
                    throw new InvalidDataException("Legacy force contains a future faction officer.");
                force[key] = null;
                continue;
            }
            if (!force.ContainsKey(key))
                throw new InvalidDataException("Saved faction officer knowledge is missing.");
            if (force[key] is JsonNode saved && (saved is not JsonValue field ||
                !field.TryGetValue<int>(out int id) || id is < 0 or > ushort.MaxValue))
                throw new InvalidDataException("Saved faction officer is invalid.");
        }
    }

    private static void MigratePopulation(JsonObject force, int version)
    {
        const string key = "PopulationTotal";
        if (version < 164)
        {
            if (force[key] is not null)
                throw new InvalidDataException("Legacy force contains a future population total.");
            force[key] = null;
            return;
        }
        if (!force.ContainsKey(key))
            throw new InvalidDataException("Saved force population knowledge is missing.");
        if (force[key] is JsonNode saved && (saved is not JsonValue field ||
            !field.TryGetValue<int>(out int amount) || amount < 0))
            throw new InvalidDataException("Saved force population is invalid.");
    }
}
