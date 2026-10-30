using San14.Core;
using San14.Data;

public partial class Main
{
    private void ConfigureCurrentForceDetail(ForceDetailsView view, GameState world, int forceId,
        Action close, ArmyCampaignRuleAdapter? preparedRules = null, IReadOnlyList<int>? selectionIds = null,
        Action<InformationDetailSelectionReturn>? returnSelection = null, bool? initialNameSortAscending = null)
    {
        var rules = preparedRules ?? _campaignComposition?.PrepareArmyQueries(world)
            ?? throw new InvalidDataException("force-information.current_campaign_rules");
        RetainCommandDetailSpeechWorld(view, next =>
        {
            world = next;
            rules = _campaignComposition!.PrepareArmyQueries(next);
        });
        view.Configure(_repository ?? throw new InvalidDataException("force-information.resource_owner"),
            rules.QueryForceInformation(world, forceId), _ui, Return,
            PrepareDetailSelection(rules, world, InformationListKind.Forces, forceId, selectionIds) with { InitialNameSortAscending = initialNameSortAscending },
            id => RunUiAction(() => view.Apply(rules.QueryForceInformation(world, id))));
        void Return()
        {
            returnSelection?.Invoke(view.ReadSelectionReturn());
            close();
        }
    }
}
