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

    /// <summary>A current receiver's complete replaced command remains the
    /// authority for its temporarily suspended Fire/false-report UI plan.
    /// The native source clears the five points; the old plan cannot replay them.
    /// Current action1 is checked separately by the source configuration adapter.</summary>
    public static bool RetainsReplacedMilitaryPlan(GameState state, UnitState image,
        PlayableMilitaryPlan plan, int expectedAction) =>
        plan.Action.ActionKey is "fire" or "capture" &&
        plan.WaypointHexIds is { Length: 0 } && plan.NextWaypointIndex == 0 && plan.IncidentalEnemyId is null &&
        state.ArmyLifecycle?.Receivers.FindUnit(image.Id)?.SavedCommand is
            { Kind: ArmySavedCommandKind.Stored, Command: { } command } && command.Action.Id == expectedAction;

    internal static void Validate(ArmySavedCommandState? value, long mapSize,
        IReadOnlySet<ArmyReceiverId> receivers)
    {
        if (value is null) return;
        if (value.Kind == ArmySavedCommandKind.Cleared && value.Command is null) return;
        if (value.Kind != ArmySavedCommandKind.Stored || value.Command is not { Action.Id: >= 1 and <= 17 } command ||
            !CompleteTarget(command.Target, mapSize, receivers, command.Action.Id))
            throw new InvalidDataException("Saved replaced Army command is incomplete or contradictory.");
    }

    private static bool CompleteTarget(ArmyTargetState? target, long mapSize, IReadOnlySet<ArmyReceiverId> receivers, int action) =>
        target switch
        {
            { Kind: ArmyTargetKind.City or ArmyTargetKind.Gate, Destination: { Kind: ArmyTargetDestinationKind.UnavailableBase,
                CityId: null, GateId: null, ReceiverId: null, HexId: null, MapObjectReceiverId: null } }
                => target.Kind == ArmyTargetKind.City ? action == 11 : action == 12,
            { Kind: ArmyTargetKind.MapObjectReceiver, Destination: { Kind: ArmyTargetDestinationKind.UnavailableObject,
                CityId: null, GateId: null, ReceiverId: null, HexId: null, MapObjectReceiverId: null } } => true,
            { Kind: ArmyTargetKind.Hex, Destination: { Kind: ArmyTargetDestinationKind.Hex, HexId: int hex, CityId: null, GateId: null, ReceiverId: null, MapObjectReceiverId: null } }
                => hex >= 0 && hex < mapSize,
            { Kind: ArmyTargetKind.City, Destination: { Kind: ArmyTargetDestinationKind.City, CityId: >= 1 and <= 51, GateId: null, HexId: null, ReceiverId: null, MapObjectReceiverId: null } } => true,
            { Kind: ArmyTargetKind.ArmyReceiver, Destination: { Kind: ArmyTargetDestinationKind.ArmyReceiver, CityId: null, GateId: null, HexId: null, ReceiverId: ArmyReceiverId id, MapObjectReceiverId: null } }
                => receivers.Contains(id),
            { Kind: ArmyTargetKind.Gate, Destination: { Kind: ArmyTargetDestinationKind.Gate,
                GateId: >= 52 and <= 61, CityId: null, HexId: null, ReceiverId: null, MapObjectReceiverId: null } } => true,
            //The complete saved-world target validator additionally resolves
            //this reusable identity against the current MapObjects owner.
            { Kind: ArmyTargetKind.MapObjectReceiver, Destination: { Kind: ArmyTargetDestinationKind.MapObjectReceiver,
                CityId: null, GateId: null, HexId: null, ReceiverId: null, MapObjectReceiverId: { Value: > 0 } } } => true,
            _ => false
        };
}
