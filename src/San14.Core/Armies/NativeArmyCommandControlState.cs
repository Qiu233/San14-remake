namespace San14.Core;

public sealed partial record UnitState
{
    /// <summary>Compatibility scalar and derived projection of the receiver's
    /// SavedCommand. New commands are written as that complete aggregate.
    /// Legacy missing stays unknown; a positive scalar cannot supply a backup.</summary>
    public byte? NativeCommandControl140 { get; init; }
}
