namespace San14.Core;

/// <summary>Persistence shape admission, not a per-operation world scan.</summary>
internal static class ArmyTargetsSaveValidation
{
    public static void Validate(GameState state)
    {
        if (state.ArmyLifecycle is not { } arena) return;
        var receivers = arena.Receivers.Entries.Select(row => row.ReceiverId).ToHashSet();
        foreach (var entry in arena.Receivers.Entries)
            if (entry.SavedCommand?.Command is { } command) ValidateTarget(command.Target);
        foreach (var row in arena.Targets?.Assignments ?? [])
        {
            if (!receivers.Contains(row.ReceiverId))
                throw new InvalidDataException("Saved army target owner or category is invalid.");
            ValidateTarget(row.Target);
        }

        void ValidateTarget(ArmyTargetState target)
        {
            if (target is null || target.Kind is { } kind && !Enum.IsDefined(kind))
                throw new InvalidDataException("Saved army target category is invalid.");
            if (target.Destination is not { } destination) return;
            bool shape = destination.Kind switch
            {
                ArmyTargetDestinationKind.City => destination.GateId is null && destination.CityId is >= 0 && destination.ReceiverId is null && destination.HexId is null && destination.MapObjectReceiverId is null,
                ArmyTargetDestinationKind.ArmyReceiver => destination.GateId is null && destination.CityId is null && destination.HexId is null && destination.MapObjectReceiverId is null && destination.ReceiverId is { Value: > 0 } id && receivers.Contains(id),
                ArmyTargetDestinationKind.UnavailableBase => target.Kind is ArmyTargetKind.City or ArmyTargetKind.Gate &&
                    destination.GateId is null && destination.CityId is null && destination.ReceiverId is null && destination.HexId is null && destination.MapObjectReceiverId is null,
                ArmyTargetDestinationKind.UnavailableObject => target.Kind == ArmyTargetKind.MapObjectReceiver &&
                    destination.GateId is null && destination.CityId is null && destination.ReceiverId is null && destination.HexId is null && destination.MapObjectReceiverId is null,
                ArmyTargetDestinationKind.Unresolved => destination.GateId is null && destination.CityId is null && destination.ReceiverId is null && destination.HexId is null && destination.MapObjectReceiverId is null,
                ArmyTargetDestinationKind.Hex => destination.GateId is null && target.Kind == ArmyTargetKind.Hex && destination.CityId is null && destination.ReceiverId is null && destination.MapObjectReceiverId is null &&
                    destination.HexId is int hex && hex >= 0 && hex < (long)state.MapWidth * state.MapHeight,
                ArmyTargetDestinationKind.MapObjectReceiver => destination.GateId is null && target.Kind == ArmyTargetKind.MapObjectReceiver &&
                    destination.CityId is null && destination.ReceiverId is null && destination.HexId is null &&
                    destination.MapObjectReceiverId is { Value: > 0 } mapObject && state.MapObjects?.Find(mapObject) is not null,
                ArmyTargetDestinationKind.Gate => target.Kind == ArmyTargetKind.Gate &&
                    destination.CityId is null && destination.ReceiverId is null && destination.HexId is null && destination.MapObjectReceiverId is null &&
                    destination.GateId is >= 52 and <= 61 && state.Gates.ContainsKey(destination.GateId.Value),
                _ => false
            };
            if (!shape) throw new InvalidDataException("Saved army target destination is contradictory.");
            if (destination.Kind == ArmyTargetDestinationKind.City && target.Kind == ArmyTargetKind.ArmyReceiver ||
                destination.Kind == ArmyTargetDestinationKind.ArmyReceiver && target.Kind == ArmyTargetKind.City ||
                target.Kind == ArmyTargetKind.Hex && destination.Kind is not (ArmyTargetDestinationKind.Hex or ArmyTargetDestinationKind.Unresolved) ||
                target.Kind == ArmyTargetKind.MapObjectReceiver && destination.Kind is not (ArmyTargetDestinationKind.MapObjectReceiver or ArmyTargetDestinationKind.Unresolved or ArmyTargetDestinationKind.UnavailableObject) ||
                target.Kind == ArmyTargetKind.Gate && destination.Kind is not (ArmyTargetDestinationKind.Gate or ArmyTargetDestinationKind.Unresolved or ArmyTargetDestinationKind.UnavailableBase))
                throw new InvalidDataException("Saved army target category contradicts its destination.");
        }
    }
}
