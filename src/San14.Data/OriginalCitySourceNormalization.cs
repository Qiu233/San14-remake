using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Projects source city objects without confusing current durability with the base capacity.</summary>
public static class OriginalCitySourceNormalization
{
    public static CitySize? SizeFromCode(int code) => code switch
    {
        1 => CitySize.Small, 2 => CitySize.Medium,
        3 => CitySize.Large, 4 => CitySize.Giant, _ => null
    };

    public static int? CurrentDurability(CityPreviewRecord city, uint marker,
        S14ScenarioObjectCatalog? objects)
    {
        // Older profiles have no decoded associated-object table yet.
        if (marker != 0x66 || objects is null) return null;
        // The CE static loader reconstructs City+4E as the same physical slot.
        // Check the association independently rather than looking up by name.
        if (city.Slot < 1 || city.Slot >= objects.Objects.Count)
            throw new InvalidDataException($"City {city.Slot} has no associated source object.");
        S14ScenarioObjectRecord linked = objects.Objects[city.Slot];
        if (linked.PhysicalSlot != city.Slot || linked.RawField12 != 1 ||
            city.HexId != linked.RawField10 || city.ForceSlot != linked.RawField13)
            throw new InvalidDataException($"City {city.Slot} associated source object differs from its city record.");
        // City+46 is a capacity input; Object+14 is current durability. Source
        // projection does not apply unresolved post-load policy/capacity effects.
        return linked.RawField14;
    }
}
