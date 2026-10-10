using System.Text.Json.Nodes;

namespace San14.Core;

internal static class LegacyTransportRouteSaveMigration
{
    internal static ICampaignResourceState? Apply(JsonObject stateDocument, ICampaignResourceState? resourceState,
        ICampaignResourceStateCodec? resourceStateCodec)
    {
        IEnumerable<JsonObject> units = stateDocument["Units"] is JsonObject active
            ? active.Select(row => row.Value).OfType<JsonObject>() : [];
        if (stateDocument["ArmyLifecycle"]?["Receivers"]?["Entries"] is JsonArray receivers)
            units = units.Concat(receivers.Select(row => row?["PendingImage"]).OfType<JsonObject>());
        foreach (var unit in units) MigrateTransport(unit, null);
        if (stateDocument["ForceDefeats"] is JsonObject defeats)
            foreach (var defeat in defeats)
                if (defeat.Value?["WithdrawnUnits"] is JsonArray withdrawn)
                    foreach (var unit in withdrawn.OfType<JsonObject>())
                        MigrateTransport(unit, int.Parse(defeat.Key, System.Globalization.CultureInfo.InvariantCulture));
        void MigrateTransport(JsonObject unit, int? defeatedForceId)
        {
            if (unit.ContainsKey("TransportRoute")) throw new InvalidDataException("Historical transport contains a current route owner.");
            if (unit["NativeTransport"] is not null)
                resourceState = (resourceStateCodec ?? throw new InvalidDataException(
                    "Historical transport knowledge requires its resource-state codec.")).MigrateTransportRoute(resourceState, unit, defeatedForceId);
            else unit["TransportRoute"] = null;
            unit.Remove("NativeTransport");
        }
        return resourceState;
    }
}
