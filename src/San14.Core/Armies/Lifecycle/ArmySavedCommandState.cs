namespace San14.Core;

public enum ArmySavedCommandKind { Cleared, Stored }

/// <summary>An action identity in the game's ordinary command catalogue.</summary>
public sealed record ArmyCommandAction(int Id);
public sealed record ArmyCommandSnapshot(ArmyCommandAction Action, ArmyTargetState Target);

/// <summary>The complete command temporarily replaced by another order.
/// Null is unknown. Cleared has no snapshot; Stored always has one.</summary>
public sealed record ArmySavedCommandState(ArmySavedCommandKind Kind, ArmyCommandSnapshot? Command)
{
    public static ArmySavedCommandState Cleared { get; } = new(ArmySavedCommandKind.Cleared, null);
    public static ArmySavedCommandState Store(ArmyCommandSnapshot command) => new(ArmySavedCommandKind.Stored, command);
    [System.Text.Json.Serialization.JsonIgnore]
    public byte Control => Kind == ArmySavedCommandKind.Cleared ? (byte)0 : checked((byte)Command!.Action.Id);
}

public static class ArmySavedCommandKnowledge
{
    /// <summary>The receiver is the authority. An old image scalar remains
    /// explicit compatibility knowledge only when its complete owner is absent.</summary>
    public static byte? ReadControl(GameState state, UnitState image) =>
        state.ArmyLifecycle?.Receivers.FindUnit(image.Id)?.SavedCommand?.Control ?? image.NativeCommandControl140;

    internal static void Validate(ArmySavedCommandState? value, long mapSize,
        IReadOnlySet<ArmyReceiverId> receivers)
    {
        if (value is null) return;
        if (value.Kind == ArmySavedCommandKind.Cleared && value.Command is null) return;
        if (value.Kind != ArmySavedCommandKind.Stored || value.Command is not { Action.Id: >= 1 and <= 17 } command ||
            !CompleteTarget(command.Target, mapSize, receivers))
            throw new InvalidDataException("Saved replaced Army command is incomplete or contradictory.");
    }

    private static bool CompleteTarget(ArmyTargetState? target, long mapSize, IReadOnlySet<ArmyReceiverId> receivers) =>
        target switch
        {
            { Kind: ArmyTargetKind.Hex, Destination: { Kind: ArmyTargetDestinationKind.Hex, HexId: int hex, CityId: null, ReceiverId: null, MapObjectReceiverId: null } }
                => hex >= 0 && hex < mapSize,
            { Kind: ArmyTargetKind.City, Destination: { Kind: ArmyTargetDestinationKind.City, CityId: >= 1 and <= 51, HexId: null, ReceiverId: null, MapObjectReceiverId: null } } => true,
            { Kind: ArmyTargetKind.ArmyReceiver, Destination: { Kind: ArmyTargetDestinationKind.ArmyReceiver, CityId: null, HexId: null, ReceiverId: ArmyReceiverId id, MapObjectReceiverId: null } }
                => receivers.Contains(id),
            //The complete saved-world target validator additionally resolves
            //this reusable identity against the current MapObjects owner.
            { Kind: ArmyTargetKind.MapObjectReceiver, Destination: { Kind: ArmyTargetDestinationKind.MapObjectReceiver,
                CityId: null, HexId: null, ReceiverId: null, MapObjectReceiverId: { Value: > 0 } } } => true,
            _ => false
        };
}
