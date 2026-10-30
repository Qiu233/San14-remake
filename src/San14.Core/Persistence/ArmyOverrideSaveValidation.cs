namespace San14.Core;

/// <summary>Checks persisted domain references once at the save boundary.</summary>
internal static class ArmyOverrideSaveValidation
{
    public static void Validate(GameState state)
    {
        var receivers = state.ArmyLifecycle?.Receivers.Entries.Select(slot => slot.ReceiverId).ToHashSet() ?? [];
        foreach (var unit in state.Units.Values) Target(unit.OverrideTarget);
        foreach (var slot in state.ArmyLifecycle?.Receivers.Entries ?? [])
        {
            if (slot.PendingImage is { } pending) Target(pending.OverrideTarget);
        }
        foreach (var slot in state.ArmyLifecycle?.Receivers.Entries ?? []) Target(slot.OverrideTarget);
        void Target(ArmyOverrideTargetState? target)
        {
            if (target is null) return;
            if (target.Kind is { } kind && !Enum.IsDefined(kind)) throw new InvalidDataException("Saved override category is invalid.");
            if (target.Destination is not { } destination) return;
            bool shape = destination.Kind switch
            {
                ArmyOverrideDestinationKind.None or ArmyOverrideDestinationKind.Unresolved => destination.EntityId is null && destination.ReceiverId is null && destination.MapObjectReceiverId is null,
                ArmyOverrideDestinationKind.City or ArmyOverrideDestinationKind.Gate or ArmyOverrideDestinationKind.Hex => destination.EntityId is >= 0 && destination.ReceiverId is null && destination.MapObjectReceiverId is null,
                ArmyOverrideDestinationKind.ArmyReceiver => destination.EntityId is null && destination.MapObjectReceiverId is null && destination.ReceiverId is { Value: > 0 } id && receivers.Contains(id),
                ArmyOverrideDestinationKind.MapObjectReceiver => target.Kind == ArmyOverrideTargetKind.Object && destination.EntityId is null && destination.ReceiverId is null &&
                    destination.MapObjectReceiverId is { Value: > 0 } mapObject && state.MapObjects?.Find(mapObject) is not null,
                _ => false
            };
            if (!shape) throw new InvalidDataException("Saved override destination is invalid.");
            if (target.Kind is null || destination.Kind == ArmyOverrideDestinationKind.Unresolved) return;
            var expected = target.Kind switch
            {
                ArmyOverrideTargetKind.None => ArmyOverrideDestinationKind.None,
                ArmyOverrideTargetKind.City => ArmyOverrideDestinationKind.City,
                ArmyOverrideTargetKind.Gate => ArmyOverrideDestinationKind.Gate,
                ArmyOverrideTargetKind.Hex => ArmyOverrideDestinationKind.Hex,
                ArmyOverrideTargetKind.ArmyReceiver => ArmyOverrideDestinationKind.ArmyReceiver,
                ArmyOverrideTargetKind.Object => ArmyOverrideDestinationKind.MapObjectReceiver,
                _ => ArmyOverrideDestinationKind.Unresolved
            };
            if (destination.Kind != expected) throw new InvalidDataException("Saved override category and destination disagree.");
        }
    }
}
