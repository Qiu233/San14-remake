namespace San14.Data;

public interface IAreaCityAssignmentState
{
    void WriteAreaCitySlot(AreaReference area, byte citySlot);
    IReadOnlyList<int?>? ReadRegisteredPersonPhysicalSlots();
    ushort? ReadPersonSecondLocation(int physicalSlot);
    int? ReadAreaPhysicalSlot(AreaReference area);
    byte? ReadPersonStatus(int physicalSlot);
    void WritePersonFirstLocation(int physicalSlot, ushort location);
}

public interface IStaticAreaCityInitializationState : IAreaCityAssignmentState
{
    AreaReference? ReadAreaTableReference(int slot);
    bool? ReadAreaNativeValidity(AreaReference area);
    byte? ReadAreaValidityCode(AreaReference area);
}

public sealed record AreaCityAssignmentResult(byte CitySlot,
    IReadOnlyList<int> PersonPhysicalSlotsWritten);

/// <summary>CE 23FDC0: current area35 followed by the registered status5 person scan.</summary>
public static class AreaCityAssignment
{
    /// <summary>
    /// The first loop of 3306E0 only. The subsequent base neighborhood rebuild
    /// is a separate stage and is not implied by completing this loop.
    /// </summary>
    public static IReadOnlyList<AreaCityAssignmentResult> InitializeStaticCities(int staticFlag,
        IStaticAreaCityInitializationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var results = new List<AreaCityAssignmentResult>();
        if (staticFlag == 0) return results.AsReadOnly();
        for (int slot = 0; slot < 501; slot++)
        {
            AreaReference area = state.ReadAreaTableReference(slot)
                ?? throw new InvalidDataException($"Current area table lookup {slot} is unknown.");
            if (!area.PhysicalSlot.HasValue) continue;
            if (!Known(state.ReadAreaNativeValidity(area), "area validity")) continue;
            // Read raw34 again after validity rather than reusing the predicate input.
            int city = Known(state.ReadAreaValidityCode(area), "area raw34");
            results.Add(Apply(area, city, state));
        }
        return results.AsReadOnly();
    }

    public static AreaCityAssignmentResult Apply(AreaReference area, int requestedCity,
        IAreaCityAssignmentState state)
    {
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(state);
        byte city = (byte)Math.Clamp(requestedCity, 0, 51);
        state.WriteAreaCitySlot(area, city);
        IReadOnlyList<int?> people = state.ReadRegisteredPersonPhysicalSlots()
            ?? throw new InvalidDataException("Current registered person sequence is unknown.");
        var writes = new List<int>();
        foreach (int? pointer in people)
        {
            int physical = pointer
                ?? throw new InvalidDataException("Registered person pointer is explicitly null; native location read would be unsafe.");
            if (physical is < 0 or >= 1851)
                throw new InvalidDataException("Registered person physical slot is outside the inspected native profile.");
            ushort second = Known(state.ReadPersonSecondLocation(physical), "person location11C");
            // The native classifier consumes Q before status and compares against the
            // physical area only for its 562..1061 category. It does not check validity.
            if (second is < 562 or > 1061) continue;
            int areaPhysical = Known(state.ReadAreaPhysicalSlot(area), "area physical slot");
            if (second - 561 != areaPhysical) continue;
            if (Known(state.ReadPersonStatus(physical), "person status11E") != 5) continue;
            // This uses the original signed request, not the clamped area byte.
            ushort first = (ushort)Math.Clamp(requestedCity >= 52 ? 0 : requestedCity, 0, 61);
            state.WritePersonFirstLocation(physical, first);
            writes.Add(physical);
        }
        return new(city, writes.AsReadOnly());
    }

    private static T Known<T>(T? value, string field) where T : struct => value
        ?? throw new InvalidDataException($"Current area city assignment input {field} is unknown.");
}
