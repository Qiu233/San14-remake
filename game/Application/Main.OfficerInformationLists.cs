using San14.Core;
using San14.Data;

public partial class Main
{
    private void OpenInformationOfficerPages(MapInformationBrowserView browser,
        ArmyCampaignRuleAdapter rules, GameState world, OfficerInformationPageSet pages,
        Action<GameState> adoptWorld)
    {
        var list = new OfficerInformationListView();
        browser.ShowDetail(list, close => ConfigureOfficerInformationList(list, world, rules, pages, close,
            candidate => IsInformationBrowserCurrent(browser) && browser.IsCurrentDetail(candidate),
            (child, configure) => browser.ShowDetail(child, configure), adoptWorld));
    }

    private void ConfigureOfficerInformationList(OfficerInformationListView list,
        GameState world, ArmyCampaignRuleAdapter rules, OfficerInformationPageSet pages, Action close,
        Func<DetailsView, bool> current, Action<DetailsView, Action<Action>> showChild,
        Action<GameState> adoptWorld)
    {
        var repository = _repository!;
        var composition = _campaignComposition!;
        int player = _session?.PlayerForceId ?? throw new InvalidOperationException("Officer information requires a player.");
        list.Configure(repository, _ui, pages, () => current(list), close,
            officer => RunUiAction(() =>
            {
                if (!current(list) || !world.Officers.ContainsKey(officer)) return;
                var detail = new OfficerDetailsView();
                showChild(detail, back => ConfigureCurrentOfficerDetail(detail, world, officer, back,
                    () => current(detail), Adopt, list.Pages.CurrentOfficerIds,
                    list.ApplyDetailReturn, list.NameSortAscending));
            }), CurrentListScrollSpeed);
        RetainCommandDetailSpeechWorld(list, next =>
        {
            world = next; rules = composition.PrepareArmyQueries(next);
        });

        void Adopt(GameState next)
        {
            world = next; rules = composition.PrepareArmyQueries(next);
            adoptWorld(next);
            // A formal immediate setting replaces the immutable world. The
            // caller's actual candidate sequence remains owned by this list.
            list.AdoptPages(rules.PrepareOfficerInformationPages(next, player, list.Pages.CurrentOfficerIds, list.Pages.DynamicPaletteEnabled));
        }
    }
}
