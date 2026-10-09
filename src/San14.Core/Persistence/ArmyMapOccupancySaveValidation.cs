namespace San14.Core;

/// <summary>Persistent shape and identity checks at world/save boundaries.</summary>
internal static class ArmyMapOccupancySaveValidation
{
    internal static void Validate(GameState state)
    {
        if (state.ArmyMapOccupancy is not { } occupancy)
        {
            if (state.NativeArmyLifecycle?.Slots.Any(slot => slot.OccupancyBindingId is not null) == true)
                throw new InvalidDataException("Map occupancy links have no owner.");
            return;
        }
        if (!Enum.IsDefined(occupancy.DefaultCellKnowledge) || occupancy.Cells is null || occupancy.Bindings is null)
            throw new InvalidDataException("Map occupancy owner is invalid.");
        long size = (long)state.MapWidth * state.MapHeight;
        foreach (var row in occupancy.Cells)
        {
            if (row.Key < 0 || row.Key >= size || row.Value is not { } cell || !Enum.IsDefined(cell.Kind) ||
                (cell.Kind == ArmyMapCellKind.Binding
                    ? cell.BindingId is not int id || id <= 0 || !occupancy.Bindings.ContainsKey(id)
                    : cell.BindingId is not null))
                throw new InvalidDataException("Map occupancy cell is invalid.");
        }
        foreach (var row in occupancy.Bindings)
            if (row.Key <= 0 || row.Value is not { } target || !Enum.IsDefined(target.Kind) ||
                (target.Kind == ArmyOccupancyUnitKind.Unit ? target.UnitId is not >= 0 : target.UnitId is not null))
                throw new InvalidDataException("Map occupancy binding is invalid.");
        var seen = new HashSet<int>();
        foreach (var slot in state.NativeArmyLifecycle?.Slots ?? [])
        {
            if (slot.OccupancyBindingId is not int binding) continue;
            if (!seen.Add(binding) || !occupancy.Bindings.TryGetValue(binding, out var target))
                throw new InvalidDataException("Map occupancy presentation link is invalid.");
            int? current = slot.ActiveUnitId ?? slot.PendingImage?.Id;
            if (current is int unit && (target.Kind != ArmyOccupancyUnitKind.Unit || target.UnitId != unit))
                throw new InvalidDataException("Map occupancy target disagrees with its current unit.");
            if (current is null && (slot.Commander12 == 0 || slot.Status10 == 0) && target.Kind != ArmyOccupancyUnitKind.Vacant)
                throw new InvalidDataException("Reset receiver has a nonvacant map occupancy target.");
        }
    }
}
