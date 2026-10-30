namespace San14.Data;

internal sealed partial class CampaignEventExecutionContext
{
    internal int ExecuteStoryAreaRegionFilter(int code, IReadOnlyList<int> arguments)
    {
        if (arguments.Count != 8) throw Missing("Area region-filter operands");
        if (code is not (291 or 292)) throw Missing("Area region-filter opcode");
        RequireStoryAreas();
        var current = StoryAreaCurrent;
        int target = StoryRegionListSource.Resolve(current, arguments[0]);
        if (StoryRegionListSource.Valid(current, target))
            _storyAreaPhysicalSlots.RemoveAll(physical =>
            {
                var city = StoryAreaCity(new(physical), original: true);
                //The shared predicate keeps invalid original Cities for its
                //narrow mode0 and drops them for removal mode1.
                if (!Known(Queries.ReadConditionBaseValidity(city), "Area-region original City validity"))
                    return code == 292;
                int region = StoryRegionListSource.CityRegion(current,
                    Known(city.PhysicalSlot, "Area-region original City identity"));
                return code == 292 ? region == target : region != target;
            });
        return _storyAreaPhysicalSlots.Count != 0 ? 1 : 0;
    }
}
