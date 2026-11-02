using Godot;
using San14.Core;
using San14.Data;

public partial class Main
{
    private void OpenInformationDefinitionPages(MapInformationBrowserView browser,
        ArmyCampaignRuleAdapter rules, GameState world, DefinitionInformationPageSet pages,
        Action<GameState> adoptWorld)
    {
        var list = new DefinitionInformationListView();
        browser.ShowDetail(list, close => list.Configure(_repository!, _ui, pages,
            () => IsInformationBrowserCurrent(browser) && browser.IsCurrentDetail(list), close,
            definition => RunInspectionUiAction(world, () =>
            {
                if (!IsInformationBrowserCurrent(browser) || !browser.IsCurrentDetail(list) ||
                    _session?.PlayerForceId is not int player) return;
                var officers = rules.PrepareDefinitionOfficerInformationPages(world, player, pages.Kind, definition);
                if (officers.Available && officers.Value is { } known)
                    OpenInformationOfficerPages(browser, rules, world, known, Adopt);
                else GD.PushWarning(officers.UnresolvedInput!);
            }), CurrentListScrollSpeed, TryBorrowApplicationUiResources(list), pages.Kind == InformationListKind.Scenery
                ? id => RunInspectionUiAction(world, () => OpenInformationSceneryDetail(browser,rules,world,pages,id,list.RefreshSelectionReturn))
                : pages.Kind == InformationListKind.Items
                    ? id => RunInspectionUiAction(world, () => OpenInformationItem(browser,rules,world,pages.CurrentDefinitionIds,id,
                        list.ApplyDetailSelectionReturn,pages.NameSortAscending)) : null));
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
