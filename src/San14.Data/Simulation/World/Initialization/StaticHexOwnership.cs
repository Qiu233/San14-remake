namespace San14.Data;

public interface IStaticHexOwnershipState : IAreaHexIteratorState
{
    AreaReference? ReadAreaTableReference(int slot);
    bool? ReadAreaNativeValidity(AreaReference area);
    byte? ReadAreaCitySlot(AreaReference area);
    BaseReference? ReadCityTableReference(int slot);
    bool? ReadBaseNativeValidity(BaseReference city);
    byte? ReadAreaScenarioForceSlot(AreaReference area);
    void AssignStaticHexForce(HexReference hex, int requestedForce);
}

public sealed record StaticHexOwnershipWrite(int AreaPhysicalSlot, int HexTableSlot,
    HexReference Hex, byte RequestedForce);

/// <summary>
/// CE 330F9E..3310A2: the static area-to-hex ownership loop of 330D80.
/// Object overlays before this loop and later overlays remain separate stages.
/// </summary>
public static class StaticHexOwnership
{
    public static IReadOnlyList<StaticHexOwnershipWrite> Apply(int staticFlag,
        IStaticHexOwnershipState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var writes = new List<StaticHexOwnershipWrite>();
        if (staticFlag == 0) return writes.AsReadOnly();
        for (int slot = 0; slot < 501; slot++)
        {
            AreaReference area = Pointer(state.ReadAreaTableReference(slot), $"area {slot}");
            if (!area.PhysicalSlot.HasValue) continue;
            if (!Known(state.ReadAreaNativeValidity(area), "area validity")) continue;
            byte cityKey = Known(state.ReadAreaCitySlot(area), "area city35");
            BaseReference city = Pointer(state.ReadCityTableReference(cityKey <= 51 ? cityKey : 0), "city");
            if (!city.PhysicalSlot.HasValue) continue;
            if (!Known(state.ReadBaseNativeValidity(city), "city validity")) continue;
            byte force = Known(state.ReadAreaScenarioForceSlot(area), "saved area force6A");
            int areaPhysical = Known(state.ReadAreaPhysicalSlot(area), "iterator area physical identity");
            int cursor = AreaHexIterator.FindNextIndex(0, areaPhysical, state);
            // Constructing the native end iterator repeats the area identity calculation.
            _ = Known(state.ReadAreaPhysicalSlot(area), "end iterator area physical identity");
            while (cursor < 48400)
            {
                // The native loop dereferences the table again after its predicate matched.
                HexReference hex = Pointer(state.ReadHexTableReference(cursor), $"matched hex {cursor}");
                state.AssignStaticHexForce(hex, force);
                writes.Add(new(areaPhysical, cursor, hex, force));
                cursor = AreaHexIterator.FindNextIndex(cursor + 1, areaPhysical, state);
                _ = Known(state.ReadAreaPhysicalSlot(area), "fresh end iterator area physical identity");
            }
        }
        return writes.AsReadOnly();
    }

    private static T Known<T>(T? value, string field) where T : struct => value
        ?? throw new InvalidDataException($"Current static hex ownership input {field} is unknown.");
    private static T Pointer<T>(T? value, string field) where T : class => value
        ?? throw new InvalidDataException($"Current static hex ownership {field} lookup is unknown.");
}
