using System.Text.Json.Nodes;

namespace San14.Core;

internal static class AreaCitySaveFields
{
    internal static void Migrate(JsonObject state, int version)
    {
        if (state["Areas"] is not JsonObject areas) return;
        foreach (var pair in areas)
        {
            if (pair.Value is not JsonObject area || !area.ContainsKey("CityId"))
                throw new InvalidDataException("Saved area city assignment is missing.");
            if (area["CityId"] is null)
            {
                if (version < 165) throw new InvalidDataException("Legacy area contains a future unassigned city.");
            }
            else if (area["CityId"] is not JsonValue value || !value.TryGetValue<int>(out int id) || id <= 0)
                throw new InvalidDataException("Saved area city assignment is invalid.");
        }
    }
}
