namespace San14.Core;

public enum ArmyOverrideTargetKind { None, City, Gate, Hex, Object, ArmyReceiver, Unresolved }
public enum ArmyOverrideDestinationKind { None, City, Gate, Hex, ArmyReceiver, Unresolved, MapObjectReceiver }

/// <summary>An override destination uses campaign identities. An unresolved
/// destination is known to exist, but its resource identity is not decoded.</summary>
public sealed record ArmyOverrideDestination(ArmyOverrideDestinationKind Kind,
    int? EntityId = null, ArmyReceiverId? ReceiverId = null)
{
    public MapObjectReceiverId? MapObjectReceiverId { get; init; }
}

/// <summary>The temporary target selected by provocation and other explicit
/// overrides. Category and destination knowledge are independent of the normal
/// army order. Null is unknown, not a cleared override.</summary>
public sealed record ArmyOverrideTargetState(ArmyOverrideTargetKind? Kind, ArmyOverrideDestination? Destination)
{
    public static ArmyOverrideTargetState Cleared { get; } = new(ArmyOverrideTargetKind.None,
        new(ArmyOverrideDestinationKind.None));
}

public sealed partial record UnitState
{
    public ArmyOverrideTargetState? OverrideTarget { get; init; }
}

public static class ArmyOverrideTargetWrites
{
    /// <summary>Null means that this operation did not write the override.</summary>
    public static UnitState Apply(UnitState army, ArmyOverrideTargetState? target) =>
        target is null ? army : army with { OverrideTarget = target };
}
