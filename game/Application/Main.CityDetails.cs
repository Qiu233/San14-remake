using San14.Core;
using San14.Data;

public partial class Main
{
    private void ConfigureCurrentCityDetail(CityDetailsView view, GameState world, int cityId,
        Action close, bool mapBackdrop = false, ArmyCampaignRuleAdapter? preparedRules = null,
        CampaignIncomeReportPublication? report = null, IReadOnlyList<int>? selectionIds = null,
        Action<InformationDetailSelectionReturn>? returnSelection = null, bool? initialNameSortAscending = null)
    {
        var rules = preparedRules ?? _campaignComposition?.PrepareArmyQueries(world)
            ?? throw new InvalidDataException("city-information.current_campaign_rules");
        var page = report is null ? rules.QueryCityInformation(world, cityId) : rules.QueryCityInformation(report, cityId);
        RetainCommandDetailSpeechWorld(view, next =>
        {
            world = next;
            rules = _campaignComposition!.PrepareArmyQueries(next);
        });
        view.Configure(_repository ?? throw new InvalidDataException("city-information.resource_owner"),
            page, _ui, Return, mapBackdrop,
            PrepareDetailSelection(rules, world, InformationListKind.Cities, cityId, selectionIds) with { InitialNameSortAscending = initialNameSortAscending },
            id => RunUiAction(() => view.Apply(report is null ? rules.QueryCityInformation(world, id) : rules.QueryCityInformation(report, id))));
        void Return()
        {
            returnSelection?.Invoke(view.ReadSelectionReturn());
            close();
        }
    }
}
