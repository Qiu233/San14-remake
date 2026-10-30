using System.Text.Json.Nodes;

namespace San14.Core;

internal static class FacilityArmySpeechSaveFields
{
    private static IEnumerable<JsonObject> Packets(JsonObject state) =>
        ((state["CampaignEffectRequests"]?["PendingBatches"] as JsonArray)?.OfType<JsonObject>()
            .SelectMany(batch => (batch["Requests"] as JsonArray)?.OfType<JsonObject>() ?? []) ?? [])
            .Select(request => request["Speech"]).OfType<JsonObject>();

    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version < 183 && Packets(state).Any(packet => packet["FacilityArmySlot"] is not null))
            throw new InvalidDataException("Legacy world contains a future facility speech Army operand.");
    }
    internal static void Migrate(JsonObject state, int version)
    {
        foreach (var packet in Packets(state))
            if (version < 183) packet["FacilityArmySlot"] = null;
            else if (!packet.ContainsKey("FacilityArmySlot") || packet["FacilityArmySlot"] is JsonNode node &&
                (node is not JsonValue value || !value.TryGetValue<int>(out int slot) || slot is < 0 or > 500))
                throw new InvalidDataException("Saved facility speech Army operand is missing or invalid.");
    }
}
