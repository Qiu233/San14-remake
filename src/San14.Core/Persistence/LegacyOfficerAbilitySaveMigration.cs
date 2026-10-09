using System.Text.Json.Nodes;

namespace San14.Core;

/// <summary>Only historical save migration needs to resolve serialized officer
/// records. Current game state and current saves do not retain this source.</summary>
public interface ILegacyOfficerAbilitySource
{
    int? ResolveRecord(string resourceUri, string sourceSha256, string language, int officerId);
}

/// <summary>Retires the historical source carrier after checking its campaign
/// identity. Slot-dependent bytes become independent domain limits.</summary>
internal static class LegacyOfficerAbilitySaveMigration
{
    public static void Apply(JsonObject state, ILegacyOfficerAbilitySource? source = null)
    {
        if (state["Officers"] is not JsonObject officers) return;
        var records = new HashSet<int>();
        JsonObject? parameters = state["NativeArmySimulation"]?["Parameters"] as JsonObject;
        foreach (JsonNode? node in officers.Select(pair => pair.Value))
        {
            if (node is not JsonObject officer) continue;
            if (officer.ContainsKey("AbilityState"))
                throw new InvalidDataException("Legacy officer contains conflicting ability owners.");
            officer["AbilityState"] = null;
            if (officer["NativeAbilityRuntime"] is JsonNode snapshot)
            {
                if (snapshot is not JsonObject ability || parameters is null ||
                    Text(parameters, "Language") is null || Text(parameters, "RuleVersion") is null ||
                    Text(ability, "Revision") != "san14:ce-owned-person-ability-20261002" ||
                    Text(ability, "Language") != Text(parameters, "Language") ||
                    Text(ability, "RuleVersion") != Text(parameters, "RuleVersion") ||
                    Text(ability, "SourceScenarioUri") is not { Length: > 0 } uri ||
                    Text(state, "ScenarioKey") != uri ||
                    Text(ability, "SourceScenarioSha256") is not { Length: 64 } hash ||
                    !hash.All(Uri.IsHexDigit) || state["ContentHashes"] is not JsonObject ledger ||
                    Text(ledger, uri) != hash ||
                    ability["PhysicalSlot"] is not JsonValue physicalValue ||
                    !physicalValue.TryGetValue<int>(out int record) || record is < 1 or > 1850 ||
                    !records.Add(record) ||
                    ability.ContainsKey("Health199") || ability.ContainsKey("Office11F") ||
                    ability.ContainsKey("Status11E") || ability.ContainsKey("Flags196") ||
                    ability.ContainsKey("Extra1FD") || ability.ContainsKey("Secondary1F4"))
                    throw new InvalidDataException("Legacy officer ability identity or snapshot is invalid.");
                if (source is null)
                    throw new InvalidDataException("Legacy officer ability migration requires its original scenario resource directory.");
                if (officer["Id"] is not JsonValue identity || !identity.TryGetValue<int>(out int officerId) ||
                    source.ResolveRecord(uri, hash, Text(parameters, "Language")!, officerId) != record)
                    throw new InvalidDataException("Legacy officer ability source record is incompatible with its scenario resource.");
                officer["AbilityState"] = new JsonObject
                {
                    ["OrdinaryLimit"] = Decode(ReadByte(ability, "EncodedOrdinaryCap224"), record),
                    ["CharmLimit"] = Decode(ReadByte(ability, "EncodedCharmCap225"), record),
                    ["ObservedDoctrineId"] = ReadByte(ability, "Doctrine184")
                };
            }
            officer.Remove("NativeAbilityRuntime");
        }
    }

    private static string? Text(JsonObject? owner, string field) =>
        owner?[field] is JsonValue value && value.TryGetValue<string>(out string? text) &&
        !string.IsNullOrWhiteSpace(text) ? text : null;

    private static int? ReadByte(JsonObject owner, string name)
    {
        if (!owner.ContainsKey(name)) throw new InvalidDataException("Legacy officer ability channel is missing.");
        if (owner[name] is null) return null;
        if (owner[name] is not JsonValue value || !value.TryGetValue<int>(out int number) || number is < 0 or > 255)
            throw new InvalidDataException("Legacy officer ability channel is invalid.");
        return number;
    }

    private static int? Decode(int? encoded, int record) => encoded is int value
        ? (byte)((byte)((value << 3) | (value >> 5)) ^ (byte)record) : null;
}
