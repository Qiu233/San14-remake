namespace San14.Data;

public readonly record struct OriginalArmyCurrentHexOccupancyWrite(int PhysicalHex, ushort Raw1C);
public sealed record OriginalArmyCurrentHexOccupancyReceipt(int ArmyPhysicalSlot, int FromHex, int ToHex,
    short OldSigned1C, OriginalArmyCurrentHexOccupancyWrite[] Writes);
public sealed record OriginalArmyCurrentHexOccupancyResult(OriginalArmyCurrentHexOccupancyReceipt? Receipt,
    string? UnresolvedOwner);

/// <summary>Only the reached2D99C0 old-occupant clear and2DA620 destination
/// 241D70 write. The caller must already own an actual position commit; this
/// does not decide movement, debit credit, write Army2A or set flags150.</summary>
public static class OriginalArmyCurrentHexOccupancySource
{
    public static OriginalArmyCurrentHexOccupancyResult TryApplyCurrent(OriginalEventInitializationContext current,
        int armyPhysical, int fromHex, int toHex)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (armyPhysical is < 1 or > 500) return Unknown("current.position_commit_Army_physical1_500");
        if (fromHex is < 0 or >= 48400 || toHex is < 0 or >= 48400)
            return Unknown("current.position_commit_Hex_index");
        var from = current.ReadHexTableReference(fromHex);
        if (from is null) return Unknown("current.position_commit_old_Hex_pointer");
        if (from.PhysicalSlot is not int fromPhysical) return Unknown("branch.position_commit_invalid_old_Hex");
        var to = current.ReadHexTableReference(toHex);
        if (to is null) return Unknown("current.position_commit_destination_Hex_pointer");
        if (to.PhysicalSlot is not int toPhysical) return Unknown("branch.position_commit_invalid_destination_Hex");
        if (current.ReadHexRaw1C(from) is not ushort oldRaw)
            return Unknown("current.position_commit_old_Hex_signed1C");

        //231660 sign-extends the old word. All dependencies are reached before
        //either write, so a missing destination/input cannot clear the old cell.
        short oldSigned = unchecked((short)oldRaw);
        if (!current.TryApplyArmyOccupancyTraversal(armyPhysical, fromPhysical, toPhysical, out var typedWrites))
            return Unknown("current.position_commit_Army_occupancy_binding");
        var writes = typedWrites.Select(write => new OriginalArmyCurrentHexOccupancyWrite(write.HexId,
            write.Cell.Kind == San14.Core.ArmyMapCellKind.Empty ? (ushort)0 : checked((ushort)armyPhysical))).ToArray();
        return new(new(armyPhysical, fromHex, toHex, oldSigned, writes), null);
    }

    private static OriginalArmyCurrentHexOccupancyResult Unknown(string owner) => new(null, owner);
}
