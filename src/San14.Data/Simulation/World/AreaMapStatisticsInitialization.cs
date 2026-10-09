namespace San14.Data;

public interface IAreaMapStatisticsInitializationState
{
    bool? ReadMapStatisticsWorldPresent();
    int? ReadMapStatisticsWorldField48();
    AreaReference? ReadAreaTableReference(int slot);
    void ApplySpecialModeAreaReset(AreaReference area);
    bool? ReadAreaNativeValidity(AreaReference area);
    int? ReadAreaMapForceSlot(AreaReference area);
    int? CountAreaTerrainCells(AreaReference area);
    int? CountAreaOwnedCells(AreaReference area);
    void WriteAreaRawField78(AreaReference area, byte value);
    void WriteAreaRawField76(AreaReference area, ushort value);
    void WriteAreaRawField68(AreaReference area, ushort value);
    void UpdateAreaMapStatisticsSummary();
}

/// <summary>3304A0: optional special-mode changes, current statistics, mandatory summary.</summary>
public static class AreaMapStatisticsInitialization
{
    public static bool Apply(int staticFlag, IAreaMapStatisticsInitializationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!Known(state.ReadMapStatisticsWorldPresent(), "world presence"))
            throw new InvalidDataException("Current world is null at the native mode48 read.");
        int mode = Known(state.ReadMapStatisticsWorldField48(), "world mode48");
        if (unchecked((uint)mode - 100u) <= 99u)
            for (int slot = 0; slot < 501; slot++)
                state.ApplySpecialModeAreaReset(Pointer(state.ReadAreaTableReference(slot), $"special area {slot}"));
        for (int slot = 0; slot < 501; slot++)
        {
            AreaReference area = Pointer(state.ReadAreaTableReference(slot), $"area {slot}");
            if (!area.PhysicalSlot.HasValue) continue;
            if (!Known(state.ReadAreaNativeValidity(area), "area validity")) continue;
            int force = Known(state.ReadAreaMapForceSlot(area), "current representative force");
            state.WriteAreaRawField78(area, (byte)Math.Clamp(force, 0, 51));
            int terrainCount = Known(state.CountAreaTerrainCells(area), "terrain counter");
            state.WriteAreaRawField76(area, CountToNativeWord(terrainCount));
            if (staticFlag != 0)
            {
                int ownedCount = Known(state.CountAreaOwnedCells(area), "owned counter");
                state.WriteAreaRawField68(area, CountToNativeWord(ownedCount));
            }
        }
        state.UpdateAreaMapStatisticsSummary();
        return true;
    }

    public static ushort CountToNativeWord(int count) => count < 0 ? (ushort)0 : unchecked((ushort)count);
    private static T Known<T>(T? value, string field) where T : struct => value
        ?? throw new InvalidDataException($"Current area map statistics initialization {field} is unknown.");
    private static T Pointer<T>(T? value, string field) where T : class => value
        ?? throw new InvalidDataException($"Current area map statistics initialization {field} lookup is unknown.");
}
