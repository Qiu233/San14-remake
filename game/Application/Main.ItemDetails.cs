using San14.Core;
using San14.Data;

public partial class Main
{
    private void OpenInformationItem(MapInformationBrowserView browser,ArmyCampaignRuleAdapter rules,
        GameState world,InformationList publication,int itemId)
    {
        OpenInformationItem(browser,rules,world,DetailSelectionIds(publication,InformationListKind.Items),itemId);
    }

    private void OpenInformationItem(MapInformationBrowserView browser,ArmyCampaignRuleAdapter rules,
        GameState world,IReadOnlyList<int> orderedIds,int itemId,
        Action<InformationDetailSelectionReturn>? returnSelection=null,bool? initialNameSortAscending=null)
    {
        if(_repository is null)return;
        var context=PrepareDetailSelection(rules,world,InformationListKind.Items,itemId,
            orderedIds) with {InitialNameSortAscending=initialNameSortAscending};
        var detail=new ItemDetailsView();
        browser.ShowDetail(detail,close=>
        {
            RetainCommandDetailSpeechWorld(detail, next =>
            {
                world = next;
                rules = _campaignComposition!.PrepareArmyQueries(next);
            });
            detail.Configure(_repository,_ui,rules.QueryItemInformation(world,itemId),
                context,()=>{returnSelection?.Invoke(detail.ReadSelectionReturn());close();},id=>RunUiAction(()=>detail.Apply(rules.QueryItemInformation(world,id))),
                ()=>RunUiAction(()=>
                {
                    if(!IsInformationBrowserCurrent(browser))return;
                    var poem=rules.ReadItemPoemInformation(world,detail.CurrentItemId);
                    var view=new ItemPoemDetailsView();
                    browser.ShowDetail(view,back=>view.Configure(_repository,_ui,poem,back,CurrentListScrollSpeed));
                }));
        });
    }
}
