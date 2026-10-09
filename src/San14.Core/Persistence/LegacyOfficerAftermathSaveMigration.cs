using System.Text.Json.Nodes;

namespace San14.Core;

/// <summary>Moves historical auxiliaries into independent resentment and disposition
/// knowledge; uninterpreted resource codes are handed to the resource-state codec.</summary>
internal static class LegacyOfficerAftermathSaveMigration
{
    public static IReadOnlyList<LegacyOfficerAftermathKnowledge> Apply(JsonObject state)
    {
        var residual = new List<LegacyOfficerAftermathKnowledge>();
        if (state["Officers"] is not JsonObject officers) return residual;
        foreach ((string key, JsonNode? node) in officers)
        {
            if (node is not JsonObject officer) continue;
            if (officer.ContainsKey("Resentment") || officer.ContainsKey("AllegianceDisposition"))
                throw new InvalidDataException("Historical officer has conflicting aftermath owners.");
            officer["Resentment"] = null;
            officer["AllegianceDisposition"] = null;
            if (officer["NativeDestructionRuntime"] is JsonNode oldNode)
            {
                if (oldNode is not JsonObject old || old["Revision"] is not JsonValue revision ||
                    !revision.TryGetValue<string>(out string? identity) ||
                    identity != "san14:ce-owned-person-destruction-runtime-20261007" ||
                    !old.ContainsKey("Word19A") || !old.ContainsKey("Byte19C") || !old.ContainsKey("Byte19D") ||
                    !old.ContainsKey("Byte1A0") || !old.ContainsKey("Word1A4"))
                    throw new InvalidDataException("Historical officer aftermath snapshot is incomplete.");
                int? target = Scalar(old["Word19A"], 0, ushort.MaxValue);
                int? intensity = Scalar(old["Byte19C"], 0, byte.MaxValue);
                int? previousForce = Scalar(old["Byte19D"], 0, byte.MaxValue);
                int? captureReset = Scalar(old["Byte1A0"], 0, byte.MaxValue);
                int? disposition = Scalar(old["Word1A4"], short.MinValue, short.MaxValue);
                officer["Resentment"] = new JsonObject
                {
                    ["TargetOfficerId"] = target is <= 6000 ? target : null,
                    ["Intensity"] = intensity
                };
                if (disposition is not null)
                    officer["AllegianceDisposition"] = new JsonObject { ["Value"] = disposition };
                if (target is > 6000 || previousForce is not null || captureReset is not null)
                {
                    if (!int.TryParse(key, out int id) || id < 0 || officer["Id"]?.GetValue<int>() != id)
                        throw new InvalidDataException("Historical officer aftermath identity is inconsistent.");
                    residual.Add(new(id, target is > 6000 ? target : null, previousForce, captureReset));
                }
            }
            officer.Remove("NativeDestructionRuntime");
        }
        return residual;
    }

    private static int? Scalar(JsonNode? node, int minimum, int maximum)
    {
        if (node is null) return null;
        if (node is not JsonValue value || !value.TryGetValue<int>(out int scalar) || scalar < minimum || scalar > maximum)
            throw new InvalidDataException("Historical officer aftermath field is outside its resource encoding.");
        return scalar;
    }
}
