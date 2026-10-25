using System.Text.Json.Nodes;

namespace San14.Core;

internal static class CoalitionCooldownSaveFields
{
    internal static void Migrate(JsonObject state, int version)
    {
        if (state["NativeArmySimulation"]?["World"] is not JsonObject world) return;
        const string key = "PlayerCoalitionCooldownDate";
        if (version < 164)
        {
            if (world[key] is not null)
                throw new InvalidDataException("Legacy world contains a future player coalition cooldown.");
            world[key] = null;
            return;
        }
        if (!world.ContainsKey(key))
            throw new InvalidDataException("Saved player coalition cooldown knowledge is missing.");
        if (world[key] is not JsonNode saved) return;
        if (saved is not JsonObject date || !Integer(date, "Year", short.MinValue, short.MaxValue) ||
            !Integer(date, "Month", 1, 12) || !Integer(date, "Day", 1, 30) || !Integer(date, "Hour", 0, 23))
            throw new InvalidDataException("Saved player coalition cooldown date is invalid.");
    }

    private static bool Integer(JsonObject owner, string name, int minimum, int maximum) =>
        owner[name] is JsonValue field && field.TryGetValue<int>(out int value) && value >= minimum && value <= maximum;
}
