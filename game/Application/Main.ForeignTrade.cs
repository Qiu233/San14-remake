using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;
using San14.Client.Gameplay;

public partial class Main
{
    private ForeignTradeOrdersView? _foreignTradeView;
    private ForeignTradeSelectionView? _foreignTradeSelector;
    private DeploymentCalculatorDialog? _foreignTradeCalculator;
    private ForeignTradeDraft? _foreignTradeDraft;
    private GameplaySession? _foreignTradeSession;
    private GameState? _foreignTradeWorld;
    private MapSceneView? _foreignTradeMap;
    private ForeignTradeUiPage? _foreignTradeSelectorPage;
    private AreaAdministrationOfficerContent? _foreignTradeCardContent;
    private bool HasForeignTradePage=>_foreignTradeView is not null||HasForeignTradeReport;
    private bool IsForeignTradeCurrent=>!_failed&&!_shuttingDown&&_foreignTradeView is not null&&_foreignTradeDraft is not null&&
        ReferenceEquals(_session,_foreignTradeSession)&&ReferenceEquals(_world,_foreignTradeWorld)&&
        _foreignTradeMap is not null&&_residentMap==_foreignTradeMap;
    private bool CanEditForeignTrade=>IsForeignTradeCurrent&&_foreignTradeSelector is null&&_foreignTradeCalculator is null;
    private ForeignTradeUiPage? ForeignTradeCurrentPage=>!IsForeignTradeCurrent?null:
        _foreignTradeSelector is not null?_foreignTradeSelectorPage:
        _foreignTradeCalculator is not null?ForeignTradeUiPage.Other:ForeignTradeUiPage.Top;
    private bool ForeignTradeCountrySelectorVisible=>ForeignTradeCurrentPage==ForeignTradeUiPage.Country&&_foreignTradeSelector?.IsVisibleInTree()==true;
    private bool ForeignTradePersonSelectorVisible=>ForeignTradeCurrentPage==ForeignTradeUiPage.Person&&_foreignTradeSelector?.IsVisibleInTree()==true;
    private int? ForeignTradeSelectedCountryId=>ForeignTradeCountrySelectorVisible&&_foreignTradeSelector?.SelectedIds is { Count:1 } selected
        ?selected[0]:_foreignTradeDraft?.CountryId;
    private IReadOnlyList<int> ForeignTradeSelectedPersonIds=>ForeignTradePersonSelectorVisible
        ?_foreignTradeSelector!.SelectedIds:_foreignTradeDraft?.ExecutorIds??[];
    private Func<IReadOnlyList<int>,bool>? ForeignTradeCountryAdmission {get;set;}
    private Func<IReadOnlyList<int>,bool>? ForeignTradePersonAdmission {get;set;}
    private bool ForeignTradeCountryReturnAllowed {get;set;}=true;
    private bool ForeignTradePersonReturnAllowed {get;set;}=true;
    private long ForeignTradeCountryAcceptedSequence {get;set;}
    private long ForeignTradePersonAcceptedSequence {get;set;}
    private long ForeignTradeAcceptedSequence {get;set;}
    private int? ForeignTradeLastAcceptedCountryId {get;set;}
    private IReadOnlyList<int> ForeignTradeLastAcceptedPersonIds {get;set;}=[];

    private void AdoptForeignTradeSpeechWorld(GameplaySession session, GameState previous, GameState replacement)
    {
        if (_foreignTradeView is not null && _foreignTradeDraft is { } draft &&
            _foreignTradeSession == session && _foreignTradeMap is not null && _foreignTradeMap == _residentMap &&
            ReferenceEquals(_foreignTradeWorld, previous) && draft.TryAdoptPresentationWorld(previous, replacement))
            _foreignTradeWorld = replacement;
        if (_foreignTradeReport is not null && _foreignTradeReportSession == session &&
            _foreignTradeReportMap is not null && _foreignTradeReportMap == _residentMap &&
            ReferenceEquals(_foreignTradeReportWorld, previous))
            _foreignTradeReportWorld = replacement;
    }

    private void OpenForeignTradeOrders()
    {
        if(_failed||_shuttingDown||_advancingPeriod||IsReturningToTitle||HasAdministrationPage||HasMilitaryOrderPage||
            HasCampaignSavePage||HasMapSystemMenu||HasCommandWorkspace||HasActionReport||HasPersonnelConfirmation||
            _detailsView is not null||_repository is null||_campaignComposition is not { } composition||
            _session is not { PlayerForceId:int player } session||session.HasPendingOperation||
            session.World is not { Phase:TurnPhase.Strategy,NativeArmySimulation:not null } world||
            _residentMap is not { IsPlayableMapReady:true } map)return;
        var rules=composition.PrepareArmyQueries(world);
        var draft=rules.CreateForeignTradeDraft(world,player,_tutorialLesson?.Lesson.World??world);
        _foreignTradeDraft=draft;_foreignTradeSession=session;_foreignTradeWorld=world;_foreignTradeMap=map;
        _foreignTradeCardContent=new();
        var view=new ForeignTradeOrdersView();_foreignTradeView=view;AddChild(view);
        try
        {
            SetMapFocus(true);map.CloseCityCommandMenu();map.SetDetailsOpen(true,keepMapVisible:true);HideDeploymentHud();
            BindCurrentTutorialForeignTrade();
            view.Configure(_repository,_ui,()=>CanEditForeignTrade,
                ()=>RunUiAction(RequestForeignTradeOrdersBack),()=>RunPlayAction(ConfirmForeignTradeOrders),
                ()=>RunUiAction(OpenForeignTradeCountrySelection),()=>RunUiAction(OpenForeignTradePersonSelection),
                gold=>RunUiAction(()=>OpenForeignTradeAmount(gold)),troops=>RunUiAction(()=>
                {if(CanEditForeignTrade){draft.SetTroops(troops);RefreshForeignTradeOrders();}}),
                ()=>RunUiAction(OpenForeignTradeItemSelection),()=>RunUiAction(()=>
                {if(CanEditForeignTrade){draft.ResetItem();RefreshForeignTradeOrders();}}), resources: TryBorrowApplicationUiResources(view));
            RefreshForeignTradeOrders();ObserveTutorialInformation(50);
        }
        catch{CloseForeignTradeOrders();throw;}
    }

    private void RefreshForeignTradeOrders()
    {
        if(!IsForeignTradeCurrent)return;
        var draft=_foreignTradeDraft!;var world=_foreignTradeWorld!;
        string unknown=_ui.Get("detail_unknown");
        string country=draft.CountryId is int selected?draft.Countries.FirstOrDefault(row=>row.Id==selected)?.Name??unknown:"";
        string names=string.Join("、",draft.ExecutorIds.Select(id=>world.Officers.GetValueOrDefault(id)?.Name??unknown));
        int cardId=draft.LeadExecutorOfficerId??0;
        var cardQuery=draft.QueryOfficerCard(cardId);
        AreaAdministrationOfficerValues card=cardQuery.Available
            ?_foreignTradeCardContent!.Create(cardQuery.Value!,draft.QueryOfficerTraitTooltip)
            :new(cardId==0?"":world.Officers[cardId].Name,null,null,null,null);
        var summary=draft.QuerySummary();
        bool offers=draft.CountryId.HasValue&&draft.ExecutorIds.Count>0;
        _foreignTradeView!.Apply(new(draft.Title,country,card,names,draft.Gold,draft.Supplies,draft.Troops,
            draft.ItemId is int item?draft.Items.FirstOrDefault(row=>row.ItemId==item)?.Name??unknown:"",
            draft.GoldMinimum,draft.SuppliesMinimum,draft.GoldMaximum,draft.SuppliesMaximum,draft.TroopsAvailable,
            draft.TroopOptions,draft.BooksAvailable,draft.BooksRequired,draft.TravelDays,draft.CanConfirm,
            draft.Countries.Any(row=>row.Enabled==true),draft.CountryId.HasValue&&draft.Executors.Any(row=>row.Enabled==true),
            offers,draft.Items.Any(row=>row.Enabled==true),draft.UnresolvedOwner,
            summary.Friendship,summary.Rivals));
        _foreignTradeView.SetBusy(!CanEditForeignTrade);
    }

    private void OpenForeignTradeCountrySelection()
    {
        if(!CanEditForeignTrade)return;var draft=_foreignTradeDraft!;
        OpenForeignTradeSelector(ForeignTradeUiPage.Country,ForeignTradeSelectorKind.Country,chosen=>
        {
            if(ForeignTradeCountryAdmission?.Invoke(chosen)==false)return false;
            if(chosen.Count!=1||!draft.SelectCountry(chosen[0]))return false;
            ForeignTradeCountryAcceptedSequence++;return true;
        });
    }
    private void OpenForeignTradePersonSelection()
    {
        if(!CanEditForeignTrade||_foreignTradeDraft?.CountryId is null)return;var draft=_foreignTradeDraft;
        OpenForeignTradeSelector(ForeignTradeUiPage.Person,ForeignTradeSelectorKind.Executors,chosen=>
        {
            if(ForeignTradePersonAdmission?.Invoke(chosen)==false||!draft.SelectExecutors(chosen))return false;
            ForeignTradePersonAcceptedSequence++;return true;
        });
    }
    private void OpenForeignTradeItemSelection()
    {
        if(!CanEditForeignTrade)return;var draft=_foreignTradeDraft!;
        OpenForeignTradeSelector(ForeignTradeUiPage.Other,ForeignTradeSelectorKind.Item,
            chosen=>chosen.Count==1&&draft.SelectItem(chosen[0]));
    }
    private void OpenForeignTradeSelector(ForeignTradeUiPage kind,ForeignTradeSelectorKind selectionKind,
        Func<IReadOnlyList<int>,bool> accept)
    {
        if(!CanEditForeignTrade||_repository is null)return;
        var draft=_foreignTradeDraft!;var query=draft.PrepareSelector(selectionKind);
        if(!query.Available||query.Value is null){_foreignTradeView!.ApplyNotice(_ui.Get("detail_unknown"));return;}
        var selector=new ForeignTradeSelectionView();_foreignTradeSelector=selector;_foreignTradeSelectorPage=kind;AddChild(selector);
        _foreignTradeView!.SetBusy(true);_foreignTradeView.Hide();
        try
        {
            selector.Configure(_repository,_ui,query.Value,
                ()=>IsForeignTradeCurrent&&_foreignTradeSelector==selector,chosen=>RunUiAction(()=>
                {
                    if(!IsForeignTradeCurrent||_foreignTradeSelector!=selector||_foreignTradeDraft!=draft)return;
                    if(chosen is not null&&!accept(chosen)){selector.ApplyNotice(_ui.Get("detail_unknown"));return;}
                    _foreignTradeSelector=null;_foreignTradeSelectorPage=null;selector.Abandon();selector.QueueFree();
                    _foreignTradeView!.Show();RefreshForeignTradeOrders();
                }),CurrentListScrollSpeed);
            BindCurrentTutorialForeignTrade();RefreshForeignTradeAdmission();
        }
        catch
        {
            _foreignTradeSelector=null;_foreignTradeSelectorPage=null;selector.Abandon();selector.QueueFree();
            _foreignTradeView.Show();RefreshForeignTradeOrders();throw;
        }
    }

    private void OpenForeignTradeAmount(bool gold)
    {
        if(!CanEditForeignTrade||_repository is null)return;
        var draft=_foreignTradeDraft!;int minimum=gold?draft.GoldMinimum:draft.SuppliesMinimum;
        int? maximum=gold?draft.GoldMaximum:draft.SuppliesMaximum;
        if(maximum is not int max||max<minimum)return;
        var calculator=new DeploymentCalculatorDialog();_foreignTradeCalculator=calculator;AddChild(calculator);
        _foreignTradeView!.SetBusy(true);
        try
        {
            calculator.Configure(_repository,_ui,gold?draft.Gold:draft.Supplies,minimum,max,chosen=>RunUiAction(()=>
            {
                if(!IsForeignTradeCurrent||_foreignTradeCalculator!=calculator)return;
                _foreignTradeCalculator=null;calculator.Abandon();calculator.QueueFree();
                if(chosen is int amount){if(gold)draft.SetGold(amount);else draft.SetSupplies(amount);}
                RefreshForeignTradeOrders();
            }),panelPosition:new(860,352),titleMessage:gold?0x020C:0x020D);
        }
        catch{_foreignTradeCalculator=null;calculator.Abandon();calculator.QueueFree();RefreshForeignTradeOrders();throw;}
    }

    private string ForeignTradeMessage(int id)=>_repository!.LoadMessages(_ui.SourceLocale,MessageKind.Data)
        .Messages.Sections[1].Records[id].Text??throw new InvalidDataException($"Missing foreign trade message {id:X4}.");

    private void RefreshForeignTradeAdmission()
    {
        if(_foreignTradeSelector is not { } selector)return;
        selector.ApplySelectionAdmission(_foreignTradeSelectorPage switch
        {
            ForeignTradeUiPage.Country when ForeignTradeCountryAdmission is { } country=>id=>country([id]),
            ForeignTradeUiPage.Person when ForeignTradePersonAdmission is { } people=>id=>people([id]),
            _=>null
        },_foreignTradeSelectorPage switch
        {
            ForeignTradeUiPage.Country=>ForeignTradeCountryReturnAllowed,
            ForeignTradeUiPage.Person=>ForeignTradePersonReturnAllowed,
            _=>true
        });
    }

    private void ConfirmForeignTradeOrders()
    {
        if(!CanEditForeignTrade||_foreignTradeDraft?.BuildOrder() is not { } order)return;
        var draft=_foreignTradeDraft;var view=_foreignTradeView!;view.SetBusy(true);
        if(!ApplyPlayableOrder(order,()=>
        {
            ForeignTradeLastAcceptedCountryId=draft.CountryId;
            ForeignTradeLastAcceptedPersonIds=Array.AsReadOnly(draft.ExecutorIds.ToArray());
            ForeignTradeAcceptedSequence++;CloseForeignTradeOrders();
        }))
        {RefreshForeignTradeOrders();view.ApplyNotice(_playMessage.Text);}
    }

    private void RequestForeignTradeOrdersBack()
    {
        if(HasForeignTradeReport)return;
        if(!IsForeignTradeCurrent)return;
        if(_foreignTradeSelector is { } selector){selector.RequestCancel();return;}
        if(_foreignTradeCalculator is { } calculator){calculator.RequestCancel();return;}
        CloseForeignTradeOrders();
    }
    private void CloseForeignTradeOrders()
    {
        CloseForeignTradeReport();
        var map=_foreignTradeMap;_foreignTradeMap=null;_foreignTradeWorld=null;_foreignTradeSession=null;_foreignTradeDraft=null;
        var selector=_foreignTradeSelector;_foreignTradeSelector=null;_foreignTradeSelectorPage=null;
        var calculator=_foreignTradeCalculator;_foreignTradeCalculator=null;
        var view=_foreignTradeView;_foreignTradeView=null;_foreignTradeCardContent=null;
        ForeignTradeCountryAdmission=ForeignTradePersonAdmission=null;
        ForeignTradeCountryReturnAllowed=ForeignTradePersonReturnAllowed=true;
        selector?.Abandon();selector?.QueueFree();calculator?.Abandon();calculator?.QueueFree();view?.Abandon();view?.QueueFree();
        if(map is not null&&GodotObject.IsInstanceValid(map)&&_residentMap==map)map.SetDetailsOpen(false);
        if(!_failed&&!_shuttingDown&&_residentMap is not null)SetMapFocus(_mapFocused);
    }
}
