using Godot;
using San14.Core;
using San14.Data;

public partial class Main
{
    private void OpenInformationFormationPages(MapInformationBrowserView browser,
        ArmyCampaignRuleAdapter rules, GameState world, FormationInformationPageSet pages,
        Action<GameState> adoptWorld)
    {
        var list = new FormationInformationListView();
        browser.ShowDetail(list, close => list.Configure(_repository!, _ui, pages,
            () => IsInformationBrowserCurrent(browser) && browser.IsCurrentDetail(list), close,
            formation => RunInspectionUiAction(world, () =>
            {
                if (!IsInformationBrowserCurrent(browser) || !browser.IsCurrentDetail(list) ||
                    _session?.PlayerForceId is not int player) return;
                var officers = rules.PrepareFormationOfficerInformationPages(world, player, formation);
                if (officers.Available && officers.Value is { } known)
                    OpenInformationOfficerPages(browser, rules, world, known, Adopt);
                else GD.PushWarning(officers.UnresolvedInput!);
            }), CurrentListScrollSpeed, TryBorrowApplicationUiResources(list)));
        RetainCommandDetailSpeechWorld(list, next =>
        {
            world = next; rules = _campaignComposition!.PrepareArmyQueries(next);
        });
        void Adopt(GameState next)
        {
            world = next; rules = _campaignComposition!.PrepareArmyQueries(next); adoptWorld(next);
        }
    }
}
