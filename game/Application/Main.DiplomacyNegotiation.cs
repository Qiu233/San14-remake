using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private DiplomacyNegotiationView? _diplomacyNegotiation;
    private string? _diplomacyNegotiationNotice;
    private NormalPeriodOperation? _automaticDiplomacyAttempt;
    private Control? _diplomacyNegotiationAuxiliary;
    private MapSceneView? _diplomacyNegotiationMap;
    private GameState? _diplomacyNegotiationMapRestore;
    private Func<bool>? _diplomacyNegotiationCurrent;

    private void RefreshDiplomaticNegotiation()
    {
        if (TryResumeCreatedEventResults()) return;
        if (RefreshNormalTurnContinuation()) return;
        if (_diplomacyNegotiation is not null || _advancingPeriod || _failed || _shuttingDown ||
            _session is not { PlayerForceId: int player, PendingNormalPeriod: { } pending } session ||
            _campaignComposition is not { } composition || _repository is null) return;
        if (pending.RequiresAutomaticDiplomacyContinuation)
        {
            // A failed background attempt may refresh this same owner. Leave
            // an explicit Advance available instead of immediately retrying.
            if (ReferenceEquals(_automaticDiplomacyAttempt, pending)) return;
            _automaticDiplomacyAttempt = pending;
            _advancingPeriod = true;
            _periodAdvanceTask = AdvancePlayableAsync(session, composition);
            return;
        }
        var queries=composition.PrepareArmyQueries(pending.Candidate);
        var request = queries.QueryDiplomacyNegotiations(pending.Candidate, player).FirstOrDefault();
        if (request is null) return;
        bool editable=request.Kind==DiplomaticMissionKind.Exchange||
            request.Kind!=DiplomaticMissionKind.DemandSurrender&&
            request.Negotiation?.Stage is not DiplomaticNegotiationStage.RecipientDecision&&
            (request.Negotiation is not null||player==request.ForceId);
        var draft=editable?queries.OpenDiplomacyNegotiationDraft(pending.Candidate,request,player,new(_presentationRandom.State)):null;
        var page=draft?.Page??queries.QueryDiplomacyNegotiationPage(pending.Candidate,request,player);
        var appearances=composition.DetailForces(pending.Candidate,[request.ForceId,request.TargetForceId]);
        var people=request.Kind is DiplomaticMissionKind.ReturnCaptives or DiplomaticMissionKind.DemandSurrender or DiplomaticMissionKind.Exchange
            ?PrepareDiplomacyArrivalVisuals(queries,pending.Candidate,request,page.DisplayedForceId) with
                {DisplayedForceAppearance=appearances.GetValueOrDefault(page.DisplayedForceId)}:null;
        if(draft?.AppRandomState is {} openedRandom)_presentationRandom.InstallCommittedState(openedRandom.State);
        var view = new DiplomacyNegotiationView(); _diplomacyNegotiation = view; AddChild(view);
        _diplomacyNegotiationCurrent=Current;
        try
        {
        void Submit(bool accepted)=>RunUiAction(() =>
        {
            if (!Current() || _advancingPeriod) return;
            if(accepted&&request.Kind==DiplomaticMissionKind.DemandSurrender&&player==request.TargetForceId)
            {ConfirmSurrender();return;}
            Commit(accepted);
        });
        void Commit(bool accepted)
        {
            if(!Current()||_advancingPeriod)return;
            var choice = draft?.BuildOrder(accepted)??new ConfirmDiplomaticNegotiationOrder(player, request.TaskId,
                request.Negotiation?.Proposal??request.InitialOffer,accepted);
            CloseDiplomaticNegotiation();
            _advancingPeriod = true;
            _periodAdvanceTask = AdvancePlayableAsync(session, composition, campaignChoice: choice);
        }
        void ConfirmSurrender()
        {
            if(_diplomacyNegotiationAuxiliary is not null)return;
            var question=new DeploymentTargetConfirmation();_diplomacyNegotiationAuxiliary=question;
            view.SuspendBody();view.AddChild(question);
            try
            {
                question.ConfigureGameMessage(_repository,_ui,0x2F02,confirmed=>RunUiAction(()=>
                {
                    if(!Current()||_diplomacyNegotiationAuxiliary!=question)return;
                    CloseDiplomaticNegotiationAuxiliary();
                    if(confirmed)Commit(true);
                }));
                question.Name="DiplomacySurrenderConfirmation";
            }
            catch{CloseDiplomaticNegotiationAuxiliary();throw;}
        }
        if(draft is not null)
            view.ConfigureTerms(_repository,pending.Candidate,draft,player,
                appearances,
                _ui,Current,Submit,()=>RunUiAction(ShowForce),()=>RunUiAction(ShowMap),
                kind=>RunUiAction(()=>ChooseCondition(kind)),()=>RunUiAction(()=>Edit(draft.ClearCondition)),
                ()=>RunUiAction(CalculateIntelligence),amount=>RunUiAction(()=>Edit(()=>draft.SetMilitaryIntelligence(amount))),people);
        else view.Configure(_repository,pending.Candidate,request,player,page,_ui,Current,Submit,
            ()=>RunUiAction(ShowForce),()=>RunUiAction(ShowMap),people);
        view.RegisterHeader(_sourceHeaders);
        if (_diplomacyNegotiationNotice is { } message)
        { view.SetNotice(message); _diplomacyNegotiationNotice = null; }
        _residentMap?.CloseCityCommandMenu();
        _residentMap?.SetDetailsOpen(true, keepMapVisible: true);
        SetMapFocus(false);
        }
        catch{CloseDiplomaticNegotiation();throw;}
        bool Current() => !_failed && !_shuttingDown && _diplomacyNegotiation == view &&
            ReferenceEquals(_session, session) && ReferenceEquals(session.PendingNormalPeriod, pending);

        void Edit(Action action)
        {
            if(!Current()||draft is null)return;
            action();
            if(draft.AppRandomState is {} random)_presentationRandom.InstallCommittedState(random.State);
            view.ApplyTerms();
        }
        void CalculateIntelligence()
        {
            if(draft is null||draft.CanChooseMilitaryIntelligence!=true||
                draft.MilitaryIntelligenceMinimum is not int minimum||
                draft.MilitaryIntelligenceCalculatorMaximum is not int maximum||maximum<minimum)return;
            Calculate(draft.MilitaryIntelligenceValue??minimum,minimum,maximum,0x0835,
                amount=>Edit(()=>draft.SetMilitaryIntelligence(amount)),new(930,460));
        }
        void Calculate(int current,int minimum,int maximum,int title,Action<int> apply,Vector2? position=null)
        {
            if(!Current()||_diplomacyNegotiationAuxiliary is not null)return;
            var calculator=new DeploymentCalculatorDialog();_diplomacyNegotiationAuxiliary=calculator;
            view.SuspendBody();view.AddChild(calculator);
            try
            {
                calculator.Configure(_repository,_ui,current,minimum,maximum,chosen=>RunUiAction(()=>
                {
                    if(!Current()||_diplomacyNegotiationAuxiliary!=calculator)return;
                    CloseDiplomaticNegotiationAuxiliary();if(chosen is int value)apply(value);
                }),panelPosition:position,titleMessage:title);
            }
            catch{CloseDiplomaticNegotiationAuxiliary();throw;}
        }
        void ChooseCondition(DiplomacyNegotiationConditionKind kind)
        {
            if(!Current()||draft is null||_diplomacyNegotiationAuxiliary is not null)return;
            bool gold=kind==DiplomacyNegotiationConditionKind.Gold;
            if(kind is DiplomacyNegotiationConditionKind.Gold or DiplomacyNegotiationConditionKind.Supplies)
            {
                var query=gold?draft.GoldSelection:draft.SuppliesSelection;
                if(!query.Available||query.Value is not {Allowed:true} range)return;
                if(draft.Kind==DiplomaticMissionKind.Exchange)
                {
                    Calculate(gold?draft.Offer.Gold:draft.Offer.Supplies,range.Minimum,range.Maximum,
                        gold?0x020C:0x020D,value=>Edit(()=>{if(gold)draft.SetGold(value);else draft.SetSupplies(value);}),new(860,352));
                    return;
                }
                var amountMenu=new MerchantAmountMenuView();_diplomacyNegotiationAuxiliary=amountMenu;
                view.SuspendBody();view.AddChild(amountMenu);
                try
                {
                    amountMenu.Configure(_repository,_ui,range.Presets,gold?draft.Offer.Gold:draft.Offer.Supplies,
                        range.Minimum,range.Maximum,()=>Current()&&_diplomacyNegotiationAuxiliary==amountMenu,
                        chosen=>RunUiAction(()=>
                        {
                            if(!Current()||_diplomacyNegotiationAuxiliary!=amountMenu)return;
                            CloseDiplomaticNegotiationAuxiliary();
                            if(chosen is int amount)Edit(()=>{if(gold)draft.SetGold(amount);else draft.SetSupplies(amount);});
                        }),titleMessage:gold?0x020C:0x020D,calculatorPosition:new(860,352));
                }
                catch{CloseDiplomaticNegotiationAuxiliary();throw;}
                return;
            }
            bool item=kind==DiplomacyNegotiationConditionKind.Item;
            var choices=item?draft.ItemChoices:draft.AreaChoices;
            if(!choices.Available||choices.Value is not {Count:>0} rows)return;
            var selector=new DiplomacyTargetSelectionView();_diplomacyNegotiationAuxiliary=selector;
            view.SuspendBody();view.AddChild(selector);
            try
            {
                selector.Configure(_repository,_ui,draft.ReadTargetSelection(item?DiplomacyTargetSelectionKind.Item:DiplomacyTargetSelectionKind.Area),
                    item?draft.Offer.ItemId:draft.Offer.AreaId,item?0x058C:0x05AF,
                    ()=>Current()&&_diplomacyNegotiationAuxiliary==selector,chosen=>RunUiAction(()=>
                    {
                        if(!Current()||_diplomacyNegotiationAuxiliary!=selector)return;
                        CloseDiplomaticNegotiationAuxiliary();
                        if(chosen is int id)Edit(()=>{if(item)draft.SelectItem(id);else draft.SelectArea(id);});
                    }),CurrentListScrollSpeed);
            }
            catch{CloseDiplomaticNegotiationAuxiliary();throw;}
        }

        void ShowForce()
        {
            if(!Current()||_diplomacyNegotiationAuxiliary is not null||_selectedSnapshot is null)return;
            var detail=new ForceDetailsView();_diplomacyNegotiationAuxiliary=detail;
            view.SuspendBody();view.AddChild(detail);
            try
            {
                var world=pending.Candidate;int force=page.DisplayedForceId;
                ConfigureCurrentForceDetail(detail,world,force,
                    ()=>RunUiAction(()=>CloseDiplomaticNegotiationAuxiliary()),
                    preparedRules:composition.PrepareArmyQueries(world));
                detail.RegisterHeader(_sourceHeaders);
            }
            catch{CloseDiplomaticNegotiationAuxiliary();throw;}
        }
        void ShowMap()
        {
            if(!Current()||_diplomacyNegotiationAuxiliary is not null||_residentMap is not { IsPlayableMapReady:true } map)return;
            var navigation=new ReportMapReturnView();_diplomacyNegotiationAuxiliary=navigation;
            _diplomacyNegotiationMap=map;_diplomacyNegotiationMapRestore=session.World;
            view.SuspendBody();view.AddChild(navigation);
            try
            {
                map.RefreshPlayableWorld(pending.Candidate);map.SetDetailsOpen(false);SetMapFocus(true);
                navigation.Configure(_repository,_ui.SourceLocale,_sourceHeaders,
                    ()=>RunUiAction(()=>CloseDiplomaticNegotiationAuxiliary()));
            }
            catch{CloseDiplomaticNegotiationAuxiliary();throw;}
        }
    }

    private void CloseDiplomaticNegotiationAuxiliary(bool resume=true)
    {
        var auxiliary=_diplomacyNegotiationAuxiliary;_diplomacyNegotiationAuxiliary=null;
        var map=_diplomacyNegotiationMap;_diplomacyNegotiationMap=null;
        var restore=_diplomacyNegotiationMapRestore;_diplomacyNegotiationMapRestore=null;
        if(auxiliary is not null&&GodotObject.IsInstanceValid(auxiliary))
        {
            if(auxiliary is ReportMapReturnView navigation)navigation.Retire();
            if(auxiliary is DeploymentCalculatorDialog calculator)calculator.Abandon();
            if(auxiliary is MerchantAmountMenuView amountMenu)amountMenu.Abandon();
            if(auxiliary is DiplomacyTargetSelectionView selector)selector.Abandon();
            if(auxiliary is DeploymentTargetConfirmation question)question.Abandon();
            auxiliary.Hide();auxiliary.GetParent()?.RemoveChild(auxiliary);auxiliary.QueueFree();
        }
        bool current=_diplomacyNegotiationCurrent?.Invoke()==true;
        if(map is not null&&GodotObject.IsInstanceValid(map)&&_residentMap==map&&ReferenceEquals(_world,restore))
            map.RefreshPlayableWorld(restore!);
        if(resume&&current)
        {
            _residentMap?.SetDetailsOpen(true,keepMapVisible:true);SetMapFocus(false);
            _diplomacyNegotiation!.ResumeBody();
        }
    }

    private void HandleDiplomaticNegotiationInput(InputEvent input)
    {
        // These children own their calculator/list cancel behavior.
        if(_diplomacyNegotiationAuxiliary is DeploymentCalculatorDialog or MerchantAmountMenuView or DiplomacyTargetSelectionView)return;
        if(_diplomacyNegotiationAuxiliary is DeploymentTargetConfirmation question)
        {
            if(input is InputEventKey {Keycode:Key.Escape,Pressed:true,Echo:false}||
                input is InputEventMouseButton {ButtonIndex:MouseButton.Right,Pressed:false})
            {GetViewport().SetInputAsHandled();RunUiAction(question.RequestCancel);}
            return;
        }
        if(input is InputEventKey { Keycode:Key.Escape } key)
        {
            GetViewport().SetInputAsHandled();
            if(key.Pressed&&!key.Echo&&_diplomacyNegotiationAuxiliary is not null)
                RunUiAction(()=>CloseDiplomaticNegotiationAuxiliary());
        }
        else if(input is InputEventMouseButton { ButtonIndex:MouseButton.Right } mouse&&
            _diplomacyNegotiationAuxiliary is not null)
        {
            bool close=_diplomacyNegotiationMap is {} map?map.SourceRightCancelReleased(mouse):!mouse.Pressed;
            if(close){GetViewport().SetInputAsHandled();RunUiAction(()=>CloseDiplomaticNegotiationAuxiliary());}
        }
    }

    private void CloseDiplomaticNegotiation()
    {
        _automaticDiplomacyAttempt = null;
        CloseDiplomaticNegotiationAuxiliary(resume:false);_diplomacyNegotiationCurrent=null;
        var view = _diplomacyNegotiation; _diplomacyNegotiation = null;
        if (view is null) return;
        view.Abandon(); view.QueueFree();
    }
}
