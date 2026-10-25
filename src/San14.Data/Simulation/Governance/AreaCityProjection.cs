namespace San14.Data;

/// <summary>The current Area assignment boundary. Native zero is a known
/// absence of a receiving City; native lookup knowledge must be resolved
/// before decoding. The fixed original-City validity byte is independent.</summary>
internal static class AreaCityProjection
{
    internal static int? FromNative(int city) => city switch
    {
        0 => null,
        >= 1 and <= 51 => city,
        _ => throw new InvalidDataException("area.current_city_outside_source_table")
    };
    internal static byte ToNative(int? city) => city switch
    {
        null => 0,
        >= 1 and <= 51 => checked((byte)city.Value),
        _ => throw new InvalidDataException("area.receiving_city_outside_source_table")
    };
}
