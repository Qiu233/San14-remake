using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace San14.Core;

/// <summary>Converts the old source group tables once, before deserialization.
/// Free receiver values become readiness, not persistent phantom armies.</summary>
internal static class LegacyArmyGroupsSaveMigration
{
    public static void Apply(JsonObject state)
    {
        if (state["NativeArmyLifecycle"] is not JsonObject lifecycle) return;
        if (lifecycle["Slots"] is not JsonArray slots) throw Missing("army receivers");
        foreach (var node in slots)
            (node as JsonObject ?? throw Missing("army receiver"))["GroupAssignmentReady"] = null;
        if (!lifecycle.ContainsKey("ArmyGroups")) lifecycle["ArmyGroups"] = null;
        if (lifecycle["Auxiliaries"] is not JsonObject auxiliary) return;
        if (lifecycle["ArmyGroups"] is not null) throw Missing("two group owners");
        if (auxiliary["ArmyTroops58"] is not JsonArray assignments ||
            auxiliary["TroopsLeader2C"] is not JsonArray leaders ||
            assignments.Count != slots.Count || leaders.Count != assignments.Count)
            throw Missing("group table shape");
        var groups = new ArmyGroupsState { ResetLeader = Leader(0) };
        foreach (var node in slots)
        {
            var slot = node!.AsObject();
            int physical = slot["PhysicalSlot"]?.GetValue<int>() ?? throw Missing("army source identity");
            if ((uint)physical >= assignments.Count) throw Missing("army source identity");
            int? old = assignments[physical]?.GetValue<int>();
            if (old is < 0 or > ushort.MaxValue) throw Missing("group assignment word");
            slot["GroupAssignmentReady"] = old is int value ? JsonValue.Create(value == 0) : null;
            int? unitId = slot["ActiveUnitId"]?.GetValue<int>() ?? slot["PendingImage"]?["Id"]?.GetValue<int>();
            if (unitId is int unit)
                groups.UnitAssignments.Add(unit, new(old is not null,
                    old is > 0 && old < leaders.Count ? old : null));
        }
        // Group zero is the source's unassigned receiver, not a game group.
        for (int group = 1; group < leaders.Count; group++)
        {
            var old = leaders[group];
            var leader = old is null ? new ArmyGroupLeader(false, ArmyGroupOfficerReference.None) :
                new ArmyGroupLeader(true, Leader(old.GetValue<int>()));
            if (!leader.IsKnown || leader.Officer != groups.ResetLeader) groups.Leaders.Add(group, leader);
        }
        ArmyGroupsSaveValidation.Validate(groups);
        lifecycle["ArmyGroups"] = JsonSerializer.SerializeToNode(groups);
        auxiliary.Remove("ArmyTroops58"); auxiliary.Remove("TroopsLeader2C");

        ArmyGroupOfficerReference Leader(int raw)
        {
            // The old current-definition capture rebuilds its lookup from all
            // scenario characters. Source Person0 is the default receiver;
            // positive IDs absent from that complete catalog have no receiver.
            if (raw is < 0 or > ushort.MaxValue) throw Missing("leader identity word");
            if (raw is 0 or > 6000) return ArmyGroupOfficerReference.Default;
            return state["Officers"] is JsonObject officers && officers.ContainsKey(raw.ToString(CultureInfo.InvariantCulture))
                ? new(ArmyGroupOfficerKind.Officer, raw) : ArmyGroupOfficerReference.None;
        }
    }

    private static InvalidDataException Missing(string field) => new("Legacy army groups cannot be decoded: " + field + ".");
}
