using Godot;
using San14.Core;
using San14.Data;

public partial class Main
{
    private void OpenInformationSceneryDetail(MapInformationBrowserView browser,ArmyCampaignRuleAdapter rules,
        GameState world,DefinitionInformationPageSet pages,int sceneryId,Action returned)
    {
        if(!IsInformationBrowserCurrent(browser))return;
        var prepared=rules.PrepareSceneryCatalogInformation(world,sceneryId);
        if(!prepared.Available||prepared.Value is not { } initial){GD.PushWarning(prepared.UnresolvedInput!);return;}
        // Names come from this same immutable-world list; preserve sorted order
        // and duplicate registered identities rather than constructing a title catalog.
        var namePage=pages.ReadPage(0);
        int nameColumn=pages.Groups[0].Columns.IndexOf(pages.Groups[0].Columns.First(column=>column.Id==0));
        var entries=namePage.Rows.Select(row=>new CollectionDetailEntry(row.DefinitionId,row.Cells[nameColumn].Text??_ui.Get("detail_unknown"))).ToArray();
        var detail=new MapInformationSceneryDetailView();
        browser.ShowDetail(detail,close=>detail.Configure(_repository!,_ui,initial,entries,
            ()=>IsInformationBrowserCurrent(browser)&&browser.IsCurrentDetail(detail),
            ()=>{returned();close();},Select,CurrentListScrollSpeed));
        void Select(int id)
        {
            if(!IsInformationBrowserCurrent(browser)||!browser.IsCurrentDetail(detail))return;
            var next=rules.PrepareSceneryCatalogInformation(world,id);
            if(next.Available&&next.Value is { } page)
            {
                var selected=pages.AdoptDetailOrder(0,pages.CurrentDefinitionIds,id,pages.NameSortAscending);
                if(!selected.Available){GD.PushWarning(selected.UnresolvedInput!);return;}
                detail.Apply(page);
            }
            else GD.PushWarning(next.UnresolvedInput!);
        }
    }
}
