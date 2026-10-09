using San14.Core;

namespace San14.Data;

/// <summary>CE TC2AEBC0 / 2AED40 and the byte36 write in2DA620.
/// The generic2D2CA0 creator retains its receiver's direction rather than resetting it.</summary>
public static class ArmyDirectionSource
{
    public const int HexCount = 48400;
    /// <summary>Actual2269E0 word36=0, reached by CArmyUnitData's constructor/reset.
    /// This value may only be installed when that reset is known to have run.</summary>
    public const byte ResetDirection36 = 0;

    /// <summary>Exact result for legal source Hex indices. Equal and nonadjacent
    /// endpoints return0; source directions1..6 use the existing native neighbor order.</summary>
    public static byte Direction2AEBC0(int fromHex, int toHex)
    {
        if ((uint)fromHex >= HexCount || (uint)toHex >= HexCount)
            throw new ArgumentOutOfRangeException(nameof(fromHex), "Army direction requires source Hex indices0..48399.");
        if (fromHex == toHex) return 0;
        for (byte direction = 1; direction <= 6; direction++)
            if (PersonReturnPathSource.Neighbor(fromHex, direction) == toHex) return direction;
        return 0;
    }

    /// <summary>The movement interface accepts only an actual adjacent edge.
    /// Controlled position jumps and equal positions do not publish a direction.</summary>
    public static byte? ReadAdjacentDirection36(int fromHex, int toHex)
    {
        if ((uint)fromHex >= HexCount || (uint)toHex >= HexCount) return null;
        byte direction = Direction2AEBC0(fromHex, toHex);
        return direction == 0 ? null : direction;
    }

    /// <summary>The caller supplies the actual edge, including a batch's final
    /// edge. The batch-entry Army position only needs to remain internally coherent.</summary>
    public static byte? ReadDirection36(GameState state, UnitState unit, int fromHex, int toHex)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(unit);
        if (state.MapWidth != 220 || state.MapHeight != 220 || unit.NativeArmyOrigin is null ||
            ArmyMarchSourceAdapter.ReadCreatedCurrentHex(state, unit) != unit.HexId) return null;
        return ReadAdjacentDirection36(fromHex, toHex);
    }

    /// <summary>Original2AED40 yaw in radians, including source0/5 zero.</summary>
    public static float YawRadians2AED40(int direction) => direction switch
    {
        1 => 240f * 0.017453292f,
        2 => 3.1415927f,
        3 => 2.0943952f,
        4 => 5.2359877f,
        6 => 1.0471976f,
        _ => 0f
    };
}

public sealed partial class ArmyCampaignRuleAdapter : IPlayableArmyPositionRules
{
    public byte? ReadDirection36(GameState state, UnitState unit, int fromHex, int toHex)
        => ArmyDirectionSource.ReadDirection36(state, unit, fromHex, toHex);
}
