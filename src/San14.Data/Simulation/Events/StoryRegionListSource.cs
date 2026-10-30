namespace San14.Data;

internal static class StoryRegionListSource
{
    internal static int Resolve(SourceWorldContext current, int raw)
    {
        if (raw < 0) throw Missing("negative_region_index");
        return current.ReadStaticTableReference(0xDC20, raw < 12 ? raw : 0)?.PhysicalSlot
            ?? throw Missing("Region_pointer");
    }

    //CRegionData's actual validity is its physical distance from table0.
    internal static bool Valid(SourceWorldContext current, int physical) => physical != Resolve(current, 0);

    internal static int CityRegion(SourceWorldContext current, int city)
    {
        byte raw = current.ConditionCityRegionReader?.Invoke(city) ?? throw Missing("CityCE_State2E_region");
        return Resolve(current, raw);
    }

    private static InvalidDataException Missing(string field) => new("story.region_list." + field);
}
