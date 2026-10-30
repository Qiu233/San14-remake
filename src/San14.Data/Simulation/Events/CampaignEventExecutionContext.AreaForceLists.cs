using San14.Data.Formats;

namespace San14.Data;

internal sealed partial class CampaignEventExecutionContext
{
    internal int AddStoryAreasByForce(int selector,
        Func<int, S14EventConditionForceReference?> readTargetForce)
    {
        var selected = ResolveForce(selector, readTargetForce);
        if (selected is null || !Known(Queries.ReadConditionForceValidity(selected), "added Area Force validity")) return 1;
        var wanted = new ForceReference(selected.PhysicalSlot);
        foreach (var entry in StoryRegisteredAreas())
        {
            var area = entry ?? throw Missing("registered Area pointer");
            byte raw = Known(StoryAreaCurrent.ReadAreaCurrentForceSlot(area), "registered Area Force");
            var actual = StoryAreaCurrent.ReadForceTableReference(raw < 52 ? raw : 0)
                ?? throw Missing("registered Area Force pointer");
            //190 tests the selected Force, not an additional Area validity.
            if (actual == wanted) AppendStoryArea(area);
        }
        return 1;
    }
}
