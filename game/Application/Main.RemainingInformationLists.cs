using San14.Core;
using San14.Data;

public partial class Main
{
    private void OpenInformationAreaPages(MapInformationBrowserView browser,
        ArmyCampaignRuleAdapter rules, GameState world, AreaInformationPageSet pages)
    {
        var session = _session!;
        var list = new AreaInformationListView();
        bool Current(DetailsView candidate) => IsInformationBrowserCurrent(browser) && browser.IsCurrentDetail(candidate) &&
            ReferenceEquals(_session, session) && ReferenceEquals(session.World, world);
        browser.ShowDetail(list, close =>
        {
            RetainCommandDetailSpeechWorld(list, next =>
            {
                world = next;
                rules = _campaignComposition!.PrepareArmyQueries(next);
            });
            list.Configure(_repository!, _ui, pages, () => Current(list), close,
            id => RunUiAction(() =>
            {
                if (!Current(list) || !world.Areas.ContainsKey(id)) return;
                var detail = new AreaDetailsView();
                browser.ShowDetail(detail, back => ConfigureCurrentAreaDetail(detail, world, id, back,
                    preparedRules: rules, selectionIds: list.Pages.CurrentAreaIds,
                    returnSelection: list.ApplyDetailReturn, initialNameSortAscending: list.NameSortAscending));
            }), CurrentListScrollSpeed);
        });
    }

    private void OpenInformationDistrictPages(MapInformationBrowserView browser,
        ArmyCampaignRuleAdapter rules, GameState world, DistrictInformationPageSet pages, Action<GameState> adoptWorld)
    {
        var session = _session!;
        int player = session.PlayerForceId!.Value;
        var list = new DistrictInformationListView();
        bool Current(DetailsView candidate) => IsInformationBrowserCurrent(browser) && browser.IsCurrentDetail(candidate) &&
            ReferenceEquals(_session, session) && ReferenceEquals(session.World, world);
        browser.ShowDetail(list, close =>
        {
            RetainCommandDetailSpeechWorld(list, next =>
            {
                world = next;
                rules = _campaignComposition!.PrepareArmyQueries(next);
            });
            list.Configure(_repository!, _ui, pages, () => Current(list), close,
            id => RunUiAction(() =>
            {
                if (!Current(list) || !world.Districts.ContainsKey(id)) return;
                OpenInformationDistrict(browser, rules, world, id, Adopt, list.Pages.CurrentDistrictIds,
                    list.ApplyDetailReturn, list.NameSortAscending);
            }), CurrentListScrollSpeed);
        });
        void Adopt(GameState next)
        {
            world=next;rules=_campaignComposition!.PrepareArmyQueries(next);adoptWorld(next);
            list.AdoptPages(rules.PrepareDistrictInformationPages(next,player,list.Pages.CurrentDistrictIds));
        }
    }

    private void OpenInformationGatePages(MapInformationBrowserView browser,
        ArmyCampaignRuleAdapter rules, GameState world, GateInformationPageSet pages, Action<GameState> adoptWorld)
    {
        var session = _session!;
        int player = session.PlayerForceId!.Value;
        var list = new GateInformationListView();
        bool Current(DetailsView candidate) => IsInformationBrowserCurrent(browser) && browser.IsCurrentDetail(candidate) &&
            ReferenceEquals(_session, session) && ReferenceEquals(session.World, world);
        browser.ShowDetail(list, close =>
        {
            RetainCommandDetailSpeechWorld(list, next =>
            {
                world = next;
                rules = _campaignComposition!.PrepareArmyQueries(next);
            });
            list.Configure(_repository!, _ui, pages, () => Current(list), close,
            id => RunUiAction(() =>
            {
                if (!Current(list) || world.Gates?.ContainsKey(id)!=true) return;
                OpenInformationGate(browser,rules,world,id,Adopt,list.Pages.CurrentGateIds,
                    list.ApplyDetailReturn,list.NameSortAscending);
            }), CurrentListScrollSpeed);
        });
        void Adopt(GameState next)
        {
            world=next;rules=_campaignComposition!.PrepareArmyQueries(next);adoptWorld(next);
            list.AdoptPages(rules.PrepareGateInformationPages(next,player,list.Pages.CurrentGateIds));
        }
    }
}
