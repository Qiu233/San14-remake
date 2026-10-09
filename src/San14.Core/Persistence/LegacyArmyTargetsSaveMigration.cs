using System.Text.Json.Nodes;

namespace San14.Core;

/// <summary>Recognizes the historical physical table once. Current targets
/// retain receiver identities and never derive them from a unit identity.</summary>
internal static class LegacyArmyTargetsSaveMigration
{
    public static IReadOnlyList<LegacyArmyTargetKnowledge> Apply(JsonObject state)
    {
        var residues = new List<LegacyArmyTargetKnowledge>();
        if (state["NativeArmyLifecycle"] is not JsonObject arena) return residues;
        if (arena.ContainsKey("Targets")) throw new InvalidDataException("Legacy army targets contain conflicting owners.");
        arena["Targets"] = null;
        if (arena["Targets38"] is JsonNode oldNode)
        {
            if (oldNode is not JsonArray old || old.Count != 501 || arena["Slots"] is not JsonArray slots || slots.Count != 501)
                throw new InvalidDataException("Historical army target table is incomplete.");
            var receivers = new Dictionary<int, int>();
            foreach (var node in slots)
            {
                if (node is not JsonObject row || row["PhysicalSlot"] is not JsonValue physical ||
                    !physical.TryGetValue<int>(out int ordinal) || row["ReceiverId"]?["Value"] is not JsonValue identity ||
                    !identity.TryGetValue<int>(out int receiver) || receiver <= 0)
                    throw new InvalidDataException("Historical army target receiver directory is incomplete.");
                receivers.Add(ordinal, receiver);
            }
            var assignments = new JsonArray();
            for (int i = 0; i < old.Count; i++)
            {
                if (old[i] is not JsonObject row || !row.ContainsKey("Kind38") || !row.ContainsKey("Identity3A") || !receivers.TryGetValue(i, out int receiver))
                    throw new InvalidDataException("Historical army target row is incomplete.");
                int? rawKind = Read(row["Kind38"], byte.MaxValue), rawIdentity = Read(row["Identity3A"], ushort.MaxValue);
                ArmyTargetKind? kind = rawKind switch { null => null, 255 => ArmyTargetKind.None, 5 => ArmyTargetKind.City, 27 => ArmyTargetKind.ArmyReceiver, _ => ArmyTargetKind.Unresolved };
                JsonObject? destination = null;
                bool unresolved = false;
                if (rawIdentity is int code)
                {
                    ArmyTargetDestinationKind destinationKind = ArmyTargetDestinationKind.Unresolved;
                    int? cityId = null, targetReceiver = null;
                    if (kind == ArmyTargetKind.City && code is >= 1 and <= 51) { destinationKind = ArmyTargetDestinationKind.City; cityId = code; }
                    else if (kind == ArmyTargetKind.ArmyReceiver && receivers.TryGetValue(code, out int resolved))
                    { destinationKind = ArmyTargetDestinationKind.ArmyReceiver; targetReceiver = resolved; }
                    else unresolved = true;
                    destination = new() { ["Kind"] = (int)destinationKind, ["CityId"] = cityId,
                        ["ReceiverId"] = targetReceiver is int target ? new JsonObject { ["Value"] = target } : null };
                }
                assignments.Add(new JsonObject { ["ReceiverId"] = new JsonObject { ["Value"] = receiver },
                    ["Target"] = new JsonObject { ["Kind"] = kind is null ? null : (int)kind, ["Destination"] = destination } });
                if (kind == ArmyTargetKind.Unresolved || unresolved)
                    residues.Add(new(new(receiver), kind == ArmyTargetKind.Unresolved ? rawKind : null, unresolved ? rawIdentity : null));
            }
            arena["Targets"] = new JsonObject { ["Assignments"] = assignments };
        }
        arena.Remove("Targets38");
        return residues;
    }
    private static int? Read(JsonNode? node, int maximum) => node is null ? null :
        node is JsonValue value && value.TryGetValue<int>(out int number) && number >= 0 && number <= maximum ? number :
        throw new InvalidDataException("Historical army target code is outside its encoding.");
}
