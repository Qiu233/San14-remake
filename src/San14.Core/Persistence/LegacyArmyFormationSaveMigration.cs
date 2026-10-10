using System.Text.Json.Nodes;

namespace San14.Core;

internal static class LegacyArmyFormationSaveMigration
{
    internal static ICampaignResourceState? Apply(JsonObject stateDocument, ICampaignResourceState? resourceState,
        ICampaignResourceStateCodec? resourceStateCodec)
    {
        IEnumerable<JsonObject> units = stateDocument["Units"] is JsonObject active
            ? active.Select(row => row.Value).OfType<JsonObject>() : [];
        if (stateDocument["ArmyLifecycle"]?["Receivers"]?["Entries"] is JsonArray receivers)
            units = units.Concat(receivers.Select(row => row?["PendingImage"]).OfType<JsonObject>());
        foreach (var unit in units) Migrate(unit, null);
        if (stateDocument["ForceDefeats"] is JsonObject defeats)
            foreach (var defeat in defeats)
                if (defeat.Value?["WithdrawnUnits"] is JsonArray withdrawn)
                    foreach (var unit in withdrawn.OfType<JsonObject>())
                        Migrate(unit, int.Parse(defeat.Key, System.Globalization.CultureInfo.InvariantCulture));
        void Migrate(JsonObject unit, int? defeatedForceId)
        {
            if (unit["NativeArmyConfiguration"] is not null)
                resourceState = (resourceStateCodec ?? throw new InvalidDataException(
                    "Historical army configuration requires its resource-state codec.")).MigrateArmyConfiguration(resourceState, unit, defeatedForceId);
            else
            {
                if (unit.ContainsKey("Formations") || unit.ContainsKey("FieldAttackPolicy") || !unit.ContainsKey("FormationId"))
                    throw new InvalidDataException("Historical army contains current or incomplete formation ownership.");
                unit["Formations"] = unit["FormationId"] is null ? null : new JsonObject {
                    ["SelectedLandFormationId"] = unit["FormationId"]!.DeepClone(), ["CurrentLandFormationId"] = null, ["NavalFormationId"] = null };
                unit["FieldAttackPolicy"] = null;
                unit.Remove("FormationId"); unit.Remove("NativeArmyConfiguration");
            }
        }
        return resourceState;
    }
}
