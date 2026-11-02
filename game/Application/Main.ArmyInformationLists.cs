using San14.Core;
using San14.Data;

public partial class Main
{
    private void OpenInformationArmyPages(MapInformationBrowserView browser,
        ArmyCampaignRuleAdapter rules, GameState world, ArmyInformationPageSet pages,
        Action<GameState> adoptWorld)
    {
        var repository = _repository!;
        var composition = _campaignComposition!;
        var session = _session!;
        int player = session.PlayerForceId ?? throw new InvalidOperationException("Army information requires a player.");
        var list = new ArmyInformationListView();
        bool Current(DetailsView candidate) => IsInformationBrowserCurrent(browser) && browser.IsCurrentDetail(candidate) &&
            IsInspectionWorldCurrent(session, world);
        browser.ShowDetail(list, close =>
        {
            list.Configure(repository, _ui, pages, () => Current(list), close,
                unitId => RunInspectionUiAction(world, () =>
                {
                    if (!Current(list) || !world.Units.ContainsKey(unitId)) return;
                    var publication = rules.QueryUnitSourceDetails(world, unitId);
                    if (publication.Exists != true) return;
                    var detail = new ArmyDetailsView();
                    browser.ShowDetail(detail, back => ConfigureUnitSourceDetails(detail, world, rules,
                        unitId, publication, back, browser, Adopt));
                }), CurrentListScrollSpeed, TryBorrowApplicationUiResources(list));
            RetainCommandDetailSpeechWorld(list, next =>
            {
                world = next; rules = composition.PrepareArmyQueries(next);
            });
        });

        void Adopt(GameState next)
        {
            world = next; rules = composition.PrepareArmyQueries(next); adoptWorld(next);
            list.AdoptPages(rules.PrepareArmyInformationPages(next, player, list.Pages.CurrentUnitIds));
        }
    }
}
