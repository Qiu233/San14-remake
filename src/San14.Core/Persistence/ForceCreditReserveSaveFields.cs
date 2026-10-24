using System.Text.Json.Nodes;

namespace San14.Core;

internal static class ForceCreditReserveSaveFields
{
    internal static void Migrate(JsonObject state, int version)
    {
        if (version < 159)
        {
            if (state["ForceCreditReserves"] is not null)
                throw new InvalidDataException("Legacy save contains future force credit reserves.");
            state["ForceCreditReserves"] = null;
            return;
        }

        if (!state.ContainsKey("ForceCreditReserves"))
            throw new InvalidDataException("Saved force credit reserve knowledge is missing.");
        if (state["ForceCreditReserves"] is not JsonNode saved) return;
        if (saved is not JsonObject owner || owner["PointsByForce"] is not JsonObject forces)
            throw new InvalidDataException("Saved force credit reserves are incomplete.");
        foreach (var pair in forces)
        {
            if (!int.TryParse(pair.Key, out int force) || force is < 0 or > 51 ||
                pair.Value is not JsonArray points || points.Count != 6)
                throw new InvalidDataException("Saved force credit reserve vector is invalid.");
            foreach (var point in points)
                if (point is not JsonValue value || !value.TryGetValue<byte>(out _))
                    throw new InvalidDataException("Saved force credit reserve point is invalid.");
        }
    }
}
