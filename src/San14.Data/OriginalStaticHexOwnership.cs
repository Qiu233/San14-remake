namespace San14.Data;

public interface IOriginalStaticHexOwnershipState : IOriginalAreaHexIteratorState
{
    OriginalAreaReference? ReadAreaTableReference(int slot);
    bool? ReadAreaNativeValidity(OriginalAreaReference area);
    byte? ReadAreaCitySlot(OriginalAreaReference area);
    OriginalBaseReference? ReadCityTableReference(int slot);
    bool? ReadBaseNativeValidity(OriginalBaseReference city);
    byte? ReadAreaScenarioForceSlot(OriginalAreaReference area);
    void AssignStaticHexForce(OriginalHexReference hex, int requestedForce);
}

public sealed record OriginalStaticHexOwnershipWrite(int AreaPhysicalSlot, int HexTableSlot,
    OriginalHexReference Hex, byte RequestedForce);

/// <summary>
/// CE 330F9E..3310A2: the static area-to-hex ownership loop of 330D80.
/// Object overlays before this loop and later overlays remain separate stages.
/// </summary>
public static class OriginalStaticHexOwnership
{
    public static IReadOnlyList<OriginalStaticHexOwnershipWrite> Apply(int staticFlag,
        IOriginalStaticHexOwnershipState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var writes = new List<OriginalStaticHexOwnershipWrite>();
        if (staticFlag == 0) return writes.AsReadOnly();
        for (int slot = 0; slot < 501; slot++)
        {
            OriginalAreaReference area = Pointer(state.ReadAreaTableReference(slot), $"area {slot}");
            if (!area.PhysicalSlot.HasValue) continue;
            if (!Known(state.ReadAreaNativeValidity(area), "area validity")) continue;
            byte cityKey = Known(state.ReadAreaCitySlot(area), "area city35");
            OriginalBaseReference city = Pointer(state.ReadCityTableReference(cityKey <= 51 ? cityKey : 0), "city");
            if (!city.PhysicalSlot.HasValue) continue;
            if (!Known(state.ReadBaseNativeValidity(city), "city validity")) continue;
            byte force = Known(state.ReadAreaScenarioForceSlot(area), "saved area force6A");
            int areaPhysical = Known(state.ReadAreaPhysicalSlot(area), "iterator area physical identity");
            int cursor = OriginalAreaHexIterator.FindNextIndex(0, areaPhysical, state);
            // Constructing the native end iterator repeats the area identity calculation.
            _ = Known(state.ReadAreaPhysicalSlot(area), "end iterator area physical identity");
            while (cursor < 48400)
            {
                // The native loop dereferences the table again after its predicate matched.
                OriginalHexReference hex = Pointer(state.ReadHexTableReference(cursor), $"matched hex {cursor}");
                state.AssignStaticHexForce(hex, force);
                writes.Add(new(areaPhysical, cursor, hex, force));
                cursor = OriginalAreaHexIterator.FindNextIndex(cursor + 1, areaPhysical, state);
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
