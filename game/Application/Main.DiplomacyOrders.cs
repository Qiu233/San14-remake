using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private enum DiplomacySelectionPurpose { None, Force, City, Executor, Months, Gold, Supplies, GiftItem, GiftArea, Captives }
    private DiplomacySelectionPurpose _diplomacySelectionPurpose;
    private long _diplomacySelectionAcceptedSequence;
    private DiplomacySelectionPurpose _lastDiplomacyAcceptedPurpose;
    private int? _lastDiplomacyAcceptedId;
    private DiplomaticMissionKind? _lastDiplomacyAcceptedMissionKind;
    private Func<int,bool>? DiplomacyForceAdmission;
    private Func<int,bool>? DiplomacyExecutorAdmission;
    private bool DiplomacySelectionReturnAllowed=true;

    private void RefreshDiplomacySelectionAdmission()
    {
        _diplomacyTargets?.ApplySelectionAdmission(_diplomacySelectionPurpose==DiplomacySelectionPurpose.Force
            ?DiplomacyForceAdmission:null,DiplomacySelectionReturnAllowed);
        _diplomacyPersons?.ApplySelectionAdmission(DiplomacyExecutorAdmission,DiplomacySelectionReturnAllowed);
        _diplomacyExecutors?.ApplySelectionAdmission(DiplomacyExecutorAdmission,DiplomacySelectionReturnAllowed);
    }
    private DiplomacyOrdersView? _diplomacyOrdersView;
    private DiplomacyTargetSelectionView? _diplomacyTargets;
    private DiplomacyPrisonerSelectionView? _diplomacyPersons;
    private DiplomacyExecutorSelectionView? _diplomacyExecutors;
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
    private bool CanEditDiplomacyOrders => IsDiplomacyOrdersCurrent&&_diplomacyTargets is null&&_diplomacyPersons is null&&_diplomacyExecutors is null&&
        _diplomacyAmounts is null&&_diplomacyMonths is null&&_diplomacyTradeCalculator is null;

    private void AdoptDiplomacyOrdersSpeechWorld(GameplaySession session, GameState previous, GameState replacement)
    {
        if (_diplomacyOrdersView is null || _diplomacyDraft is not { } draft ||
            _diplomacySession != session || _diplomacyMap is null || _diplomacyMap != _residentMap ||
            !ReferenceEquals(_diplomacyWorld, previous) ||
            !draft.TryAdoptPresentationWorld(previous, replacement, new(_presentationRandom.State))) return;
        _diplomacyWorld = replacement;
    }

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
        _diplomacyCardContent=new();
        var view=new DiplomacyOrdersView();_diplomacyOrdersView=view;AddChild(view);
        try
        {
            SetMapFocus(true);map.CloseCityCommandMenu();map.SetDetailsOpen(true,keepMapVisible:true);HideDeploymentHud();
            view.Configure(_repository,_ui,kind,()=>CanEditDiplomacyOrders,
                ()=>RunUiAction(RequestDiplomacyOrdersBack),()=>RunPlayAction(ConfirmDiplomacyOrders),
                ()=>RunUiAction(()=>OpenDiplomacyTargetSelection(false)),()=>RunUiAction(()=>OpenDiplomacyTargetSelection(true)),
                ()=>RunUiAction(OpenDiplomacyExecutorSelection),()=>RunUiAction(OpenDiplomacyMonthSelection),
                kind=>RunUiAction(()=>OpenDiplomacyGiftSelection(kind)),()=>RunUiAction(()=>ChangeDiplomacyDraft(draft.ResetGift)),
                ()=>RunUiAction(OpenDiplomacyCaptiveSelection),kind=>RunUiAction(()=>OpenDiplomacyTradeSelection(kind,true)),
                ()=>RunUiAction(()=>ChangeDiplomacyDraft(draft.ResetRequested)), resources: TryBorrowApplicationUiResources(view));
            RefreshDiplomacyOrders();
            if(kind==DiplomaticMissionKind.Goodwill)ObserveTutorialInformation(39);
            if(kind==DiplomaticMissionKind.Alliance)ObserveTutorialInformation(40);
            BindCurrentTutorialDiplomacy();
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
        OfficerPortraitRoute? advisorPortrait=null;
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
                ?_ui.Get("play_diplomacy_rule_unavailable"):draft.AdviceText,advisorPortrait)
        {
            CaptivePeople=PrepareDiplomacyCaptiveVisuals(draft,world,out var captiveMissing),
            CaptivePeopleUnresolvedInput=captiveMissing,
            CaptiveRansomGold=draft.CaptiveRansomGold,CanChooseCaptives=draft.CaptiveChoices.Count>0&&draft.CaptiveChoicesUnresolvedInput is null,
            AvailableGold=draft.MaxGold,RequestedDescription=DiplomacyRequestedDescription(draft),
            RequestedKind=OfferKind(draft.RequestedOffer),
            CanChooseRequested=[draft.CanChooseRequestedGold==true,draft.CanChooseRequestedSupplies==true,
                draft.CanChooseRequestedItem==true,draft.CanChooseRequestedArea==true]
        });
        _diplomacyOrdersView.SetBusy(!CanEditDiplomacyOrders);
    }

    private void OpenDiplomacyTargetSelection(bool city)
    {
        if(!CanEditDiplomacyOrders||_repository is null)return;
        var draft=_diplomacyDraft!;
        if((city?draft.TargetCityIds:draft.TargetForceIds).Count==0)return;
        var page=draft.ReadTargetSelection(city?DiplomacyTargetSelectionKind.City:DiplomacyTargetSelectionKind.Force);
        var map=_diplomacyMap!;
        var selector=new DiplomacyTargetSelectionView();_diplomacyTargets=selector;AddChild(selector);
        bool Current()=>IsDiplomacyOrdersCurrent&&_diplomacyTargets==selector&&_diplomacyDraft==draft;
        void BindMode(bool list)
        {
            if(!Current())return;
            map.EndDiplomacyTargetPick(selector);map.SetDetailsOpen(list,keepMapVisible:true);
            if(!list)map.BeginDiplomacyTargetPick(selector,_diplomacyWorld!,
                page.MapSelection??throw new InvalidDataException("diplomacy.target_map.publication"),
                ()=>Current()&&selector.IsMapMode,selector.CanChooseTarget,
                id=>RunUiAction(()=>{if(Current())selector.PickMapTarget(id);}));
            HideDeploymentHud();
        }
        selector.TreeExiting+=()=>{if(GodotObject.IsInstanceValid(map))map.EndDiplomacyTargetPick(selector);};
        _diplomacySelectionPurpose=city?DiplomacySelectionPurpose.City:DiplomacySelectionPurpose.Force;
        _diplomacyOrdersView!.SetBusy(true);_diplomacyOrdersView.Hide();
        try
        {
            selector.Configure(_repository,_ui,page,null,city?0x0592:0x058D,
                ()=>IsDiplomacyOrdersCurrent&&_diplomacyTargets==selector,chosen=>RunUiAction(()=>
                {
                    if(!IsDiplomacyOrdersCurrent||_diplomacyTargets!=selector||_diplomacyDraft!=draft)return;
                    map.EndDiplomacyTargetPick(selector);map.SetDetailsOpen(true,keepMapVisible:true);
                    _diplomacyTargets=null;_diplomacySelectionPurpose=DiplomacySelectionPurpose.None;selector.Abandon();selector.QueueFree();_diplomacyOrdersView!.Show();
                    if(chosen is int id)ChangeDiplomacyDraft(()=>{if(city)draft.SelectTargetCity(id);else draft.SelectForce(id);},city?DiplomacySelectionPurpose.City:DiplomacySelectionPurpose.Force,id);
                    else ChangeDiplomacyDraft(draft.RefreshAdvice);
                }),CurrentListScrollSpeed,new DiplomacyTargetSelectionView.MapMode(city?0x3AF:0x36D,city,
                    list=>RunUiAction(()=>BindMode(list)),map.SourceRightCancelReleased));
            BindMode(!city);BindCurrentTutorialDiplomacy();
        }
        catch{map.EndDiplomacyTargetPick(selector);map.SetDetailsOpen(true,keepMapVisible:true);_diplomacyTargets=null;_diplomacySelectionPurpose=DiplomacySelectionPurpose.None;selector.Abandon();selector.QueueFree();_diplomacyOrdersView!.Show();RefreshDiplomacyOrders();throw;}
    }

    private void OpenDiplomacyExecutorSelection()
    {
        if(!CanEditDiplomacyOrders||_repository is null)return;
        var draft=_diplomacyDraft!;
        AreaAdministrationQuery<DiplomacyExecutorSelection> query;
        try { query=draft.ReadExecutorSelection(); }
        finally
        { if(draft.AppRandomState is {} random)_presentationRandom.InstallCommittedState(random.State); }
        if(!query.Available || query.Value is not {} model)
        {
            _diplomacyOrdersView!.ApplyNotice(_ui.Get("information_partial"));
            if(query.UnresolvedInput is {} missing)GD.PushWarning(missing);
            return;
        }
        var selector=new DiplomacyExecutorSelectionView();_diplomacyExecutors=selector;AddChild(selector);
        _diplomacySelectionPurpose=DiplomacySelectionPurpose.Executor;
        _diplomacyOrdersView!.SetBusy(true);_diplomacyOrdersView.Hide();
        try
        {
            selector.Configure(_repository,_ui,_diplomacyOrdersView.SharedResources,model,
                ()=>IsDiplomacyOrdersCurrent&&_diplomacyExecutors==selector,chosen=>RunUiAction(()=>
                {
                    if(!IsDiplomacyOrdersCurrent||_diplomacyExecutors!=selector||_diplomacyDraft!=draft)return;
                    _diplomacyExecutors=null;_diplomacySelectionPurpose=DiplomacySelectionPurpose.None;
                    selector.Abandon();selector.QueueFree();_diplomacyOrdersView!.Show();
                    if(chosen is int id)ChangeDiplomacyDraft(()=>draft.SelectExecutor(id),DiplomacySelectionPurpose.Executor,id);
                    else ChangeDiplomacyDraft(draft.RefreshAdvice);
                }),CurrentListScrollSpeed);
            BindCurrentTutorialDiplomacy();
        }
        catch
        {
            _diplomacyExecutors=null;_diplomacySelectionPurpose=DiplomacySelectionPurpose.None;
            selector.Abandon();selector.QueueFree();_diplomacyOrdersView!.Show();RefreshDiplomacyOrders();throw;
        }
    }

    private void OpenDiplomacyGiftSelection(int kind)
    {
        if(_diplomacyDraft?.Kind==DiplomaticMissionKind.Exchange){OpenDiplomacyTradeSelection(kind,false);return;}
        if(!CanEditDiplomacyOrders||_repository is null)return;
        var draft=_diplomacyDraft!;var parent=_diplomacyOrdersView!;
        if(kind is 0 or 1)
        {
            bool gold=kind==0;
            var query=gold?draft.OpenGoldSelection():draft.OpenSuppliesSelection();
            if(!query.Available||query.Value is not {Allowed:true} range)
            {parent.ApplyNotice(_ui.Get("play_diplomacy_rule_unavailable"));return;}
            var menu=new MerchantAmountMenuView();_diplomacyAmounts=menu;AddChild(menu);parent.SetBusy(true);parent.Hide();
            _diplomacySelectionPurpose=gold?DiplomacySelectionPurpose.Gold:DiplomacySelectionPurpose.Supplies;
            try
            {
                menu.Configure(_repository,_ui,range.Presets,gold?draft.GiftGold:draft.GiftSupplies,
                    range.Minimum,range.Maximum,()=>IsDiplomacyOrdersCurrent&&_diplomacyAmounts==menu,
                    chosen=>RunUiAction(()=>
                    {
                        if(!IsDiplomacyOrdersCurrent||_diplomacyAmounts!=menu||_diplomacyDraft!=draft)return;
                        _diplomacyAmounts=null;_diplomacySelectionPurpose=DiplomacySelectionPurpose.None;menu.Abandon();menu.QueueFree();parent.Show();
                        if(chosen is int amount)ChangeDiplomacyDraft(()=>{if(gold)draft.SetGiftGold(amount);else draft.SetGiftSupplies(amount);},gold?DiplomacySelectionPurpose.Gold:DiplomacySelectionPurpose.Supplies,amount);
                        else RefreshDiplomacyOrders();
                    }),titleMessage:gold?0x020C:0x020D,calculatorPosition:new(860,352));
            }
            catch{_diplomacyAmounts=null;_diplomacySelectionPurpose=DiplomacySelectionPurpose.None;menu.Abandon();menu.QueueFree();parent.Show();RefreshDiplomacyOrders();throw;}
            return;
        }
        bool item=kind==2;
        if(kind is not (2 or 3)||(item?draft.CanChooseItem:draft.CanChooseArea)!=true)return;
        var choices=item?draft.ItemChoices:draft.AreaChoices;
        if(!choices.Available)
        {parent.ApplyNotice(_ui.Get("play_diplomacy_rule_unavailable"));return;}
        var selector=new DiplomacyTargetSelectionView();_diplomacyTargets=selector;AddChild(selector);parent.SetBusy(true);parent.Hide();
        _diplomacySelectionPurpose=item?DiplomacySelectionPurpose.GiftItem:DiplomacySelectionPurpose.GiftArea;
        try
        {
            selector.Configure(_repository,_ui,draft.ReadTargetSelection(item?DiplomacyTargetSelectionKind.Item:DiplomacyTargetSelectionKind.Area),
                item?draft.GiftItemId:draft.GiftAreaId,item?0x058C:0x05AF,
                ()=>IsDiplomacyOrdersCurrent&&_diplomacyTargets==selector,chosen=>RunUiAction(()=>
                {
                    if(!IsDiplomacyOrdersCurrent||_diplomacyTargets!=selector||_diplomacyDraft!=draft)return;
                    _diplomacyTargets=null;_diplomacySelectionPurpose=DiplomacySelectionPurpose.None;selector.Abandon();selector.QueueFree();parent.Show();
                    if(chosen is int id)ChangeDiplomacyDraft(()=>{if(item)draft.SelectGiftItem(id);else draft.SelectGiftArea(id);},item?DiplomacySelectionPurpose.GiftItem:DiplomacySelectionPurpose.GiftArea,id);
                    else RefreshDiplomacyOrders();
                }),CurrentListScrollSpeed);
        }
        catch{_diplomacyTargets=null;_diplomacySelectionPurpose=DiplomacySelectionPurpose.None;selector.Abandon();selector.QueueFree();parent.Show();RefreshDiplomacyOrders();throw;}
    }

    private void OpenDiplomacyMonthSelection()
    {
        if(!CanEditDiplomacyOrders||_repository is null||_diplomacyDraft!.Kind!=DiplomaticMissionKind.Alliance)return;
        var draft=_diplomacyDraft;int[] choices=[6,12,24];
        var menu=new UiSourcePulldownMenu();_diplomacyMonths=menu;_diplomacyOrdersView!.AddSourceOverlay(menu);_diplomacyOrdersView.SetBusy(true);
        _diplomacySelectionPurpose=DiplomacySelectionPurpose.Months;
        try
        {
            var box=_diplomacyOrdersView.MonthsRectangle;
            menu.Configure(_repository,_ui.SourceLocale,new(box.Position,new(box.Size.X,120)),
                choices.Select(value=>new UiSourcePulldownItem(DiplomacyDataText(1,0x045F,value),draft.CanChooseMonths(value))).ToArray(),
                UiSourcePulldownOutsideClick.ConfirmConsumed,chosen=>RunUiAction(()=>
                {
                    if(!IsDiplomacyOrdersCurrent||_diplomacyMonths!=menu||_diplomacyDraft!=draft)return;
                    _diplomacyMonths=null;_diplomacySelectionPurpose=DiplomacySelectionPurpose.None;menu.Abandon();menu.QueueFree();
                    if(chosen is int index)ChangeDiplomacyDraft(()=>draft.SetAllianceMonths(choices[index]),DiplomacySelectionPurpose.Months,choices[index]);else ChangeDiplomacyDraft(draft.RefreshAdvice);
                }));
        }
        catch{_diplomacyMonths=null;_diplomacySelectionPurpose=DiplomacySelectionPurpose.None;menu.Abandon();menu.QueueFree();RefreshDiplomacyOrders();throw;}
    }

    private void ChangeDiplomacyDraft(Action action, DiplomacySelectionPurpose purpose=DiplomacySelectionPurpose.None, int? acceptedId=null)
    {
        if(acceptedId is int id&&
            (purpose==DiplomacySelectionPurpose.Force&&!(DiplomacyForceAdmission?.Invoke(id)??true)||
             purpose==DiplomacySelectionPurpose.Executor&&!(DiplomacyExecutorAdmission?.Invoke(id)??true)))
        {RefreshDiplomacyOrders();return;}
        try
        {
            action();
            if(_diplomacyDraft?.AppRandomState is {} random)_presentationRandom.InstallCommittedState(random.State);
            if(purpose!=DiplomacySelectionPurpose.None)
            {
                _lastDiplomacyAcceptedPurpose=purpose;_lastDiplomacyAcceptedId=acceptedId;
                _lastDiplomacyAcceptedMissionKind=_diplomacyDraft?.Kind;
                _diplomacySelectionAcceptedSequence=checked(_diplomacySelectionAcceptedSequence+1);
            }
        }
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
        if(_diplomacyTradeCalculator is{} calculator){calculator.RequestCancel();return;}
        if(_diplomacyTargets is {} targets){targets.RequestCancel();return;}
        if(_diplomacyPersons is {} persons){persons.RequestCancel();return;}
        if(_diplomacyExecutors is {} executors){executors.RequestCancel();return;}
        if(_diplomacyAmounts is {} amounts){amounts.RequestCancel();return;}
        if(_diplomacyMonths is {} months){months.RequestCancel();return;}
        CloseDiplomacyOrders();
    }
    private void CloseDiplomacyOrders()
    {
        if(_diplomacyOrdersView is null&&_diplomacyDraft is null&&_diplomacyMap is null)return;
        _diplomacySelectionPurpose=DiplomacySelectionPurpose.None;
        DiplomacyForceAdmission=DiplomacyExecutorAdmission=null;DiplomacySelectionReturnAllowed=true;
        var map=_diplomacyMap;_diplomacyMap=null;_diplomacyWorld=null;_diplomacySession=null;_diplomacyDraft=null;_diplomacyCardContent=null;
        var targets=_diplomacyTargets;_diplomacyTargets=null;var persons=_diplomacyPersons;_diplomacyPersons=null;
        if(targets is not null&&map is not null&&GodotObject.IsInstanceValid(map))map.EndDiplomacyTargetPick(targets);
        var executors=_diplomacyExecutors;_diplomacyExecutors=null;
        var months=_diplomacyMonths;_diplomacyMonths=null;
        var amounts=_diplomacyAmounts;_diplomacyAmounts=null;
        var view=_diplomacyOrdersView;_diplomacyOrdersView=null;
        var calculator=_diplomacyTradeCalculator;_diplomacyTradeCalculator=null;calculator?.Abandon();calculator?.QueueFree();
        targets?.Abandon();targets?.QueueFree();persons?.Abandon();persons?.QueueFree();
        executors?.Abandon();executors?.QueueFree();
        amounts?.Abandon();amounts?.QueueFree();
        months?.Abandon();months?.QueueFree();view?.Abandon();view?.QueueFree();
        if(map is not null&&GodotObject.IsInstanceValid(map)&&_residentMap==map)map.SetDetailsOpen(false);
        if(!_failed&&!_shuttingDown&&_residentMap is not null)SetMapFocus(_mapFocused);
    }
}
