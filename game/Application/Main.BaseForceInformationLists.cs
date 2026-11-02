using San14.Core;
using San14.Data;

public partial class Main
{
    private void OpenInformationCityPages(MapInformationBrowserView browser,
        ArmyCampaignRuleAdapter rules, GameState world, CityInformationPageSet pages)
    {
        var session = _session!;
        var list = new CityInformationListView();
        bool Current(DetailsView candidate) => IsInformationBrowserCurrent(browser) && browser.IsCurrentDetail(candidate) &&
            IsInspectionWorldCurrent(session, world);
        browser.ShowDetail(list, close =>
        {
            RetainCommandDetailSpeechWorld(list, next =>
            {
                world = next;
                rules = _campaignComposition!.PrepareArmyQueries(next);
            });
            list.Configure(_repository!, _ui, pages, () => Current(list), close,
            id => RunInspectionUiAction(world, () =>
            {
                if (!Current(list) || !world.Cities.ContainsKey(id)) return;
                var detail = new CityDetailsView();
                browser.ShowDetail(detail, back => ConfigureCurrentCityDetail(detail, world, id, back,
                    preparedRules: rules, selectionIds: list.Pages.CurrentCityIds,
                    returnSelection: list.ApplyDetailReturn, initialNameSortAscending: list.NameSortAscending));
            }), CurrentListScrollSpeed, TryBorrowApplicationUiResources(list));
        });
    }

    private void OpenInformationForcePages(MapInformationBrowserView browser,
        ArmyCampaignRuleAdapter rules, GameState world, ForceInformationPageSet pages)
    {
        var session = _session!;
        var list = new ForceInformationListView();
        bool Current(DetailsView candidate) => IsInformationBrowserCurrent(browser) && browser.IsCurrentDetail(candidate) &&
            IsInspectionWorldCurrent(session, world);
        browser.ShowDetail(list, close =>
        {
            RetainCommandDetailSpeechWorld(list, next =>
            {
                world = next;
                rules = _campaignComposition!.PrepareArmyQueries(next);
            });
            list.Configure(_repository!, _ui, pages, () => Current(list), close,
            id => RunInspectionUiAction(world, () =>
            {
                if (!Current(list) || !world.Forces.ContainsKey(id)) return;
                var detail = new ForceDetailsView();
                browser.ShowDetail(detail, back => ConfigureCurrentForceDetail(detail, world, id, back,
                    preparedRules: rules, selectionIds: list.Pages.CurrentForceIds,
                    returnSelection: list.ApplyDetailReturn, initialNameSortAscending: list.NameSortAscending));
            }), CurrentListScrollSpeed, TryBorrowApplicationUiResources(list));
        });
    }
}
