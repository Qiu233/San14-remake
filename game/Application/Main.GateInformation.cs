using San14.Core;
using San14.Data;

public partial class Main
{
    private void OpenInformationGate(MapInformationBrowserView browser, ArmyCampaignRuleAdapter rules,
        GameState world, int gateId, Action<GameState> adoptWorld,
        IReadOnlyList<int>? selectionIds = null,
        Action<InformationDetailSelectionReturn>? returnSelection = null, bool? initialNameSortAscending = null)
    {
        if (_repository is null) return;
        var detail = new GateDetailsView();
        void Adopt(GameState next)
        {
            world = next; rules = _campaignComposition!.PrepareArmyQueries(next); adoptWorld(next);
            detail.Apply(rules.QueryGateInformation(next, detail.CurrentGateId));
        }
        var page = rules.QueryGateInformation(world, gateId);
        var selection = PrepareDetailSelection(rules,world,InformationListKind.Gates,gateId,selectionIds)
            with { InitialNameSortAscending = initialNameSortAscending };
        browser.ShowDetail(detail, close =>
        {
            RetainCommandDetailSpeechWorld(detail, next =>
            {
                world = next;
                rules = _campaignComposition!.PrepareArmyQueries(next);
            });
            detail.Configure(_repository, _ui, page, () =>
            {
                returnSelection?.Invoke(detail.ReadSelectionReturn()); close();
            }, () => RunInspectionUiAction(world, () => OpenInformationGateResidents(browser,rules,world,detail.CurrentGateId,Adopt)),
                selection, id => RunInspectionUiAction(world, () =>
                {
                    if (IsInformationBrowserCurrent(browser) && browser.IsCurrentDetail(detail))
                        detail.Apply(rules.QueryGateInformation(world,id));
                }));
        });
    }

    private void OpenInformationGateResidents(MapInformationBrowserView browser, ArmyCampaignRuleAdapter rules,
        GameState world, int gateId, Action<GameState> adoptWorld)
    {
        if (_repository is null || !IsInformationBrowserCurrent(browser) || _session?.PlayerForceId is not int player) return;
        var membership = rules.QueryGateResidentOfficerIds(world, gateId);
        if (!membership.Available || membership.Value is not { } ids)
        {
            browser.ApplyNotice(_ui.Get("information_partial"));
            return;
        }
        OpenInformationOfficerPages(browser, rules, world,
            rules.PrepareOfficerInformationPages(world, player, ids), adoptWorld);
    }
}
