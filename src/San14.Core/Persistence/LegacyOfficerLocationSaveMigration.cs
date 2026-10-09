using System.Text.Json.Nodes;

namespace San14.Core;

internal static class LegacyOfficerLocationSaveMigration
{
    public static IReadOnlyList<LegacyOfficerLocationKnowledge> Apply(JsonObject state)
    {
        IReadOnlyDictionary<int, ArmyReceiverId>? receivers = null;
        if (state["NativeArmyLifecycle"] is JsonObject lifecycle && lifecycle["Slots"] is JsonArray slots)
            receivers = LegacyArmyReceiverSaveMigration.AssignReceiverIds(slots);
        var residual = new List<LegacyOfficerLocationKnowledge>();
        if (state["Officers"] is not JsonObject officers) return residual;
        foreach ((string key, JsonNode? node) in officers)
        {
            if (node is not JsonObject officer) continue;
            if (officer.ContainsKey("LocationState")) throw new InvalidDataException("Legacy officer has conflicting position owners.");
            officer["LocationState"] = null;
            if (officer["NativeLocationRuntime"] is JsonNode oldNode)
            {
                if (oldNode is not JsonObject old || old["Revision"] is not JsonValue revision ||
                    !revision.TryGetValue<string>(out string? identity) || string.IsNullOrWhiteSpace(identity) ||
                    !old.ContainsKey("Second11C") || old.ContainsKey("TraitMask1BC") || old.ContainsKey("TraitExtras1BE"))
                    throw new InvalidDataException("Historical officer location snapshot is incomplete or obsolete.");
                int? second = Scalar(old["Second11C"], ushort.MaxValue);
                int? district = Scalar(old["District118"], byte.MaxValue);
                JsonObject? position = second switch
                {
                    0 => Position(OfficerPositionKind.Unplaced, null),
                    >= 1 and <= 51 => Position(OfficerPositionKind.City, second),
                    >= 52 and <= 61 => Position(OfficerPositionKind.Gate, second),
                    >= 562 and <= 1061 => Position(OfficerPositionKind.Area, second - 561),
                    >= 62 and <= 561 when receivers is not null && receivers.TryGetValue(second.Value - 61, out _) =>
                        Position(OfficerPositionKind.ArmyReceiver, receivers[second.Value - 61].Value),
                    _ => null
                };
                officer["LocationState"] = new JsonObject
                {
                    ["Position"] = position ?? (second is null ? null : Position(OfficerPositionKind.Unresolved, null)),
                    ["Membership"] = district is >= 0 and <= 51
                        ? new JsonObject { ["Kind"] = district == 0 ? (int)OfficerDistrictMembershipKind.Unaffiliated : (int)OfficerDistrictMembershipKind.District,
                            ["DistrictId"] = district == 0 ? null : district }
                        : district is null ? null : new JsonObject { ["Kind"] = (int)OfficerDistrictMembershipKind.Unresolved, ["DistrictId"] = null }
                };
                int? unmappedSecond = second is not null && position is null ? second : null;
                int? unmappedDistrict = district is > 51 ? district : null;
                if (unmappedSecond is not null || unmappedDistrict is not null)
                {
                    if (!int.TryParse(key, out int id) || officer["Id"]?.GetValue<int>() != id)
                        throw new InvalidDataException("Historical officer location identity is inconsistent.");
                    residual.Add(new(id, unmappedSecond, unmappedDistrict));
                }
            }
            officer.Remove("NativeLocationRuntime");
        }
        return residual;
    }

    private static JsonObject Position(OfficerPositionKind kind, int? id) => new() { ["Kind"] = (int)kind, ["EntityId"] = id };
    private static int? Scalar(JsonNode? node, int maximum)
    {
        if (node is null) return null;
        if (node is not JsonValue value || !value.TryGetValue<int>(out int scalar) || scalar < 0 || scalar > maximum)
            throw new InvalidDataException("Historical officer location source field is invalid.");
        return scalar;
    }
}
