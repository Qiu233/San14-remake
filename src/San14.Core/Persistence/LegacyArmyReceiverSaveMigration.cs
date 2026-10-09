using System.Text.Json.Nodes;

namespace San14.Core;

/// <summary>Assigns independent receiver identities to explicit saved legacy
/// source-table rows. It does not infer rows for absent lifecycle state.</summary>
internal static class LegacyArmyReceiverSaveMigration
{
    internal static IReadOnlyDictionary<int, ArmyReceiverId> AssignReceiverIds(JsonArray slots)
    {
        var identities = new Dictionary<int, ArmyReceiverId>();
        int nextReceiverId = 1;
        foreach (var node in slots)
        {
            if (node is not JsonObject slot || slot.ContainsKey("ReceiverId") ||
                slot["PhysicalSlot"] is not JsonValue value || !value.TryGetValue<int>(out var sourceOrdinal) ||
                sourceOrdinal < 0 || identities.ContainsKey(sourceOrdinal))
                throw new InvalidDataException("Legacy army receiver rows are incomplete or contradictory.");
            var receiverId = new ArmyReceiverId(nextReceiverId++);
            identities.Add(sourceOrdinal, receiverId);
            slot["ReceiverId"] = new JsonObject { ["Value"] = receiverId.Value };
        }
        return identities;
    }
}
