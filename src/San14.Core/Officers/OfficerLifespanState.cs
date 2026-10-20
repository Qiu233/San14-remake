namespace San14.Core;

/// <summary>Current terminal year and lifespan category. The category is retained
/// without inventing a label for values not consumed by the known rules.</summary>
public sealed record OfficerLifespanState(ushort TerminalYear, byte Kind);

public sealed partial record OfficerState
{
    public OfficerLifespanState? Lifespan { get; init; }
}
