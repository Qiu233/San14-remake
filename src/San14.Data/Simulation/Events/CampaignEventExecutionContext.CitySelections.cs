using San14.Data.Formats;

namespace San14.Data;

internal sealed partial class CampaignEventExecutionContext
{
    private Func<int, S14EventConditionCityPointer>? _storyTargetCityReader;

    internal void BindStoryTargetCityReader(Func<int, S14EventConditionCityPointer> readCity)
        => _storyTargetCityReader = readCity;

    internal S14EventConditionCityPointer ReadStoryTargetCityPointer(int selector)
    {
        if ((uint)(selector + 9) >= 5) throw Missing("selected City selector");
        //A fresh non-Staging context retains the constructor reset. A restored
        //ordinary Story binds the runtime reader, which preserves unknown slots.
        return _storyTargetCityReader is null ? new(null) : _storyTargetCityReader(selector);
    }
}
