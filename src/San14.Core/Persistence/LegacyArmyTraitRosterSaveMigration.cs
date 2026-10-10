using System.Text.Json.Nodes;

namespace San14.Core;

internal static class LegacyArmyTraitRosterSaveMigration
{
    internal static ICampaignResourceState? Apply(JsonObject stateDocument, ICampaignResourceState? resourceState,
        ICampaignResourceStateCodec? resourceStateCodec)
    {
        IEnumerable<JsonObject> units = stateDocument["Units"] is JsonObject active
            ? active.Select(row => row.Value).OfType<JsonObject>() : [];
        if (stateDocument["ArmyLifecycle"]?["Receivers"]?["Entries"] is JsonArray receivers)
            units = units.Concat(receivers.Select(row => row?["PendingImage"]).OfType<JsonObject>());
        foreach (var unit in units) MigrateTraits(unit, null);
        if (stateDocument["ForceDefeats"] is JsonObject defeats)
            foreach (var defeat in defeats)
                if (defeat.Value?["WithdrawnUnits"] is JsonArray withdrawn)
                    foreach (var unit in withdrawn.OfType<JsonObject>())
                        MigrateTraits(unit, int.Parse(defeat.Key, System.Globalization.CultureInfo.InvariantCulture));
        void MigrateTraits(JsonObject unit, int? defeatedForceId)
        {
            if (unit.ContainsKey("TraitRoster")) throw new InvalidDataException("Historical army contains a current trait roster.");
            if (unit["NativeArmyConfiguration"] is not null)
                resourceState = (resourceStateCodec ?? throw new InvalidDataException(
                    "Historical army trait knowledge requires its resource-state codec.")).MigrateArmyTraits(resourceState, unit, defeatedForceId);
            else unit["TraitRoster"] = null;
        }
        return resourceState;
    }
}
