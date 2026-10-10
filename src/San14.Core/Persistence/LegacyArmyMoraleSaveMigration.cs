using System.Text.Json.Nodes;

namespace San14.Core;

internal static class LegacyArmyMoraleSaveMigration
{
    internal static void Apply(JsonObject state, ICampaignResourceStateCodec? codec)
    {
        IEnumerable<JsonObject> units = state["Units"] is JsonObject active
            ? active.Select(row => row.Value).OfType<JsonObject>() : [];
        if (state["ArmyLifecycle"]?["Receivers"]?["Entries"] is JsonArray receivers)
            units = units.Concat(receivers.Select(row => row?["PendingImage"]).OfType<JsonObject>());
        if (state["ForceDefeats"] is JsonObject defeats)
            units = units.Concat(defeats.SelectMany(row => row.Value?["WithdrawnUnits"] is JsonArray withdrawn
                ? withdrawn.OfType<JsonObject>() : []));
        foreach (var unit in units)
        {
            if (unit["NativeMorale1A"] is not null)
                (codec ?? throw new InvalidDataException("Historical source morale requires its resource-state codec.")).MigrateArmyMorale(state, unit);
            else
            {
                if (unit.ContainsKey("MoraleState") || unit["Morale"] is not JsonValue provisional ||
                    !provisional.TryGetValue<int>(out int value))
                    throw new InvalidDataException("Historical army provisional morale is invalid.");
                unit["MoraleState"] = new JsonObject { ["ProvisionalValue"] = value, ["CurrentValue"] = null };
                unit.Remove("Morale"); unit.Remove("NativeMorale1A");
            }
        }
    }
}
