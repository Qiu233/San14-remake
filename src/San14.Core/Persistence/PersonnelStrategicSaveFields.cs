using System.Text.Json.Nodes;

namespace San14.Core;

internal static class PersonnelStrategicSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version < 164 && (state["PersonnelCommands"]?["Strategies"] is not null ||
            state["PersonnelCommands"]?["Independences"] is not null))
            throw new InvalidDataException("Legacy save contains future strategic personnel tasks.");
    }

    internal static void Migrate(JsonObject state, int version)
    {
        if (state["PersonnelCommands"] is not JsonObject owner) return;
        if (version < 164) { owner["Strategies"] = null; owner["Independences"] = null; return; }
        MigrateIndependences(owner);
        if (!owner.ContainsKey("Strategies"))
            throw new InvalidDataException("Saved strategic personnel task knowledge is missing.");
        if (owner["Strategies"] is not JsonNode saved) return;
        if (saved is not JsonObject tasks) throw new InvalidDataException("Saved strategic tasks are invalid.");
        foreach (var pair in tasks)
        {
            if (!int.TryParse(pair.Key, out int id) || id < 0 || pair.Value is not JsonObject task ||
                !Integer(task, "Id", id, id) || !Integer(task, "DispatchSlot", 0, PersonnelCommandTasks.CompatibleDispatchCapacity - 1) ||
                task["MemberIds"] is not JsonArray members || members.Any(member =>
                    member is not JsonValue field || !field.TryGetValue<int>(out int person) || person < 0) ||
                !Integer(task, "Kind", 0, int.MaxValue) || task["Kind"]!.GetValue<int>() is not (10 or 11 or 12 or 13 or 14 or 20 or 24) ||
                !Integer(task, "ForceId", 0, 51) || !Integer(task, "SourceLocationId", 0, 61) ||
                !Integer(task, "SourceDistrictId", 0, 51) || task["Target"] is not JsonObject target ||
                !Integer(target, "Kind", 0, 3) || !Integer(target, "Id", 0, int.MaxValue) ||
                !NullableInteger(task, "SecondForceId", 0, 51) || !NullableInteger(task, "TargetGovernorId", 0, 6000) ||
                !Integer(task, "TargetOriginalForceId", 0, byte.MaxValue) || !Integer(task, "SourceForceId", 0, byte.MaxValue) ||
                !Integer(task, "StartHexId", 0, 48399) || !Integer(task, "EndHexId", 0, 48399) ||
                !Date(task["StartDate"]) || !Date(task["DueDate"]) || !Integer(task, "SavedResult", -1, int.MaxValue) ||
                !Integer(task, "MilitaryIntelligence", 0, int.MaxValue) || !Integer(task, "IndependenceChoice", int.MinValue, int.MaxValue) ||
                !Boolean(task, "Started") || !Boolean(task, "Cancelled") || !Boolean(task, "IsRetiring") ||
                !Boolean(task, "IsAssociated") || !Boolean(task, "ReportToPlayer"))
                throw new InvalidDataException("Saved strategic personnel task is incomplete or invalid.");
        }
    }

    private static void MigrateIndependences(JsonObject owner)
    {
        if (!owner.ContainsKey("Independences"))
            throw new InvalidDataException("Saved independence personnel task knowledge is missing.");
        if (owner["Independences"] is not JsonNode saved) return;
        if (saved is not JsonObject tasks) throw new InvalidDataException("Saved independence tasks are invalid.");
        foreach (var pair in tasks)
        {
            if (!int.TryParse(pair.Key, out int id) || id < 0 || pair.Value is not JsonObject task ||
                !Integer(task, "Id", id, id) || !Integer(task, "DispatchSlot", 0, PersonnelCommandTasks.CompatibleDispatchCapacity - 1) ||
                task["MemberIds"] is not JsonArray members || members.Any(member =>
                    member is not JsonValue field || !field.TryGetValue<int>(out int person) || person < 0) ||
                !Integer(task, "ForceId", 0, 51) || !Integer(task, "SourceLocationId", 0, 61) ||
                !Integer(task, "SourceDistrictId", 0, 51) || !Integer(task, "TargetCityId", 0, 61) ||
                !Integer(task, "CapturedGovernorId", 0, 6000) ||
                !Integer(task, "TargetOriginalForceId", 0, byte.MaxValue) || !Integer(task, "SourceForceId", 0, byte.MaxValue) ||
                !Integer(task, "StartHexId", 0, 48399) || !Integer(task, "EndHexId", 0, 48399) ||
                !Date(task["StartDate"]) || !Date(task["DueDate"]) || !Integer(task, "SavedResult", -1, int.MaxValue) ||
                !Integer(task, "MilitaryIntelligence", 0, int.MaxValue) || !Boolean(task, "Started") ||
                !Boolean(task, "Cancelled") || !Boolean(task, "IsRetiring") ||
                !Boolean(task, "IsAssociated") || !Boolean(task, "ReportToPlayer"))
                throw new InvalidDataException("Saved independence personnel task is incomplete or invalid.");
        }
    }

    private static bool Date(JsonNode? node) => node is JsonObject date &&
        Integer(date, "Year", short.MinValue, short.MaxValue) && Integer(date, "Month", 1, 12) &&
        Integer(date, "Day", 1, 30) && Integer(date, "Hour", 0, 23);
    private static bool Boolean(JsonObject owner, string name) => owner[name] is JsonValue field && field.TryGetValue<bool>(out _);
    private static bool NullableInteger(JsonObject owner, string name, int minimum, int maximum) =>
        owner.ContainsKey(name) && (owner[name] is null || Integer(owner, name, minimum, maximum));
    private static bool Integer(JsonObject owner, string name, int minimum, int maximum) =>
        owner[name] is JsonValue field && field.TryGetValue<int>(out int value) && value >= minimum && value <= maximum;
}
