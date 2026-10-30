namespace San14.Data.Formats;

public sealed partial class S14PlayerEventConditionRuntime
{
    internal int ExecuteStoryCityRegionFilter(int code, IReadOnlyList<int> arguments,
        SourceWorldContext current, Func<S14EventConditionCityPointer>? readEmperorCity)
    {
        if (arguments.Count != 8) throw new InvalidDataException("story.city_region.operands");
        if (code == 318)
            _selectedCities.RemoveAll(city => city != (readEmperorCity?.Invoke()
                ?? throw new InvalidDataException("story.city_region.Emperor_E4_City")));
        else if (code is 268 or 269)
        {
            int target = StoryRegionListSource.Resolve(current, arguments[0]);
            if (StoryRegionListSource.Valid(current, target))
                _selectedCities.RemoveAll(city =>
                {
                    //No City validity gate: the predicate reads CE directly.
                    int actual = StoryRegionListSource.CityRegion(current,
                        Known(city.PhysicalSlot, "City-region candidate identity"));
                    return code == 269 ? actual == target : actual != target;
                });
        }
        else throw new InvalidDataException("story.city_region.opcode");
        return _selectedCities.Count != 0 ? 1 : 0;
    }
}
