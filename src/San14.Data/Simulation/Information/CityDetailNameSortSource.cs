namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //34C5A0 reads this City's fixed secondary text, independently of the
    //visible current CityState.Name. Resolve the actual receiver first.
    internal string ReadCityDetailNameSortKeyCurrent(SourceWorldContext current, int cityId) =>
        CurrentPersonListSource.CitySortText(current, _scenario, cityId);
}
