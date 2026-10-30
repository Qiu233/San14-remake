using Godot;
using San14.Core;
using San14.Data;

public partial class Main
{
    private DiplomacyPersonVisual PrepareDiplomacyPersonVisual(DiplomacyArrivalPerson person)
    {
        var route=person.Portrait;
        var portrait=route.Kind==OfficerPortraitRouteKind.StockNamed
            ?new LocalizedUiSpriteSource(_repository!).LoadTexturePackSprite(route.ResourceName!,route.SpriteName!):null;
        return new(person.OfficerId,person.Name??_ui.Get("detail_unknown"),portrait,person.SpecialTrait,
            person.TraitUnresolvedInput,person.NameUnresolvedInput??route.UnresolvedInput);
    }
    private DiplomacyPeopleVisuals PrepareDiplomacyArrivalVisuals(ArmyCampaignRuleAdapter rules,GameState world,
        DiplomacyNegotiationRequest request,int shownForce)
    {
        var people=rules.QueryDiplomacyArrivalPeople(world,request,shownForce);
        return new(people.Ruler.Available?PrepareDiplomacyPersonVisual(people.Ruler.Value!):null,
            people.Captives.Select(PrepareDiplomacyPersonVisual).ToArray(),people.Ruler.UnresolvedInput,people.CaptivesUnresolvedInput);
    }
    private IReadOnlyList<DiplomacyPersonVisual> PrepareDiplomacyCaptiveVisuals(DiplomacyUiDraft draft,GameState world,out string? missing)
    {
        missing=null;if(draft.Kind!=DiplomaticMissionKind.ReturnCaptives||draft.SelectedCaptiveOfficerIds.Count==0)return [];
        var query=_campaignComposition!.PrepareArmyQueries(world).QueryDiplomacyCaptivePeople(world,draft.SelectedCaptiveOfficerIds);
        missing=query.UnresolvedInput;
        if(query.Available)return query.Value.Select(PrepareDiplomacyPersonVisual).ToArray();
        return draft.SelectedCaptiveOfficerIds.Select(id=>new DiplomacyPersonVisual(id,
            draft.CaptiveChoices.First(row=>row.OfficerId==id).Name,null,null,query.UnresolvedInput,query.UnresolvedInput)).ToArray();
    }
    private DeploymentCalculatorDialog? _diplomacyTradeCalculator;
    private static int OfferKind(DiplomacyOffer offer)=>offer.ItemId is not null?2:offer.AreaId is not null?3:
        offer.Supplies!=0?1:offer.Gold!=0?0:-1;
    private string DiplomacyRequestedDescription(DiplomacyUiDraft draft)
    {
        var offer=draft.RequestedOffer;
        if(offer.Gold!=0)return DiplomacyDataText(1,0x05F8,offer.Gold);
        if(offer.Supplies!=0)return DiplomacyDataText(1,0x059D,offer.Supplies);
        if(offer.ItemId is int item)return draft.RequestedItemChoices.Value?.FirstOrDefault(row=>row.Id==item)?.Name??_ui.Get("detail_unknown");
        if(offer.AreaId is int area)return draft.RequestedAreaChoices.Value?.FirstOrDefault(row=>row.Id==area)?.Name??_ui.Get("detail_unknown");
        return "";
    }

    private void OpenDiplomacyCaptiveSelection()
    {
        if(!CanEditDiplomacyOrders||_repository is null||_diplomacyDraft is not{Kind:DiplomaticMissionKind.ReturnCaptives} draft)return;
        if(draft.CaptiveChoicesUnresolvedInput is{} missing)
        {GD.PushWarning(missing);_diplomacyOrdersView!.ApplyNotice(_ui.Get("play_diplomacy_rule_unavailable"));return;}
        var query=draft.ReadPrisonerSelection();
        if(!query.Available || query.Value is not {} model)
        {
            if(query.UnresolvedInput is {} reason)GD.PushWarning(reason);
            _diplomacyOrdersView!.ApplyNotice(_ui.Get("play_diplomacy_rule_unavailable"));return;
        }
        if(model.Pages.CurrentOfficerIds.Length==0)return;
        var selector=new DiplomacyPrisonerSelectionView();_diplomacyPersons=selector;AddChild(selector);
        _diplomacySelectionPurpose=DiplomacySelectionPurpose.Captives;
        _diplomacyOrdersView!.SetBusy(true);_diplomacyOrdersView.Hide();
        try
        {
            selector.Configure(_repository,_ui,_diplomacyOrdersView.SharedResources,model,
                ()=>IsDiplomacyOrdersCurrent&&_diplomacyPersons==selector,chosen=>RunUiAction(()=>
                {
                    if(!IsDiplomacyOrdersCurrent||_diplomacyPersons!=selector||_diplomacyDraft!=draft)return;
                    _diplomacyPersons=null;_diplomacySelectionPurpose=DiplomacySelectionPurpose.None;
                    selector.Abandon();selector.QueueFree();_diplomacyOrdersView!.Show();
                    if(chosen is not null)ChangeDiplomacyDraft(()=>draft.SelectCaptives(chosen),DiplomacySelectionPurpose.Captives);
                    else RefreshDiplomacyOrders();
                }),CurrentListScrollSpeed);
        }
        catch
        {
            _diplomacyPersons=null;_diplomacySelectionPurpose=DiplomacySelectionPurpose.None;
            selector.Abandon();selector.QueueFree();_diplomacyOrdersView.Show();RefreshDiplomacyOrders();throw;
        }
    }

    private void OpenDiplomacyTradeSelection(int kind,bool requested)
    {
        if(!CanEditDiplomacyOrders||_repository is null||_diplomacyDraft is not{Kind:DiplomaticMissionKind.Exchange} draft)return;
        var parent=_diplomacyOrdersView!;
        if(kind is 0 or 1)
        {
            bool gold=kind==0;
            var query=requested?(gold?draft.OpenRequestedGoldSelection():draft.OpenRequestedSuppliesSelection()):
                gold?draft.OpenGoldSelection():draft.OpenSuppliesSelection();
            if(query.Value is not{Allowed:true} range||!query.Available)
            {parent.ApplyNotice(_ui.Get("play_diplomacy_rule_unavailable"));return;}
            var calculator=new DeploymentCalculatorDialog();_diplomacyTradeCalculator=calculator;AddChild(calculator);
            parent.SetBusy(true);parent.Hide();
            try
            {
                int current=requested?(gold?draft.RequestedOffer.Gold:draft.RequestedOffer.Supplies):gold?draft.GiftGold:draft.GiftSupplies;
                calculator.Configure(_repository,_ui,current,range.Minimum,range.Maximum,chosen=>RunUiAction(()=>
                {
                    if(!IsDiplomacyOrdersCurrent||_diplomacyTradeCalculator!=calculator||_diplomacyDraft!=draft)return;
                    _diplomacyTradeCalculator=null;calculator.Abandon();calculator.QueueFree();parent.Show();
                    if(chosen is int value)ChangeDiplomacyDraft(()=>
                    {
                        if(requested){if(gold)draft.SelectRequestedGold(value);else draft.SelectRequestedSupplies(value);}
                        else{if(gold)draft.SetGiftGold(value);else draft.SetGiftSupplies(value);}
                    });
                    else RefreshDiplomacyOrders();
                }),panelPosition:new(860,352),titleMessage:gold?0x020C:0x020D);
            }
            catch{_diplomacyTradeCalculator=null;calculator.Abandon();calculator.QueueFree();parent.Show();RefreshDiplomacyOrders();throw;}
            return;
        }
        if(kind is not(2 or 3))return;
        bool item=kind==2;
        bool? allowed=requested?(item?draft.CanChooseRequestedItem:draft.CanChooseRequestedArea):item?draft.CanChooseItem:draft.CanChooseArea;
        if(allowed!=true)return;
        var choices=requested?(item?draft.RequestedItemChoices:draft.RequestedAreaChoices):item?draft.ItemChoices:draft.AreaChoices;
        if(!choices.Available){parent.ApplyNotice(_ui.Get("play_diplomacy_rule_unavailable"));return;}
        var selector=new DiplomacyTargetSelectionView();_diplomacyTargets=selector;AddChild(selector);
        parent.SetBusy(true);parent.Hide();
        try
        {
            int? current=requested?(item?draft.RequestedOffer.ItemId:draft.RequestedOffer.AreaId):item?draft.GiftItemId:draft.GiftAreaId;
            selector.Configure(_repository,_ui,draft.ReadTargetSelection(item?DiplomacyTargetSelectionKind.Item:DiplomacyTargetSelectionKind.Area,requested),
                current,item?0x058C:0x05AF,()=>IsDiplomacyOrdersCurrent&&_diplomacyTargets==selector,chosen=>RunUiAction(()=>
                {
                    if(!IsDiplomacyOrdersCurrent||_diplomacyTargets!=selector||_diplomacyDraft!=draft)return;
                    _diplomacyTargets=null;selector.Abandon();selector.QueueFree();parent.Show();
                    if(chosen is int id)ChangeDiplomacyDraft(()=>
                    {
                        if(requested){if(item)draft.SelectRequestedItem(id);else draft.SelectRequestedArea(id);}
                        else{if(item)draft.SelectGiftItem(id);else draft.SelectGiftArea(id);}
                    });
                    else RefreshDiplomacyOrders();
                }),CurrentListScrollSpeed);
        }
        catch{_diplomacyTargets=null;selector.Abandon();selector.QueueFree();parent.Show();RefreshDiplomacyOrders();throw;}
    }
}
