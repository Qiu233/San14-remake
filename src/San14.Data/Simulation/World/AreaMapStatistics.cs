namespace San14.Data;

public sealed record SurfaceTypeReference(int? PhysicalSlot);

public interface IAreaMapStatisticsState : IAreaHexIteratorState
{
    byte? ReadHexSurfaceCode(HexReference hex);
    SurfaceTypeReference? ReadSurfaceTypeTableReference(int slot);
    ushort? ReadSurfaceTypeRawField2A(SurfaceTypeReference surface);
    int? ReadAreaMapForceSlot(AreaReference area);
    byte? ReadHexForceSlot(HexReference hex);
}

/// <summary>Actual223970/223A50 counters; no added area/hex/type validity predicates.</summary>
public static class AreaMapStatistics
{
    public static int CountTerrainCells(AreaReference area, IAreaMapStatisticsState state) =>
        Count(area, state, hex =>
        {
            byte code = Known(state.ReadHexSurfaceCode(hex), "current hex surface10");
            SurfaceTypeReference surface = Pointer(state.ReadSurfaceTypeTableReference(code <= 30 ? code : 0), "surface type");
            return Known(state.ReadSurfaceTypeRawField2A(surface), "surface type raw2A") > 0;
        });

    public static int CountOwnedCells(AreaReference area, IAreaMapStatisticsState state) =>
        Count(area, state, hex =>
        {
            // The member receiver is saved before the repeated representative query.
            int force = Known(state.ReadAreaMapForceSlot(area), "current representative force");
            return Known(state.ReadHexForceSlot(hex), "saved member current force14") == force;
        });

    private static int Count(AreaReference area, IAreaMapStatisticsState state,
        Func<HexReference, bool> matches)
    {
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(state);
        int filter = Known(state.ReadAreaPhysicalSlot(area), "begin area physical identity");
        int cursor = AreaHexIterator.FindNextIndex(0, filter, state);
        _ = Known(state.ReadAreaPhysicalSlot(area), "initial end area identity");
        int count = 0;
        while (cursor < 48400)
        {
            HexReference member = Pointer(state.ReadHexTableReference(cursor), "matched current hex");
            if (matches(member)) count = unchecked(count + 1);
            cursor = AreaHexIterator.FindNextIndex(cursor + 1, filter, state);
            _ = Known(state.ReadAreaPhysicalSlot(area), "fresh end area identity");
        }
        return count;
    }

    private static T Known<T>(T? value, string field) where T : struct => value
        ?? throw new InvalidDataException($"Current area map statistics input {field} is unknown.");
    private static T Pointer<T>(T? value, string field) where T : class => value
        ?? throw new InvalidDataException($"Current area map statistics {field} lookup is unknown.");
}
