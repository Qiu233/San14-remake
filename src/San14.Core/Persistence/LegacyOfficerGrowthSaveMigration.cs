using System.Text.Json.Nodes;

namespace San14.Core;

/// <summary>Migrates saved growth knowledge once. Resource identity is checked
/// against the campaign content ledger and then leaves the officer domain.</summary>
internal static class LegacyOfficerGrowthSaveMigration
{
    public static void Apply(JsonObject state)
    {
        if (state["Officers"] is not JsonObject officers) return;
        foreach (JsonNode? node in officers.Select(pair => pair.Value))
        {
            if (node is not JsonObject officer) continue;
            if (officer.ContainsKey("Growth"))
                throw new InvalidDataException("Legacy officer contains conflicting growth owners.");
            officer["Growth"] = null;
            if (officer["NativeGrowthRuntime"] is JsonNode carrier)
            {
                if (carrier is not JsonObject growth ||
                    growth["Revision"] is not JsonValue revision ||
                    !revision.TryGetValue<string>(out string? revisionId) || string.IsNullOrWhiteSpace(revisionId) ||
                    growth["SourceExperienceUri"] is not JsonValue uri || !uri.TryGetValue<string>(out string? resourceUri) ||
                    string.IsNullOrWhiteSpace(resourceUri) ||
                    growth["SourceExperienceSha256"] is not JsonValue hash || !hash.TryGetValue<string>(out string? fingerprint) ||
                    fingerprint is not { Length: 64 } || !fingerprint.All(Uri.IsHexDigit) ||
                    state["ContentHashes"] is not JsonObject hashes ||
                    hashes[resourceUri] is not JsonValue savedHash ||
                    !savedHash.TryGetValue<string>(out string? savedFingerprint) || savedFingerprint != fingerprint ||
                    !growth.ContainsKey("Experience13C") || !growth.ContainsKey("AbilityGrowthNotices") ||
                    growth.ContainsKey("PendingMerit1F8"))
                    throw new InvalidDataException("Legacy officer growth identity or snapshot is invalid.");
                JsonNode? experience = growth["Experience13C"];
                if (experience is not null && (experience is not JsonArray xp || xp.Count != 5 ||
                    xp.Any(value => value is not JsonValue scalar || !scalar.TryGetValue<uint>(out _))))
                    throw new InvalidDataException("Legacy officer experience vector is invalid.");
                JsonArray? notifications = null;
                if (growth["AbilityGrowthNotices"] is JsonNode noticeNode)
                {
                    if (noticeNode is not JsonArray notices || notices.Count > 5)
                        throw new InvalidDataException("Legacy ability growth notices are invalid.");
                    notifications = [];
                    var indices = new HashSet<int>();
                    foreach (JsonNode? noticeNodeValue in notices)
                    {
                        if (noticeNodeValue is not JsonObject notice)
                            throw new InvalidDataException("Legacy ability growth notice is invalid.");
                        int ability = Read(notice, "AbilityIndex", 0, 4);
                        if (!indices.Add(ability)) throw new InvalidDataException("Legacy growth notice identity is duplicated.");
                        notifications.Add(new JsonObject
                        {
                            ["AbilityIndex"] = ability,
                            ["AfterAbility"] = Read(notice, "After4", 0, byte.MaxValue),
                            ["BeforeAbility"] = Read(notice, "Before5", 0, byte.MaxValue),
                            ["ReportContext"] = Read(notice, "ReportContext8", int.MinValue, int.MaxValue),
                            ["UninterpretedCode"] = Read(notice, "RawC", int.MinValue, int.MaxValue),
                            ["UninterpretedState"] = Read(notice, "Raw10", int.MinValue, int.MaxValue)
                        });
                    }
                }
                officer["Growth"] = new JsonObject
                {
                    ["Experience"] = experience?.DeepClone(),
                    ["Notifications"] = notifications
                };
            }
            officer.Remove("NativeGrowthRuntime");
        }
    }

    private static int Read(JsonObject owner, string name, int minimum, int maximum)
    {
        if (owner[name] is not JsonValue value || !value.TryGetValue<int>(out int number) ||
            number < minimum || number > maximum)
            throw new InvalidDataException("Legacy ability growth notice channel is invalid.");
        return number;
    }
}
