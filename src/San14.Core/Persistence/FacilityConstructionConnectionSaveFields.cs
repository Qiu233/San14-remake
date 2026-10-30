using System.Text.Json.Nodes;

namespace San14.Core;

internal static class FacilityConstructionConnectionSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version >= 182) return;
        if (Packets(state).Any(packet => packet["AdjacentReceiverId"] is not null ||
            packet["Operation"] is JsonValue operation && operation.TryGetValue<int>(out int kind) &&
            kind >= (int)FacilityConstructionPresentationOperation.ConnectAdjacentFacility))
            throw new InvalidDataException("Legacy world contains future adjacent facility connection.");
    }
    internal static void Migrate(JsonObject state, int version)
    {
        foreach (var packet in Packets(state))
            if (version < 182) packet["AdjacentReceiverId"] = null;
            else if (!packet.ContainsKey("AdjacentReceiverId") || packet["AdjacentReceiverId"] is JsonNode node &&
                (node is not JsonObject receiver || receiver.Count != 1 || receiver["Value"] is not JsonValue value ||
                 !value.TryGetValue<int>(out int id) || id <= 0))
                throw new InvalidDataException("Saved adjacent facility receiver is missing or invalid.");
    }
    private static IEnumerable<JsonObject> Packets(JsonObject state) =>
        ((state["CampaignEffectRequests"]?["PendingBatches"] as JsonArray)?.OfType<JsonObject>()
            .SelectMany(batch => (batch["Requests"] as JsonArray)?.OfType<JsonObject>() ?? []) ?? [])
            .Select(request => request["FacilityConstructionPresentation"]).OfType<JsonObject>();
}
