using System.Text.Json.Nodes;

namespace San14.Core;

/// <summary>Moves historical receiver-owned units out of source slot images.
/// Only this save boundary interprets the legacy header to import readiness.</summary>
internal static class LegacyArmyReceiverRosterSaveMigration
{
    internal static void Apply(JsonObject state)
    {
        if (state["NativeArmyLifecycle"] is not JsonObject lifecycle) return;
        if (lifecycle.ContainsKey("Receivers") || lifecycle["Slots"] is not JsonArray slots)
            throw new InvalidDataException("Historical army receiver ownership is invalid.");
        var entries = new JsonArray();
        foreach (var node in slots)
        {
            if (node is not JsonObject slot || !slot.ContainsKey("ActiveUnitId") || !slot.ContainsKey("PendingImage") ||
                slot["ReceiverId"] is not JsonObject || slot["PhysicalSlot"] is not JsonValue ordinal ||
                !ordinal.TryGetValue<int>(out int physical) || physical < 0 ||
                !slot.ContainsKey("Commander12") || !slot.ContainsKey("Status10"))
                throw new InvalidDataException("Historical army receiver slot is incomplete.");
            ArmyReceiverPhase phase = slot["ActiveUnitId"] is not null ? ArmyReceiverPhase.Active :
                slot["PendingImage"] is not null ? ArmyReceiverPhase.Pending : EmptyPhase(slot["Commander12"], slot["Status10"]);
            entries.Add(new JsonObject
            {
                ["ReceiverId"] = slot["ReceiverId"]!.DeepClone(), ["CanAssign"] = physical != 0,
                ["Phase"] = (int)phase, ["ActiveUnitId"] = slot["ActiveUnitId"]?.DeepClone(),
                ["PendingImage"] = slot["PendingImage"]?.DeepClone()
            });
            slot.Remove("ActiveUnitId"); slot.Remove("PendingImage");
        }
        lifecycle["Receivers"] = new JsonObject { ["Entries"] = entries };
    }

    private static ArmyReceiverPhase EmptyPhase(JsonNode? commanderNode, JsonNode? statusNode)
    {
        int? commander = Read(commanderNode, ushort.MaxValue), status = Read(statusNode, byte.MaxValue);
        return commander is null || commander != 0 && status is null ? ArmyReceiverPhase.Unknown :
            commander == 0 || status == 0 ? ArmyReceiverPhase.Ready : ArmyReceiverPhase.Reserved;
    }
    private static int? Read(JsonNode? node, int maximum) => node is null ? null :
        node is JsonValue value && value.TryGetValue<int>(out int result) && result >= 0 && result <= maximum
            ? result : throw new InvalidDataException("Historical army header is outside its source format.");
}
