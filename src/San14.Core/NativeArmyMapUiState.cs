namespace San14.Core;

public sealed partial record UnitState
{
    /// <summary>Independent current Army byte13C. Actual fresh/reset writes
    /// are retained; missing legacy values are unknown.</summary>
    public byte? NativeMapUiByte13C { get; init; }
}
