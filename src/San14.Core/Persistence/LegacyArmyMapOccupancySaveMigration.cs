using System.Text.Json;
using System.Text.Json.Nodes;

namespace San14.Core;

/// <summary>Converts the old receiver-index map once at the save boundary.
/// Normal map cells name reusable bindings; their targets are logical units.</summary>
internal static class LegacyArmyMapOccupancySaveMigration
{
    internal static void Apply(JsonObject state)
    {
        if (state.ContainsKey("ArmyMapOccupancy"))
            throw new InvalidDataException("Legacy save contains a second map occupancy owner.");
        if (state["NativeArmyLifecycle"] is not JsonObject lifecycle)
        {
            state["ArmyMapOccupancy"] = null;
            return;
        }
        if (lifecycle["Slots"] is not JsonArray slots)
            throw new InvalidDataException("Legacy occupancy receiver links are missing.");
        var occupancy = new ArmyMapOccupancyState();
        var bindings = new Dictionary<int, int>();
        int nextBinding = 1;
        foreach (JsonNode? node in slots)
        {
            if (node is not JsonObject slot || slot["PhysicalSlot"]?.GetValue<int>() is not int physical || physical < 0)
                throw new InvalidDataException("Legacy occupancy receiver identity is invalid.");
            if (slot.ContainsKey("OccupancyBindingId"))
                throw new InvalidDataException("Legacy save contains a new occupancy link.");
            if (physical == 0) { slot["OccupancyBindingId"] = null; continue; }
            int binding = nextBinding++;
            bindings.Add(physical, binding);
            slot["OccupancyBindingId"] = binding;
            int? unit = slot["ActiveUnitId"]?.GetValue<int>() ?? slot["PendingImage"]?["Id"]?.GetValue<int>();
            var target = unit is int id ? ArmyOccupancyUnitReference.ForUnit(id)
                : slot["Commander12"]?.GetValue<int>() == 0 || slot["Status10"]?.GetValue<int>() == 0
                    ? ArmyOccupancyUnitReference.Vacant : ArmyOccupancyUnitReference.Unknown;
            occupancy.Bindings.Add(binding, target);
        }
        if (!lifecycle.ContainsKey("HexRaw1C"))
            throw new InvalidDataException("Legacy occupancy map is missing.");
        if (lifecycle["HexRaw1C"] is not JsonObject cells)
            throw new InvalidDataException("Legacy occupancy map is invalid.");
        foreach (var row in cells)
        {
            if (!int.TryParse(row.Key, out int hex) || hex < 0)
                throw new InvalidDataException("Legacy occupancy hex identity is invalid.");
            ArmyMapCell cell;
            if (row.Value is null) cell = ArmyMapCell.Unknown;
            else
            {
                int word = row.Value.GetValue<int>();
                if (word is < 0 or > ushort.MaxValue)
                    throw new InvalidDataException("Legacy occupancy word is invalid.");
                if (unchecked((short)word) < 0) cell = ArmyMapCell.UnsupportedTargetNegative;
                else if (word is >= 1 and <= 500)
                {
                    if (!bindings.TryGetValue(word, out int binding))
                        throw new InvalidDataException("Legacy occupancy receiver is missing.");
                    cell = ArmyMapCell.ForBinding(binding);
                }
                else cell = ArmyMapCell.Empty;
            }
            occupancy.Cells.Add(hex, cell);
        }
        state["ArmyMapOccupancy"] = JsonSerializer.SerializeToNode(occupancy);
        lifecycle.Remove("HexRaw1C");
    }
}
