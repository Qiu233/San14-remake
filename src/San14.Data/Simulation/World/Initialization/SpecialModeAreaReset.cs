namespace San14.Data;

public interface ISpecialModeAreaResetState : IAreaHexIteratorState
{
    bool? ReadAreaNativeValidity(AreaReference area);
    byte? ReadAreaFlag74(AreaReference area);
    ushort? ReadAreaNeighborSlot(AreaReference area, int index);
    byte? ReadAreaFlags38(AreaReference area);
    void WriteAreaFlags38(AreaReference area, byte value);
    AreaReference? ReadAreaTableReference(int slot);
    void WriteAreaValidityCode(AreaReference area, byte value);
    void WriteHexForceSlot(HexReference hex, byte value);
    void WriteHexSurfaceCode(HexReference hex, byte value);
}

public sealed record SpecialModeAreaResetResult(bool AreaWasValid, bool OwnMarkedBranch,
    IReadOnlyList<HexReference> HexWrites, int Flag38Writes);

/// <summary>
/// Complete228D10 and its2D68A0 branch. Caller3304A0 restricts the mode.
/// These are direct force/surface/area writes, with no2406F0 service/scenery effects.
/// CURRENT inputs and completed prefixes are preserved; no whole initialization claim.
/// </summary>
public static class SpecialModeAreaReset
{
    public static SpecialModeAreaResetResult Apply(AreaReference area,
        ISpecialModeAreaResetState state)
    {
        ArgumentNullException.ThrowIfNull(area); ArgumentNullException.ThrowIfNull(state);
        if (area.PhysicalSlot is not int physical || physical is < 0 or > 500)
            throw new InvalidDataException("Special area reset requires a nonnull supported CE area receiver.");
        var writes = new List<HexReference>();
        if (!Known(state.ReadAreaNativeValidity(area), "self area slot3 validity")) return new(false, false, writes.AsReadOnly(), 0);
        byte flag = Known(state.ReadAreaFlag74(area), "self area74");
        if (flag != 0)
        {
            int filterKey = Known(state.ReadAreaPhysicalSlot(area), "begin area physical identity");
            int cursor = AreaHexIterator.FindNextIndex(0, filterKey, state);
            _ = Known(state.ReadAreaPhysicalSlot(area), "initial end area physical identity");
            while (cursor < 48400)
            {
                HexReference hex = Pointer(state.ReadHexTableReference(cursor), "matched current hex");
                if (!hex.PhysicalSlot.HasValue) throw new InvalidDataException("Null matched hex at native direct force/surface write.");
                state.WriteHexForceSlot(hex, 0);
                state.WriteHexSurfaceCode(hex, 22);
                writes.Add(hex);
                cursor = AreaHexIterator.FindNextIndex(cursor + 1, filterKey, state);
                _ = Known(state.ReadAreaPhysicalSlot(area), "fresh end area physical identity");
            }
            state.WriteAreaValidityCode(area, 0);
            return new(true, true, writes.AsReadOnly(), 0);
        }
        int flagWrites = 0;
        for (int index = 0; index < 8; index++)
        {
            ushort raw = Known(state.ReadAreaNeighborSlot(area, index), $"neighbor word{index}");
            AreaReference neighbor = Pointer(state.ReadAreaTableReference(raw <= 500 ? raw : 0), "neighbor area");
            if (!neighbor.PhysicalSlot.HasValue) continue;
            if (Known(state.ReadAreaFlag74(neighbor), "neighbor area74") == 0) continue;
            byte current = Known(state.ReadAreaFlags38(area), "current self flags38");
            state.WriteAreaFlags38(area, (byte)(current | 0x40));
            flagWrites++;
        }
        return new(true, false, writes.AsReadOnly(), flagWrites);
    }
    private static T Known<T>(T? value, string field) where T : struct => value
        ?? throw new InvalidDataException($"Current special area reset {field} is unknown.");
    private static T Pointer<T>(T? value, string field) where T : class => value
        ?? throw new InvalidDataException($"Current special area reset {field} pointer is unknown.");
}
