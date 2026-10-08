namespace San14.Core;

public sealed partial record UnitState
{
    /// <summary>Current native Army word2A, supplied by a real creator/position
    /// write independently of the still optional movement-credit module.</summary>
    public ushort? NativeCurrentHex2A { get; init; }
}

/// <summary>The provisional movement policy still owns its route and timing.
/// Its current position write keeps already installed native position owners
/// coherent; missing legacy owners are not created by movement.</summary>
public static class PlayableUnitPosition
{
    public static UnitState AtHex(UnitState unit, int hexId)
    {
        ushort? nativeHex = unit.NativeCurrentHex2A is null ? null : checked((ushort)hexId);
        return unit with { HexId = hexId, NativeCurrentHex2A = nativeHex,
            NativeMarch = unit.NativeMarch is { } march ? march with { CurrentHex2A = checked((ushort)hexId) } : null };
    }
}
