using System.Text.Json.Nodes;

namespace San14.Core;

/// <summary>Moves historical location-owned trait fields into their independent owner.</summary>
internal static class LegacyOfficerTraitActivationSaveMigration
{
    public static void Apply(JsonObject state)
    {
        if (state["Officers"] is not JsonObject officers) return;
        foreach (JsonNode? node in officers.Select(pair => pair.Value))
        {
            if (node is not JsonObject officer) continue;
            officer["TraitActivation"] = null;
            if (officer["NativeLocationRuntime"] is not JsonObject location) continue;
            if (!location.ContainsKey("TraitMask1BC") || !location.ContainsKey("TraitExtras1BE"))
                throw new InvalidDataException("Legacy officer activation knowledge is incomplete.");
            JsonArray? slots = null;
            if (location["TraitMask1BC"] is JsonNode maskNode)
            {
                ushort mask = ReadWord(maskNode);
                slots = new();
                for (int slot = 0; slot < 16; slot++) if ((mask & (1 << slot)) != 0) slots.Add(slot);
            }
            JsonArray? durations = null;
            if (location["TraitExtras1BE"] is JsonNode durationNode)
            {
                if (durationNode is not JsonArray oldDurations || oldDurations.Count != 9)
                    throw new InvalidDataException("Legacy officer activation duration vector is invalid.");
                durations = new();
                foreach (JsonNode? duration in oldDurations)
                    durations.Add((int)ReadWord(duration));
            }
            officer["TraitActivation"] = new JsonObject { ["ActiveSlots"] = slots, ["RemainingDurations"] = durations };
            location.Remove("TraitMask1BC");
            location.Remove("TraitExtras1BE");
        }
    }

    private static ushort ReadWord(JsonNode? node) => node is JsonValue value &&
        value.TryGetValue<int>(out int number) && number is >= 0 and <= ushort.MaxValue
        ? (ushort)number : throw new InvalidDataException("Legacy officer activation value is invalid.");
}
