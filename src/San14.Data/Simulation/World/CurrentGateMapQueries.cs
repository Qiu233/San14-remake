namespace San14.Data;

// Outer null means an unknown lookup; a reference with null identity is an observed null pointer.
public sealed record ObjectReference(int? PhysicalSlot);
public sealed record HexReference(int? PhysicalSlot);
public sealed record AreaReference(int? PhysicalSlot);
public sealed record DistrictReference(int? PhysicalSlot);

public interface ICurrentGateMapState
{
    ushort? ReadBaseObjectLink(BaseReference reference);
    ObjectReference? ReadObjectTableReference(int slot);
    bool? ReadObjectNativeValidity(ObjectReference reference);
    ushort? ReadObjectHexIndex(ObjectReference reference);
    HexReference? ReadHexTableReference(int slot);
    ushort? ReadHexAreaSlot(HexReference reference);
    byte? ReadHexForceSlot(HexReference reference);
    AreaReference? ReadAreaTableReference(int slot);
    bool? ReadAreaNativeValidity(AreaReference reference);
    ushort? ReadAreaHexIndex(AreaReference reference);
    byte? ReadAreaCitySlot(AreaReference reference);
    int? ReadBaseForceSlot(BaseReference reference);
    ForceReference? ReadForceTableReference(int slot);
    bool? ReadForceNativeValidity(ForceReference reference);
    int? ReadForcePhysicalSlot(ForceReference reference);
    BaseReference? ReadCityTableReference(int slot);
    DistrictReference? ReadCityDistrictReference(BaseReference reference);
    byte? ReadDistrictForceSlot(DistrictReference reference);
    int? ReadDistrictPhysicalSlot(DistrictReference reference);
}

/// <summary>Current 22C960/22C1E0/229C10/22B050 queries, using native pointer identities.</summary>
public static class CurrentGateMapQueries
{
    public static HexReference FindHex(BaseReference reference, ICurrentGateMapState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        ushort link = Known(state.ReadBaseObjectLink(reference), "base object link4E");
        ObjectReference obj = Pointer(state.ReadObjectTableReference(link <= 3000 ? link : 0), "object");
        int hex = 0;
        if (Known(state.ReadObjectNativeValidity(obj), "object validity"))
        {
            ushort raw = Known(state.ReadObjectHexIndex(obj), "object hex10");
            if (raw <= 48399) hex = raw;
        }
        return Pointer(state.ReadHexTableReference(hex), "hex");
    }

    public static AreaReference FindArea(BaseReference reference, ICurrentGateMapState state)
    {
        HexReference hex = FindHex(reference, state);
        // 32F0B0(null) is false, whereas every nonnull CHex (including physical0) has slot3=true.
        int area = 0;
        if (hex.PhysicalSlot.HasValue)
        {
            ushort raw = Known(state.ReadHexAreaSlot(hex), "hex area12");
            if (raw <= 500) area = raw;
        }
        return Pointer(state.ReadAreaTableReference(area), "area");
    }

    public static int ReadForceSlot(BaseReference reference, ICurrentGateMapState state)
    {
        AreaReference area = FindArea(reference, state);
        if (!Known(state.ReadAreaNativeValidity(area), "area validity")) return 0;
        ushort raw = Known(state.ReadAreaHexIndex(area), "area hex3A");
        HexReference representative = Pointer(state.ReadHexTableReference(raw <= 48399 ? raw : 0), "representative hex");
        // Native code directly reads byte14 here, with no second CHex validity test or force clamp.
        return Known(state.ReadHexForceSlot(representative), "representative hex force14");
    }

    public static BaseReference FindCity(BaseReference reference, ICurrentGateMapState state)
    {
        AreaReference area = FindArea(reference, state);
        int city = 0;
        if (Known(state.ReadAreaNativeValidity(area), "area validity"))
        {
            byte raw = Known(state.ReadAreaCitySlot(area), "current area city35");
            if (raw <= 51) city = raw;
        }
        return Pointer(state.ReadCityTableReference(city), "city");
    }

    public static int ReadDistrictSlot(BaseReference reference, ICurrentGateMapState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        int rawForce = Known(state.ReadBaseForceSlot(reference), "gate slot22");
        if (rawForce < 0) throw new InvalidDataException("Negative gate force would index before the native pointer table.");
        ForceReference force = Pointer(state.ReadForceTableReference(rawForce <= 51 ? rawForce : 0), "force");
        if (!Known(state.ReadForceNativeValidity(force), "force validity")) return 0;
        // Repeats the map chain after the saved force query. All later fields are current reads.
        BaseReference city = FindCity(reference, state);
        DistrictReference district = Pointer(state.ReadCityDistrictReference(city), "city district");
        byte districtForce = Known(state.ReadDistrictForceSlot(district), "district force10");
        int forcePhysical = Known(state.ReadForcePhysicalSlot(force), "saved force physical slot");
        if (forcePhysical != districtForce) return 0;
        return Known(state.ReadDistrictPhysicalSlot(district), "district physical slot");
    }

    private static T Known<T>(T? value, string name) where T : struct => value
        ?? throw new InvalidDataException($"Current gate map input {name} is unknown.");
    private static T Pointer<T>(T? value, string name) where T : class => value
        ?? throw new InvalidDataException($"Current gate map {name} pointer lookup is unknown.");
}
