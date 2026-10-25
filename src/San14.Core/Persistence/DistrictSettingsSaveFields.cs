using System.Text.Json.Nodes;

namespace San14.Core;

internal static class DistrictSettingsSaveFields
{
    internal static void Migrate(JsonObject state, int version)
    {
        if (state["Districts"] is not JsonObject districts) return;
        foreach (var pair in districts)
        {
            if (pair.Value is not JsonObject district)
                throw new InvalidDataException("Saved district is incomplete.");
            if (version < 161)
            {
                if (district["Settings"] is not null)
                    throw new InvalidDataException("Legacy district contains future delegation settings.");
                district["Settings"] = null;
                continue;
            }
            if (!district.ContainsKey("Settings"))
                throw new InvalidDataException("Saved district setting knowledge is missing.");
            if (district["Settings"] is not JsonNode saved) continue;
            if (saved is not JsonObject settings || !Integer(settings, "PolicyId", 0, 5) ||
                !Integer(settings, "PriorityId", 0, 4) || !settings.ContainsKey("Target") ||
                !Boolean(settings, "RecruitOfficers") || !Boolean(settings, "Attack") ||
                !Boolean(settings, "Transport") || !Boolean(settings, "RecruitTroops") ||
                !Boolean(settings, "Merchant") || !Boolean(settings, "Demolition") ||
                !IdentitySet(settings["AllowedFacilityIds"], 0, 30) ||
                settings["Resources"] is not JsonObject resources ||
                !Integer(resources, "GoldUsageId", 0, 2) || !Integer(resources, "FoodUsageId", 0, 2) ||
                !Integer(resources, "TroopsUsageId", 0, 2) ||
                settings["Military"] is not JsonObject military || !Boolean(military, "Enabled") ||
                !MissionSet(military["AllowedMissionIds"]))
                throw new InvalidDataException("Saved district settings are invalid.");
            if (settings["Target"] is JsonNode target &&
                (target is not JsonObject entity || !Integer(entity, "Kind", 0, 2) ||
                 !Integer(entity, "Id", 1, entity["Kind"]!.GetValue<int>() == (int)DistrictTargetKind.Foothold ? 61 : 51)))
                throw new InvalidDataException("Saved district target is invalid.");
        }
    }

    private static bool Integer(JsonObject owner, string name, int minimum, int maximum) =>
        owner[name] is JsonValue field && field.TryGetValue<int>(out int value) && value >= minimum && value <= maximum;
    private static bool Boolean(JsonObject owner, string name) =>
        owner[name] is JsonValue field && field.TryGetValue<bool>(out _);
    private static bool MissionSet(JsonNode? node) => IdentitySet(node, 0, 7) &&
        ((JsonArray)node!).All(item => item!.GetValue<int>() != 2);
    private static bool IdentitySet(JsonNode? node, int minimum, int maximum)
    {
        if (node is not JsonArray array) return false;
        var ids = new HashSet<int>();
        foreach (var item in array)
            if (item is not JsonValue field || !field.TryGetValue<int>(out int id) ||
                id < minimum || id > maximum || !ids.Add(id)) return false;
        return true;
    }
}
