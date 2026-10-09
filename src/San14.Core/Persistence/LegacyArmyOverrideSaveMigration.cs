using System.Text.Json.Nodes;

namespace San14.Core;

/// <summary>Collapses the historical duplicate category at the save boundary.
/// Source ordinals are interpreted only through the saved receiver directory.</summary>
internal static class LegacyArmyOverrideSaveMigration
{
    public static IReadOnlyList<LegacyArmyOverrideKnowledge> Apply(JsonObject state)
    {
        var residues = new List<LegacyArmyOverrideKnowledge>();
        var receivers = new Dictionary<int, ArmyReceiverId>();
        if (state["NativeArmyLifecycle"]?["Slots"] is JsonArray slots)
            foreach (var node in slots)
            {
                if (node is not JsonObject slot || slot["PhysicalSlot"] is not JsonValue ordinalValue ||
                    !ordinalValue.TryGetValue<int>(out int ordinal) || slot["ReceiverId"]?["Value"] is not JsonValue idValue ||
                    !idValue.TryGetValue<int>(out int id) || id <= 0)
                    throw new InvalidDataException("Historical override receiver directory is invalid.");
                receivers.Add(ordinal, new(id));
            }
        if (state["Units"] is JsonObject units)
            foreach (var node in units.Select(item => item.Value))
                if (node is JsonObject unit) Unit(unit);
        if (state["NativeArmyLifecycle"]?["Slots"] is JsonArray rows)
            foreach (var node in rows)
            {
                var slot = (JsonObject)node!;
                int ordinal = slot["PhysicalSlot"]!.GetValue<int>();
                slot["OverrideTarget"] = Convert(slot["OverrideTarget"], receivers[ordinal], null, null);
                if (slot["PendingImage"] is JsonObject pending) Unit(pending);
            }
        return residues;

        void Unit(JsonObject unit)
        {
            if (unit.ContainsKey("OverrideTarget")) throw new InvalidDataException("Legacy army override has conflicting owners.");
            int unitId = unit["Id"]!.GetValue<int>();
            ArmyReceiverId? receiver = unit["NativeArmyOrigin"]?["NativeSlotIndex"] is JsonValue physical &&
                physical.TryGetValue<int>(out int ordinal) && receivers.TryGetValue(ordinal, out var linked) ? linked : null;
            int? marchKind = null;
            if (unit["NativeMarch"] is JsonObject march)
            {
                if (!march.ContainsKey("OverrideTargetType3C")) throw new InvalidDataException("Historical march category is missing.");
                marchKind = Read(march["OverrideTargetType3C"], byte.MaxValue);
                if (marchKind is null) throw new InvalidDataException("Historical march category is invalid.");
                march.Remove("OverrideTargetType3C");
            }
            unit["OverrideTarget"] = Convert(unit["NativeOverrideTarget"], receiver, unitId, marchKind);
            unit.Remove("NativeOverrideTarget");
        }
        JsonObject? Convert(JsonNode? node, ArmyReceiverId? ownerReceiver, int? unitId, int? marchKind)
        {
            int? rawKind = null, identity = null;
            if (node is not null)
            {
                if (node is not JsonObject old || !old.ContainsKey("Kind3C") || !old.ContainsKey("Identity3E"))
                    throw new InvalidDataException("Historical army override is incomplete.");
                rawKind = Read(old["Kind3C"], byte.MaxValue); identity = Read(old["Identity3E"], ushort.MaxValue);
            }
            // The transport retarget writer historically updated the override
            // without refreshing the march projection. The real override wins;
            // a march category only contributes knowledge missing from it.
            rawKind ??= marchKind;
            if (node is null && rawKind is null) return null;
            ArmyOverrideTargetKind? kind = rawKind switch
            {
                null => null, 255 => ArmyOverrideTargetKind.None, 5 => ArmyOverrideTargetKind.City,
                6 => ArmyOverrideTargetKind.Gate, 9 => ArmyOverrideTargetKind.Hex,
                11 => ArmyOverrideTargetKind.Object, 27 => ArmyOverrideTargetKind.ArmyReceiver,
                _ => ArmyOverrideTargetKind.Unresolved
            };
            JsonObject? destination = null;
            bool unresolved = false;
            if (identity is int value)
            {
                ArmyOverrideDestinationKind destinationKind;
                int? entity = null; ArmyReceiverId? targetReceiver = null;
                if (kind == ArmyOverrideTargetKind.None && value == 0) destinationKind = ArmyOverrideDestinationKind.None;
                else if (kind == ArmyOverrideTargetKind.City && value is >= 1 and <= 51)
                { destinationKind = ArmyOverrideDestinationKind.City; entity = value; }
                else if (kind == ArmyOverrideTargetKind.Gate && value is >= 1 and <= 10)
                { destinationKind = ArmyOverrideDestinationKind.Gate; entity = value + 51; }
                else if (kind == ArmyOverrideTargetKind.Hex)
                { destinationKind = ArmyOverrideDestinationKind.Hex; entity = value; }
                else if (kind == ArmyOverrideTargetKind.ArmyReceiver && receivers.TryGetValue(value, out var target))
                { destinationKind = ArmyOverrideDestinationKind.ArmyReceiver; targetReceiver = target; }
                else { destinationKind = ArmyOverrideDestinationKind.Unresolved; unresolved = true; }
                destination = new() { ["Kind"] = (int)destinationKind, ["EntityId"] = entity,
                    ["ReceiverId"] = targetReceiver is { } receiver ? new JsonObject { ["Value"] = receiver.Value } : null };
            }
            if (kind == ArmyOverrideTargetKind.Unresolved || unresolved)
                residues.Add(new(ownerReceiver, unitId, kind == ArmyOverrideTargetKind.Unresolved ? rawKind : null,
                    unresolved ? identity : null));
            return new() { ["Kind"] = kind is null ? null : (int)kind, ["Destination"] = destination };
        }
    }
    private static int? Read(JsonNode? node, int maximum) => node is null ? null :
        node is JsonValue value && value.TryGetValue<int>(out int number) && number >= 0 && number <= maximum ? number :
        throw new InvalidDataException("Historical army override is outside its encoding.");
}
