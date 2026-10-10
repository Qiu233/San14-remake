namespace San14.Core;

/// <summary>The player's ordered, explicit transport waypoints. A null entry
/// records an unknown selection; None records a known empty selection. The
/// automatic current next step belongs to the shared marching state.</summary>
public sealed record TransportRouteState
{
    public IReadOnlyList<ArmyMarchDestination?> Waypoints { get; }
    public TransportRouteState(IReadOnlyList<ArmyMarchDestination?> waypoints) =>
        Waypoints = Array.AsReadOnly(waypoints.ToArray());
}
