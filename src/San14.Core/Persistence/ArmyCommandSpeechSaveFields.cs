using System.Text.Json.Nodes;

namespace San14.Core;

internal static class ArmyCommandSpeechSaveFields
{
    private static IEnumerable<JsonObject> Packets(JsonObject state) =>
        ((state["CampaignEffectRequests"]?["PendingBatches"] as JsonArray)?.OfType<JsonObject>()
            .SelectMany(batch => (batch["Requests"] as JsonArray)?.OfType<JsonObject>() ?? []) ?? [])
            .Select(request => request["Speech"]).OfType<JsonObject>();

    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version >= 186) return;
        var oldPackets = ((state["NormalCombatRefresh"]?["PendingEffects"] as JsonArray)?.OfType<JsonObject>()
            .SelectMany(batch => (batch["Effects"] as JsonArray)?.OfType<JsonObject>() ?? []) ?? [])
            .Select(request => request["Speech"]).OfType<JsonObject>();
        if (Packets(state).Concat(oldPackets).Any(packet => packet["CommandOwnedLayout"] is not null))
            throw new InvalidDataException("Legacy world contains a future Army command speech layout.");
    }

    internal static void Migrate(JsonObject state, int version)
    {
        foreach (var packet in Packets(state))
        {
            if (version < 186)
            {
                packet["CommandOwnedLayout"] = null;
                continue;
            }
            if (!packet.ContainsKey("CommandOwnedLayout")) throw Invalid();
            if (packet["CommandOwnedLayout"] is not JsonNode saved) continue;
            if (saved is not JsonObject layout || layout.Count != 2 ||
                !layout.ContainsKey("AppearanceIndex") || !layout.ContainsKey("ForceColor") ||
                packet["Event"] is not JsonValue eventValue || !eventValue.TryGetValue<int>(out int speechEvent) ||
                speechEvent != (int)CombatSpeechEvent.ArmyEnteredBase &&
                speechEvent != (int)CombatSpeechEvent.ArmyDestinationReachedHolding ||
                (layout["AppearanceIndex"] is null
                    ? layout["ForceColor"] is not null
                    : !Integer(layout["AppearanceIndex"], ushort.MaxValue) || !Integer(layout["ForceColor"], byte.MaxValue)))
                throw Invalid();
        }
    }

    private static bool Integer(JsonNode? node, int maximum) => node is JsonValue value &&
        value.TryGetValue<int>(out int number) && number >= 0 && number <= maximum;

    private static InvalidDataException Invalid() =>
        new("Saved Army command speech layout is missing or invalid.");
}
