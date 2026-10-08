namespace San14.Core;

public sealed partial record UnitState
{
    /// <summary>Actual source56 route hold and cached301DD0 goal1F8.
    /// Missing old fields stay unknown; neither is a Core destination.</summary>
    public byte? NativeRouteHold56 { get; init; }
    public ushort? NativeRouteGoal1F8 { get; init; }
}
