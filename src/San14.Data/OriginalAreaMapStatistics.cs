namespace San14.Data;

public sealed record OriginalSurfaceTypeReference(int? PhysicalSlot);

public interface IOriginalAreaMapStatisticsState : IOriginalAreaHexIteratorState
{
    byte? ReadHexSurfaceCode(OriginalHexReference hex);
    OriginalSurfaceTypeReference? ReadSurfaceTypeTableReference(int slot);
    ushort? ReadSurfaceTypeRawField2A(OriginalSurfaceTypeReference surface);
    int? ReadAreaMapForceSlot(OriginalAreaReference area);
    byte? ReadHexForceSlot(OriginalHexReference hex);
}

/// <summary>Actual223970/223A50 counters; no added area/hex/type validity predicates.</summary>
public static class OriginalAreaMapStatistics
{
    public static int CountTerrainCells(OriginalAreaReference area, IOriginalAreaMapStatisticsState state) =>
        Count(area, state, hex =>
        {
            byte code = Known(state.ReadHexSurfaceCode(hex), "current hex surface10");
            OriginalSurfaceTypeReference surface = Pointer(state.ReadSurfaceTypeTableReference(code <= 30 ? code : 0), "surface type");
            return Known(state.ReadSurfaceTypeRawField2A(surface), "surface type raw2A") > 0;
        });

    public static int CountOwnedCells(OriginalAreaReference area, IOriginalAreaMapStatisticsState state) =>
        Count(area, state, hex =>
        {
            // The member receiver is saved before the repeated representative query.
            int force = Known(state.ReadAreaMapForceSlot(area), "current representative force");
            return Known(state.ReadHexForceSlot(hex), "saved member current force14") == force;
        });

    private static int Count(OriginalAreaReference area, IOriginalAreaMapStatisticsState state,
        Func<OriginalHexReference, bool> matches)
    {
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(state);
        int filter = Known(state.ReadAreaPhysicalSlot(area), "begin area physical identity");
        int cursor = OriginalAreaHexIterator.FindNextIndex(0, filter, state);
        _ = Known(state.ReadAreaPhysicalSlot(area), "initial end area identity");
        int count = 0;
        while (cursor < 48400)
        {
            OriginalHexReference member = Pointer(state.ReadHexTableReference(cursor), "matched current hex");
            if (matches(member)) count = unchecked(count + 1);
            cursor = OriginalAreaHexIterator.FindNextIndex(cursor + 1, filter, state);
            _ = Known(state.ReadAreaPhysicalSlot(area), "fresh end area identity");
        }
        return count;
    }

    private static T Known<T>(T? value, string field) where T : struct => value
        ?? throw new InvalidDataException($"Current area map statistics input {field} is unknown.");
    private static T Pointer<T>(T? value, string field) where T : class => value
        ?? throw new InvalidDataException($"Current area map statistics {field} lookup is unknown.");
}
