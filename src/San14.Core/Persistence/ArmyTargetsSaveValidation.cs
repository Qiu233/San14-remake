namespace San14.Core;

/// <summary>Persistence shape admission, not a per-operation world scan.</summary>
internal static class ArmyTargetsSaveValidation
{
    public static void Validate(GameState state)
    {
        if (state.ArmyLifecycle is not { Targets: { } targets } arena) return;
        var receivers = arena.Receivers.Entries.Select(row => row.ReceiverId).ToHashSet();
        foreach (var row in targets.Assignments)
        {
            if (!receivers.Contains(row.ReceiverId) || row.Target.Kind is { } kind && !Enum.IsDefined(kind))
                throw new InvalidDataException("Saved army target owner or category is invalid.");
            if (row.Target.Destination is not { } destination) continue;
            bool shape = destination.Kind switch
            {
                ArmyTargetDestinationKind.City => destination.CityId is >= 0 && destination.ReceiverId is null,
                ArmyTargetDestinationKind.ArmyReceiver => destination.CityId is null && destination.ReceiverId is { Value: > 0 } id && receivers.Contains(id),
                ArmyTargetDestinationKind.Unresolved => destination.CityId is null && destination.ReceiverId is null,
                _ => false
            };
            if (!shape) throw new InvalidDataException("Saved army target destination is contradictory.");
            if (destination.Kind == ArmyTargetDestinationKind.City && row.Target.Kind == ArmyTargetKind.ArmyReceiver ||
                destination.Kind == ArmyTargetDestinationKind.ArmyReceiver && row.Target.Kind == ArmyTargetKind.City)
                throw new InvalidDataException("Saved army target category contradicts its destination.");
        }
    }
}
