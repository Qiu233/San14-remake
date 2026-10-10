namespace San14.Core;

public sealed partial record UnitState
{
    public ushort? NativeHomeSequence28 { get; init; }
    public TransportRouteState? TransportRoute { get; init; }
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
