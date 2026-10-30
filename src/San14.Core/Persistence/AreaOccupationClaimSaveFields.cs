using System.Text.Json.Nodes;

namespace San14.Core;

internal static class AreaOccupationClaimSaveFields
{
    internal static void Migrate(JsonObject state, int version)
    {
        if (state["Areas"] is not JsonObject areas) return;
        const string key = "OccupationClaimForceSlot";
        foreach (var entry in areas)
        {
            if (entry.Value is not JsonObject area) throw new InvalidDataException("Saved Area is not an object.");
            if (version < 207)
            {
                if (area[key] is not null) throw new InvalidDataException("Legacy Area contains a future occupation claim.");
                area[key] = null;
            }
            else if (!area.ContainsKey(key) || area[key] is JsonNode claim &&
                (claim is not JsonValue value || !value.TryGetValue<int>(out int raw) || raw is < 0 or > 255))
                throw new InvalidDataException("Saved Area occupation claim knowledge is invalid.");
        }
    }
}
