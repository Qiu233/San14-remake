namespace San14.Core;

/// <summary>Independent current monthly channels, including unknown knowledge and raw byte values above twelve.</summary>
public sealed record AreaMonthlyTimers(byte? First, byte? Second);

public sealed partial record AreaState
{
    public AreaMonthlyTimers? MonthlyTimers { get; init; }
}
