namespace San14.Core;

public sealed partial record UnitState
{
    /// <summary>Current source byte140 consulted by native action setters.
    /// Constructor/reset writes are explicit; legacy missing stays unknown.</summary>
    public byte? NativeCommandControl140 { get; init; }
}
