namespace San14.Core;

/// <summary>Persistence identity and shape admission, not a derived runtime-world rebuild.</summary>
internal static class MapObjectLifecycleSaveValidation
{
    public static void Validate(GameState state)
    {
        var indexes = state.ArmyLifecycle?.Indexes;
        var pending = indexes?.PendingObjectRetirements;
        if (state.MapObjects is not { } owner)
        {
            if (pending is not null) throw new InvalidDataException("Map object retirement queue has no lifecycle owner.");
            return;
        }
        if (owner.Receivers is null) throw new InvalidDataException("Saved map object receivers are missing.");
        var ids = new HashSet<MapObjectReceiverId>();
        var entities = new HashSet<(MapObjectCategory Category, int Id)>();
        foreach (var row in owner.Receivers)
        {
            if (row is null || row.Id.Value <= 0 || !ids.Add(row.Id))
                throw new InvalidDataException("Saved map object receiver identity is invalid or duplicated.");
            if (row.Pose is { } pose && (!Enum.IsDefined(pose.Kind) || (pose.Kind == MapObjectPoseKind.Hex
                ? pose.HexId is not int hex || hex < 0 || hex >= (long)state.MapWidth * state.MapHeight
                : pose.HexId is not null)))
                throw new InvalidDataException("Saved map object pose is contradictory.");
            if (row.BaseMapFaction is { } faction && (!Enum.IsDefined(faction.Kind) ||
                (faction.Kind == MapObjectFactionKind.Force
                    ? faction.ForceId is not int force || force <= 0 || !state.Forces.ContainsKey(force)
                    : faction.ForceId is not null)))
                throw new InvalidDataException("Saved Base map faction is contradictory.");
            if (row.Subject is not { } subject)
            {
                if (row.BaseMapFaction is not null) throw new InvalidDataException("An empty map receiver cannot own Base faction.");
                continue;
            }
            if (!Enum.IsDefined(subject.Category) || string.IsNullOrWhiteSpace(subject.DefinitionKey))
                throw new InvalidDataException("Saved map object catalog subject is invalid.");
            bool isBase = subject.Category is MapObjectCategory.City or MapObjectCategory.Gate;
            if (isBase)
            {
                if (subject.StructureId is not null || subject.LocationId is not int location || location < 0 ||
                    (subject.Category == MapObjectCategory.City ? !state.Cities.ContainsKey(location) : !state.Gates.ContainsKey(location)) ||
                    !entities.Add((subject.Category, location)))
                    throw new InvalidDataException("Saved Base map object subject is invalid or duplicated.");
            }
            else if (subject.LocationId is not null || subject.StructureId is not int structure || structure < 0 ||
                state.Structures is not { } structures || !structures.TryGetValue(structure, out var entity) || entity.KindId != subject.DefinitionKey ||
                !entities.Add((MapObjectCategory.Facility, structure)) || row.BaseMapFaction is not null)
                throw new InvalidDataException("Saved non-Base map object subject is invalid or duplicated.");
        }
        if (owner.HexBindings is { } bindings && (bindings.TileCount <= 0 ||
            bindings.TileCount != (long)state.MapWidth * state.MapHeight || bindings.Occupied is null ||
            bindings.Occupied.Any(row => row.Key < 0 || row.Key >= bindings.TileCount || !ids.Contains(row.Value))))
            throw new InvalidDataException("Saved map object Hex bindings are invalid.");
        var queued = new HashSet<MapObjectReceiverId>();
        foreach (var receiver in pending ?? [])
            if (receiver is { } id && (!ids.Contains(id) || !queued.Add(id)))
                throw new InvalidDataException("Saved map object retirement identity is invalid or duplicated.");
        if (indexes?.Registries.TryGetValue(WorldEntityKind.MapObject, out var registry) == true)
            foreach (var reference in registry ?? [])
                if (reference is null || reference.Kind != WorldEntityKind.MapObject ||
                    reference.Id is int id && !ids.Contains(new(id)))
                    throw new InvalidDataException("Saved map object registry reference is invalid.");
    }
}
