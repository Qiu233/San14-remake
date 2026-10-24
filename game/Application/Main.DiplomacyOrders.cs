using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private DiplomacyOrdersView? _diplomacyOrdersView;
    private DiplomacyTargetSelectionView? _diplomacyTargets;
    private TalentPersonSelectionView? _diplomacyPersons;
    private MerchantAmountMenuView? _diplomacyAmounts;
    private UiSourcePulldownMenu? _diplomacyMonths;
    private DiplomacyUiDraft? _diplomacyDraft;
    private GameplaySession? _diplomacySession;
    private GameState? _diplomacyWorld;
    private MapSceneView? _diplomacyMap;
    private AreaAdministrationOfficerContent? _diplomacyCardContent;
    private bool HasDiplomacyOrdersPage => _diplomacyOrdersView is not null;
    private bool IsDiplomacyOrdersCurrent => !_failed&&!_shuttingDown&&_diplomacyOrdersView is not null&&_diplomacyDraft is not null&&
        ReferenceEquals(_session,_diplomacySession)&&ReferenceEquals(_world,_diplomacyWorld)&&_residentMap==_diplomacyMap&&_diplomacyMap is not null;
    private bool CanEditDiplomacyOrders => IsDiplomacyOrdersCurrent&&_diplomacyTargets is null&&_diplomacyPersons is null&&
        _diplomacyAmounts is null&&_diplomacyMonths is null;

    private void OpenDiplomacyOrders(DiplomaticMissionKind kind)
    {
        if(_failed||_shuttingDown||_advancingPeriod||IsReturningToTitle||HasAdministrationPage||HasMilitaryOrderPage||
            HasCampaignSavePage||HasMapSystemMenu||HasCommandWorkspace||HasActionReport||HasPersonnelConfirmation||
            _detailsView is not null||_repository is null||_campaignComposition is not {} composition||
            _session is not { PlayerForceId:int player } session||session.HasPendingOperation||
            session.World is not { Phase:TurnPhase.Strategy,NativeArmySimulation:not null } world||
            _residentMap is not { IsPlayableMapReady:true } map)return;
        var district=world.Districts.Values.SingleOrDefault(value=>value.ForceId==player&&value.Number==1);
        if(district is null){_playMessage.Text=_ui.Get("detail_unknown");return;}
        DiplomacyUiDraft draft;
        try { draft=composition.PrepareArmyQueries(world).OpenDiplomacyUiDraft(world,player,district.Id,kind,new(_presentationRandom.State)); }
        catch(InvalidDataException error){_playMessage.Text=_ui.Get("play_diplomacy_rule_unavailable");GD.PushWarning(error.ToString());return;}
        _diplomacyDraft=draft;_diplomacySession=session;_diplomacyWorld=world;_diplomacyMap=map;
        if(draft.AppRandomState is {} openedRandom)_presentationRandom.InstallCommittedState(openedRandom.State);
        _diplomacyCardContent=new(_repository);
        var view=new DiplomacyOrdersView();_diplomacyOrdersView=view;AddChild(view);
        try
        {
            SetMapFocus(true);map.CloseCityCommandMenu();map.SetDetailsOpen(true,keepMapVisible:true);HideDeploymentHud();
            view.Configure(_repository,_ui,kind,()=>CanEditDiplomacyOrders,
                ()=>RunUiAction(RequestDiplomacyOrdersBack),()=>RunPlayAction(ConfirmDiplomacyOrders),
                ()=>RunUiAction(()=>OpenDiplomacyTargetSelection(false)),()=>RunUiAction(()=>OpenDiplomacyTargetSelection(true)),
                ()=>RunUiAction(OpenDiplomacyExecutorSelection),()=>RunUiAction(OpenDiplomacyMonthSelection),
                kind=>RunUiAction(()=>OpenDiplomacyGiftSelection(kind)),()=>RunUiAction(()=>ChangeDiplomacyDraft(draft.ResetGift)));
            RefreshDiplomacyOrders();
        }
        catch{CloseDiplomacyOrders();throw;}
    }

    private void RefreshDiplomacyOrders()
    {
        if(!IsDiplomacyOrdersCurrent)return;
        var draft=_diplomacyDraft!;var world=_diplomacyWorld!;
        string Unknown()=>_ui.Get("detail_unknown");
        string ForceName(int? id)=>id is int force&&world.Forces.TryGetValue(force,out var faction)?faction.Name:Unknown();
        string LocationName(int? id)=>id is int city?world.Cities.GetValueOrDefault(city)?.Name??world.Gates.GetValueOrDefault(city)?.Name??Unknown():Unknown();
        var card=new AreaAdministrationOfficerValues(_ui.Get("play_diplomacy_executor"),null,null,null,null);
        if(draft.SelectedExecutorOfficerId is int officer)
        {
            var query=draft.QueryOfficerCard(officer);
            card=query.Available?_diplomacyCardContent!.Create(query.Value!):new(world.Officers[officer].Name,null,null,null,null);
        }
        LoadedSprite? advisorPortrait=null;
        if(draft.AdvisorOfficerId is int advisor)
        {
            var query=draft.QueryOfficerCard(advisor);
            if(query.Available)advisorPortrait=_diplomacyCardContent!.Create(query.Value!).Portrait;
            else GD.PushWarning(query.UnresolvedInput);
        }
        if(draft.AdviceUnresolvedInput is {} adviceMissing)GD.PushWarning(adviceMissing);
        string relation=draft.RelationLevel is int level&&level is >=0 and <=5
            ? DiplomacyDataText(0,0x0141+level):Unknown();
        int? targetForce=draft.SelectedTargetCityId is int target?world.Cities.GetValueOrDefault(target)?.ForceId:null;
        int giftKind=draft.GiftItemId is not null?2:draft.GiftAreaId is not null?3:draft.GiftSupplies!=0?1:draft.GiftGold!=0?0:-1;
        string gift=draft.GiftItemName??draft.GiftAreaName??(draft.GiftGold!=0?DiplomacyDataText(1,0x05F8,draft.GiftGold):
            draft.GiftSupplies!=0?DiplomacyDataText(1,0x059D,draft.GiftSupplies):"");
        _diplomacyOrdersView!.Apply(new(ForceName(draft.SelectedTargetForceId),LocationName(draft.SelectedTargetCityId),ForceName(targetForce),
            relation,card,draft.TravelDays,draft.AvailableBooks,draft.RequiredBooks,gift,giftKind,
            DiplomacyDataText(1,0x045F,draft.AllianceMonths),draft.ExistingAllianceMonths is int existing
                ? existing==0?DiplomacyDataText(1,0x0353):DiplomacyDataText(1,0x045F,existing):Unknown(),
            draft.CanSubmit,draft.ExecutorOfficerIds.Count>0,draft.TargetCityIds.Count>0,
            draft.CanChooseGold==true,draft.CanChooseSupplies==true,draft.CanChooseItem==true,draft.CanChooseArea==true,
            draft.UnresolvedInput is not null||draft.AdviceUnresolvedInput is not null||draft.GiftUnresolvedInput is not null
                ?_ui.Get("play_diplomacy_rule_unavailable"):draft.AdviceText,advisorPortrait));
        _diplomacyOrdersView.SetBusy(!CanEditDiplomacyOrders);
    }

    private void OpenDiplomacyTargetSelection(bool city)
    {
        if(!CanEditDiplomacyOrders||_repository is null)return;
        var draft=_diplomacyDraft!;var world=_diplomacyWorld!;
        var rows=(city?draft.TargetCityIds:draft.TargetForceIds).Select(id=>city?
            new DiplomacyTargetChoice(id,world.Cities[id].Name,world.Cities[id].ForceId is int force?
                world.Forces.GetValueOrDefault(force)?.Name??_ui.Get("detail_unknown"):_ui.Get("detail_unknown")):
            new DiplomacyTargetChoice(id,world.Forces[id].Name,"")).ToArray();
        var selector=new DiplomacyTargetSelectionView();_diplomacyTargets=selector;AddChild(selector);
        _diplomacyOrdersView!.SetBusy(true);_diplomacyOrdersView.Hide();
        try
        {
            selector.Configure(_repository,_ui,rows,city?draft.SelectedTargetCityId:draft.SelectedTargetForceId,city?0x0592:0x058D,
                ()=>IsDiplomacyOrdersCurrent&&_diplomacyTargets==selector,chosen=>RunUiAction(()=>
                {
                    if(!IsDiplomacyOrdersCurrent||_diplomacyTargets!=selector||_diplomacyDraft!=draft)return;
                    _diplomacyTargets=null;selector.Abandon();selector.QueueFree();_diplomacyOrdersView!.Show();
                    if(chosen is int id)ChangeDiplomacyDraft(()=>{if(city)draft.SelectTargetCity(id);else draft.SelectForce(id);});
                    else ChangeDiplomacyDraft(draft.RefreshAdvice);
                }),CurrentListScrollSpeed);
        }
        catch{_diplomacyTargets=null;selector.Abandon();selector.QueueFree();_diplomacyOrdersView!.Show();RefreshDiplomacyOrders();throw;}
    }

    private void OpenDiplomacyExecutorSelection()
    {
        if(!CanEditDiplomacyOrders||_repository is null)return;
        var draft=_diplomacyDraft!;var world=_diplomacyWorld!;
        var rows=draft.ExecutorOfficerIds.Select(id=>
        {
            var actor=world.Officers[id];var query=draft.QueryOfficerCard(id);
            int? Ability(int dimension)=>query.Available?query.Value!.Abilities.FirstOrDefault(value=>value.Dimension==dimension)?.Effective.Value:null;
            string location=actor.LocationId is int city?world.Cities.GetValueOrDefault(city)?.Name??world.Gates.GetValueOrDefault(city)?.Name??_ui.Get("detail_unknown"):_ui.Get("detail_unknown");
            return new TalentPersonChoice(id,actor.Name,location,actor.ForceId is int force?world.Forces.GetValueOrDefault(force)?.Name??_ui.Get("detail_unknown"):_ui.Get("detail_unknown"),Ability(2),Ability(3),Ability(4));
        }).ToArray();
        var selector=new TalentPersonSelectionView();_diplomacyPersons=selector;AddChild(selector);
        _diplomacyOrdersView!.SetBusy(true);_diplomacyOrdersView.Hide();
        try
        {
            selector.Configure(_repository,_ui,rows,draft.SelectedExecutorOfficerId is int actor?[actor]:[],1,true,
                ()=>IsDiplomacyOrdersCurrent&&_diplomacyPersons==selector,chosen=>RunUiAction(()=>
                {
                    if(!IsDiplomacyOrdersCurrent||_diplomacyPersons!=selector||_diplomacyDraft!=draft)return;
                    _diplomacyPersons=null;selector.Abandon();selector.QueueFree();_diplomacyOrdersView!.Show();
                    if(chosen is {Count:1})ChangeDiplomacyDraft(()=>draft.SelectExecutor(chosen[0]));else ChangeDiplomacyDraft(draft.RefreshAdvice);
                }),CurrentListScrollSpeed,titleMessage:0x058F);
        }
        catch{_diplomacyPersons=null;selector.Abandon();selector.QueueFree();_diplomacyOrdersView!.Show();RefreshDiplomacyOrders();throw;}
    }

    private void OpenDiplomacyGiftSelection(int kind)
    {
        if(!CanEditDiplomacyOrders||_repository is null)return;
        var draft=_diplomacyDraft!;var parent=_diplomacyOrdersView!;
        if(kind is 0 or 1)
        {
            bool gold=kind==0;
            var query=gold?draft.OpenGoldSelection():draft.OpenSuppliesSelection();
            if(!query.Available||query.Value is not {Allowed:true} range)
            {parent.ApplyNotice(_ui.Get("play_diplomacy_rule_unavailable"));return;}
            var menu=new MerchantAmountMenuView();_diplomacyAmounts=menu;AddChild(menu);parent.SetBusy(true);parent.Hide();
            try
            {
                menu.Configure(_repository,_ui,range.Presets,gold?draft.GiftGold:draft.GiftSupplies,
                    range.Minimum,range.Maximum,()=>IsDiplomacyOrdersCurrent&&_diplomacyAmounts==menu,
                    chosen=>RunUiAction(()=>
                    {
                        if(!IsDiplomacyOrdersCurrent||_diplomacyAmounts!=menu||_diplomacyDraft!=draft)return;
                        _diplomacyAmounts=null;menu.Abandon();menu.QueueFree();parent.Show();
                        if(chosen is int amount)ChangeDiplomacyDraft(()=>{if(gold)draft.SetGiftGold(amount);else draft.SetGiftSupplies(amount);});
                        else RefreshDiplomacyOrders();
                    }),titleMessage:gold?0x020C:0x020D,calculatorPosition:new(860,352));
            }
            catch{_diplomacyAmounts=null;menu.Abandon();menu.QueueFree();parent.Show();RefreshDiplomacyOrders();throw;}
            return;
        }
        bool item=kind==2;
        if(kind is not (2 or 3)||(item?draft.CanChooseItem:draft.CanChooseArea)!=true)return;
        var choices=item?draft.ItemChoices:draft.AreaChoices;
        if(!choices.Available)
        {parent.ApplyNotice(_ui.Get("play_diplomacy_rule_unavailable"));return;}
        var selector=new DiplomacyTargetSelectionView();_diplomacyTargets=selector;AddChild(selector);parent.SetBusy(true);parent.Hide();
        try
        {
            selector.Configure(_repository,_ui,choices.Value!.Select(row=>new DiplomacyTargetChoice(row.Id,row.Name,"")).ToArray(),
                item?draft.GiftItemId:draft.GiftAreaId,item?0x058C:0x05AF,
                ()=>IsDiplomacyOrdersCurrent&&_diplomacyTargets==selector,chosen=>RunUiAction(()=>
                {
                    if(!IsDiplomacyOrdersCurrent||_diplomacyTargets!=selector||_diplomacyDraft!=draft)return;
                    _diplomacyTargets=null;selector.Abandon();selector.QueueFree();parent.Show();
                    if(chosen is int id)ChangeDiplomacyDraft(()=>{if(item)draft.SelectGiftItem(id);else draft.SelectGiftArea(id);});
                    else RefreshDiplomacyOrders();
                }),CurrentListScrollSpeed);
        }
        catch{_diplomacyTargets=null;selector.Abandon();selector.QueueFree();parent.Show();RefreshDiplomacyOrders();throw;}
    }

    private void OpenDiplomacyMonthSelection()
    {
        if(!CanEditDiplomacyOrders||_repository is null||_diplomacyDraft!.Kind!=DiplomaticMissionKind.Alliance)return;
        var draft=_diplomacyDraft;int[] choices=[6,12,24];
        var menu=new UiSourcePulldownMenu();_diplomacyMonths=menu;_diplomacyOrdersView!.AddSourceOverlay(menu);_diplomacyOrdersView.SetBusy(true);
        try
        {
            var box=_diplomacyOrdersView.MonthsRectangle;
            menu.Configure(_repository,_ui.SourceLocale,new(box.Position,new(box.Size.X,120)),
                choices.Select(value=>new UiSourcePulldownItem(DiplomacyDataText(1,0x045F,value),draft.CanChooseMonths(value))).ToArray(),
                UiSourcePulldownOutsideClick.ConfirmConsumed,chosen=>RunUiAction(()=>
                {
                    if(!IsDiplomacyOrdersCurrent||_diplomacyMonths!=menu||_diplomacyDraft!=draft)return;
                    _diplomacyMonths=null;menu.Abandon();menu.QueueFree();
                    if(chosen is int index)ChangeDiplomacyDraft(()=>draft.SetAllianceMonths(choices[index]));else ChangeDiplomacyDraft(draft.RefreshAdvice);
                }));
        }
        catch{_diplomacyMonths=null;menu.Abandon();menu.QueueFree();RefreshDiplomacyOrders();throw;}
    }

    private void ChangeDiplomacyDraft(Action action)
    {
        try{action();if(_diplomacyDraft?.AppRandomState is {} random)_presentationRandom.InstallCommittedState(random.State);}
        catch(InvalidDataException error){GD.PushWarning(error.ToString());}
        RefreshDiplomacyOrders();
    }
    private string DiplomacyDataText(int section,int id,int? value=null)
    {
        string format=_repository!.LoadMessages(_ui.SourceLocale,San14.Data.Formats.MessageKind.Data)
            .Messages.Sections[section].Records[id].Text??throw new InvalidDataException($"Missing diplomacy text {section}:{id:X4}.");
        if(value is not int number)return format;
        if(format.Split("%d").Length!=2)throw new InvalidDataException($"Unsupported diplomacy number format {id:X4}.");
        return format.Replace("%d",number.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
    private void ConfirmDiplomacyOrders()
    {
        if(!CanEditDiplomacyOrders||_diplomacyDraft?.CanSubmit!=true)return;
        var view=_diplomacyOrdersView!;view.SetBusy(true);
        try{if(!ApplyPlayableOrder(_diplomacyDraft.BuildOrder(),CloseDiplomacyOrders)){RefreshDiplomacyOrders();view.ApplyNotice(_playMessage.Text);}}
        catch{if(IsDiplomacyOrdersCurrent)RefreshDiplomacyOrders();throw;}
    }
    private void RequestDiplomacyOrdersBack()
    {
        if(!IsDiplomacyOrdersCurrent)return;
        if(_diplomacyTargets is {} targets){targets.RequestCancel();return;}
        if(_diplomacyPersons is {} persons){persons.RequestCancel();return;}
        if(_diplomacyAmounts is {} amounts){amounts.RequestCancel();return;}
        if(_diplomacyMonths is {} months){months.RequestCancel();return;}
        CloseDiplomacyOrders();
    }
    private void CloseDiplomacyOrders()
    {
        if(_diplomacyOrdersView is null&&_diplomacyDraft is null&&_diplomacyMap is null)return;
        var map=_diplomacyMap;_diplomacyMap=null;_diplomacyWorld=null;_diplomacySession=null;_diplomacyDraft=null;_diplomacyCardContent=null;
        var targets=_diplomacyTargets;_diplomacyTargets=null;var persons=_diplomacyPersons;_diplomacyPersons=null;
        var months=_diplomacyMonths;_diplomacyMonths=null;
        var amounts=_diplomacyAmounts;_diplomacyAmounts=null;
        var view=_diplomacyOrdersView;_diplomacyOrdersView=null;
        targets?.Abandon();targets?.QueueFree();persons?.Abandon();persons?.QueueFree();
        amounts?.Abandon();amounts?.QueueFree();
        months?.Abandon();months?.QueueFree();view?.Abandon();view?.QueueFree();
        if(map is not null&&GodotObject.IsInstanceValid(map)&&_residentMap==map)map.SetDetailsOpen(false);
        if(!_failed&&!_shuttingDown&&_residentMap is not null)SetMapFocus(_mapFocused);
    }
}
