using System.Text.Json.Nodes;

namespace San14.Core;

internal static class LegacyOfficerMeritSaveMigration
{
    public static void Apply(JsonObject state)
    {
        if (state["Officers"] is not JsonObject officers) return;
        foreach (JsonNode? node in officers.Select(pair => pair.Value))
        {
            if (node is not JsonObject officer) continue;
            if (officer.ContainsKey("MeritState"))
                throw new InvalidDataException("Legacy officer contains conflicting merit owners.");
            JsonObject? credit = Module(officer, "NativeCreditRuntime");
            JsonObject? growth = Module(officer, "NativeGrowthRuntime");
            JsonObject? ability = Module(officer, "NativeAbilityRuntime");
            if (credit is not null && (credit["Revision"] is not JsonValue revision ||
                !revision.TryGetValue<string>(out string? value) || string.IsNullOrWhiteSpace(value)))
                throw new InvalidDataException("Legacy officer credit revision is invalid.");
            officer["MeritState"] = credit is null && growth is null && ability is null ? null : new JsonObject
            {
                ["Merit"] = Read(credit, "Merit1F6", short.MinValue, short.MaxValue),
                ["PendingMerit"] = Read(growth, "PendingMerit1F8", 0, ushort.MaxValue),
                ["RankStageId"] = Read(ability, "Secondary1F4", 0, byte.MaxValue),
                ["FirstSelectionCount"] = Read(credit, "FirstRewardCount1FA", 0, byte.MaxValue),
                ["SecondSelectionCount"] = Read(credit, "SecondRewardCount1FB", 0, byte.MaxValue),
                ["ResetCode"] = Read(credit, "Reset1FC", 0, byte.MaxValue)
            };
            officer.Remove("NativeCreditRuntime");
            growth?.Remove("PendingMerit1F8");
            ability?.Remove("Secondary1F4");
        }
    }

    private static JsonObject? Module(JsonObject officer, string name) => officer[name] switch
    {
        null => null,
        JsonObject value => value,
        _ => throw new InvalidDataException("Legacy officer merit carrier is invalid.")
    };

    private static int? Read(JsonObject? owner, string name, int minimum, int maximum)
    {
        if (owner is null) return null;
        if (!owner.ContainsKey(name)) throw new InvalidDataException("Legacy officer merit channel is missing.");
        if (owner[name] is null) return null;
        if (owner[name] is not JsonValue value || !value.TryGetValue<int>(out int number) ||
            number < minimum || number > maximum)
            throw new InvalidDataException("Legacy officer merit channel is invalid.");
        return number;
    }
}
