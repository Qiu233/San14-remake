using System.Text.Json.Nodes;

namespace San14.Core;

internal static class LegacyOfficerBehaviorSaveMigration
{
    public static void Apply(JsonObject state)
    {
        if (state["Officers"] is not JsonObject officers) return;
        foreach (JsonNode? node in officers.Select(pair => pair.Value))
        {
            if (node is not JsonObject officer) continue;
            if (officer.ContainsKey("Behavior"))
                throw new InvalidDataException("Legacy officer contains conflicting behavior owners.");
            if (officer["NativeAbilityRuntime"] is not JsonObject ability)
            {
                officer["Behavior"] = null;
                continue;
            }
            int? flags = Read(ability, "Flags196", ushort.MaxValue);
            int? marker = Read(ability, "Extra1FD", byte.MaxValue);
            officer["Behavior"] = new JsonObject
            {
                ["UsesSpecialPersonRules"] = flags is int special ? (special & 0x4000) != 0 : null,
                ["PreservesMaximumLoyalty"] = flags is int loyal ? (loyal & 0x2000) != 0 : null,
                ["ReleaseAfterAiCapture"] = flags is int release ? (release & 0x200) != 0 : null,
                ["UninterpretedFlags"] = flags is int residual ? residual & ~0x6200 : null,
                ["SpecialValidityMarker"] = marker
            };
            ability.Remove("Flags196");
            ability.Remove("Extra1FD");
        }
    }

    private static int? Read(JsonObject ability, string name, int maximum)
    {
        if (!ability.ContainsKey(name)) throw new InvalidDataException("Legacy officer behavior channel is missing.");
        if (ability[name] is null) return null;
        if (ability[name] is not JsonValue value || !value.TryGetValue<int>(out int number) || number < 0 || number > maximum)
            throw new InvalidDataException("Legacy officer behavior channel is invalid.");
        return number;
    }
}
