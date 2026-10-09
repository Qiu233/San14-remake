namespace San14.Core;

/// <summary>Actual creator writes, distinct from the provisional route.
/// WrapperNextHex48 records only an actual optional wrapper write; current48
/// is published by the creator/wrapper/setter rather than a Core route.</summary>
public sealed record NativeTransportFields(ushort[] Waypoints2C, byte Choice41,
    ushort InitialNextHex48, ushort? WrapperNextHex48)
{
    public ushort? CurrentNextHex48 { get; init; }
}

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
