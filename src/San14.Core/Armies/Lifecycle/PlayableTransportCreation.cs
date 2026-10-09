namespace San14.Core;

/// <summary>Transport-specific waypoint and behavior knowledge. The current
/// next step belongs to the unit's shared marching state.</summary>
public sealed record NativeTransportFields(ushort[] Waypoints2C, byte Choice41);

public sealed partial record UnitState
{
    public ushort? NativeHomeSequence28 { get; init; }
    public NativeTransportFields? NativeTransport { get; init; }
}

public sealed record PlayableTransportCreationResolution(GameState State, string RuleId);

/// <summary>One complete creation publication, including source stocks,
/// Person location, manager registration and command accounting.</summary>
public interface IPlayableTransportCreationRules
{
    PlayableTransportCreationResolution? CreateTransport(GameState state,
        DispatchTransportOrder order, UnitState projection);
}

public interface IPlayableTransportRetargetRules
{
    PlayableTransportCreationResolution? RetargetTransport(GameState state, UnitState unit, CityState city);
}
