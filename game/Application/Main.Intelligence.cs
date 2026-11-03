using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;
using San14.Client.Gameplay;

public partial class Main
{
    private CommandSelectionView? _intelligenceOverview;
    private IntelligenceSelectionView? _intelligenceSelector;
    private IntelligenceOrdersView? _intelligenceEditor;
    private DeploymentTargetConfirmation? _intelligenceQuestion;
    private IntelligenceHomeDraft? _intelligenceHome;
    private IntelligenceHomeEditorDraft? _intelligenceHomeEditor;
    private IntelligenceAwayDraft? _intelligenceAway;
    private IntelligenceInspectDraft? _intelligenceInspect;
    private GameplaySession? _intelligenceSession;
    private GameState? _intelligenceWorld;
    private MapSceneView? _intelligenceMap;
    private AreaAdministrationOfficerContent? _intelligenceCards;
    private IntelligenceUiMode _intelligenceMode;
    private IntelligenceUiPage _intelligenceSelectorPage;
    private bool _intelligenceReturnedOverview;
    private int? _intelligenceHomeTarget;
    private IReadOnlyList<IntelligenceAssignment> _intelligenceOriginalAssignments=[];
    private bool HasIntelligencePage=>_intelligenceMap is not null;
    private bool IsIntelligenceCurrent=>!_failed&&!_shuttingDown&&HasIntelligencePage&&
        ReferenceEquals(_session,_intelligenceSession)&&ReferenceEquals(_world,_intelligenceWorld)&&
        ReferenceEquals(_residentMap,_intelligenceMap);
    private void AdoptIntelligenceSpeechWorld(GameplaySession session, GameState previous, GameState replacement)
    {
        // Home assignments, open editors, candidate order and preview caches
        // retain their immutable business baseline through a pure speech commit.
        if (!HasIntelligencePage || _intelligenceSession != session || _intelligenceMap != _residentMap ||
            !ReferenceEquals(_intelligenceWorld, previous)) return;
        _intelligenceWorld = replacement;
    }

    private bool CanEditIntelligence=>IsIntelligenceCurrent&&_intelligenceQuestion is null&&_intelligenceSelector is null;
    private IntelligenceUiPage? IntelligenceCurrentPage=>!IsIntelligenceCurrent||_intelligenceQuestion is not null?null:
        _intelligenceSelector is not null?_intelligenceSelectorPage:
        _intelligenceMode==IntelligenceUiMode.Away?IntelligenceUiPage.AwayEditor:
        _intelligenceMode==IntelligenceUiMode.Inspect?IntelligenceUiPage.Other:
        _intelligenceEditor is not null?IntelligenceUiPage.HomeEditor:
        _intelligenceReturnedOverview?IntelligenceUiPage.HomeOverviewReturned:IntelligenceUiPage.HomeOverview;
    private IntelligenceUiMode IntelligenceCurrentMode=>_intelligenceMode;
    private int? IntelligenceSelectedForceId=>_intelligenceSelectorPage==IntelligenceUiPage.AwayForce&&
        _intelligenceSelector?.SelectedIds is {Count:1} chosen?chosen[0]:
        _intelligenceMode==IntelligenceUiMode.Home?_intelligenceHomeTarget:_intelligenceAway?.SelectedTargetId;
    private IReadOnlyList<int> IntelligenceSelectedPersonIds=>_intelligenceSelector is not null&&
        _intelligenceSelectorPage is IntelligenceUiPage.HomePerson or IntelligenceUiPage.AwayPerson?
        _intelligenceSelector.SelectedIds:SelectedIntelligenceOfficer is int selected?[selected]:[];
    private int? SelectedIntelligenceOfficer=>_intelligenceMode switch
    { IntelligenceUiMode.Home=>_intelligenceHomeEditor?.SelectedOfficerId,
        IntelligenceUiMode.Away=>_intelligenceAway?.SelectedOfficerId,_=>_intelligenceInspect?.SelectedOfficerId };
    private Func<int,bool>? IntelligenceForceAdmission {get;set;}
    private Func<IReadOnlyList<int>,bool>? IntelligencePersonAdmission {get;set;}
    private bool IntelligenceSelectionReturnAllowed {get;set;}=true;
    private long IntelligenceForceAcceptedSequence {get;set;}
    private long IntelligencePersonAcceptedSequence {get;set;}
    private long IntelligenceEditorAcceptedSequence {get;set;}
    private long IntelligenceAcceptedSequence {get;set;}
    private IntelligenceUiMode IntelligenceLastAcceptedMode {get;set;}
    private IntelligenceUiMode IntelligenceLastEditorAcceptedMode {get;set;}
    private IntelligenceUiMode IntelligenceLastPersonAcceptedMode {get;set;}
    private int? IntelligenceLastAcceptedForceId {get;set;}
    private IReadOnlyList<int> IntelligenceLastAcceptedPersonIds {get;set;}=[];
    private int? IntelligenceLastEditorAcceptedForceId {get;set;}
    private IReadOnlyList<int> IntelligenceLastEditorAcceptedPersonIds {get;set;}=[];
    private int? IntelligenceLastForceId {get;set;}
    private IReadOnlyList<int> IntelligenceLastPersonIds {get;set;}=[];

    private void OpenIntelligenceOrders(MapMenuCommand command)
    {
        if(_failed||_shuttingDown||_advancingPeriod||IsReturningToTitle||HasAdministrationPage||HasMilitaryOrderPage||
            HasCampaignSavePage||HasMapSystemMenu||HasCommandWorkspace||HasActionReport||HasPersonnelConfirmation||
            _detailsView is not null||_repository is null||_campaignComposition is not { } composition||
            _session is not {PlayerForceId:int player} session||session.HasPendingOperation||
            session.World is not {Phase:TurnPhase.Strategy,NativeArmySimulation:not null} world||
            _residentMap is not {IsPlayableMapReady:true} map)return;
        _intelligenceMode=command switch {MapMenuCommand.Spy=>IntelligenceUiMode.Home,
            MapMenuCommand.Scout=>IntelligenceUiMode.Away,MapMenuCommand.Patrol=>IntelligenceUiMode.Inspect,
            _=>throw new ArgumentOutOfRangeException(nameof(command))};
        var rules=composition.PrepareArmyQueries(world);
        _intelligenceSession=session;_intelligenceWorld=world;_intelligenceMap=map;
        _intelligenceCards=new();_intelligenceReturnedOverview=false;
        try
        {
            SetMapFocus(true);map.CloseCityCommandMenu();map.SetDetailsOpen(true,keepMapVisible:true);HideDeploymentHud();
            if(_intelligenceMode==IntelligenceUiMode.Home)
            {_intelligenceHome=rules.CreateIntelligenceHomeDraft(world,player);
                _intelligenceOriginalAssignments=_intelligenceHome.Assignments.ToArray();OpenIntelligenceOverview();}
            else
            {
                if(_intelligenceMode==IntelligenceUiMode.Away)_intelligenceAway=rules.CreateIntelligenceAwayDraft(world,player);
                else _intelligenceInspect=rules.CreateIntelligenceInspectDraft(world,player);
                OpenIntelligenceEditor();
            }
            if(_intelligenceMode!=IntelligenceUiMode.Inspect)
                ObserveTutorialInformation(_intelligenceMode==IntelligenceUiMode.Home?29:30);
        }
        catch(Exception error)when(error is InvalidDataException or InvalidOperationException)
        {CloseIntelligenceOrders();_playMessage.Text=_ui.Get("detail_unknown");GD.PushWarning(error.Message);}
    }

    private void OpenIntelligenceOverview()
    {
        if(!IsIntelligenceCurrent||_repository is null||_intelligenceHome is not { } draft)return;
        var old=_intelligenceOverview;_intelligenceOverview=null;old?.Abandon();old?.QueueFree();
        var view=new CommandSelectionView();_intelligenceOverview=view;AddChild(view);
        var rows=draft.Forces.Select(row=>new CommandSelectionRow(row.Id,
            [new(row.Name),new(string.Join("、",draft.Assignments.Where(a=>a.TargetForceId==row.Id&&a.OfficerId!=0)
                .Select(a=>_intelligenceWorld!.Officers.GetValueOrDefault(a.OfficerId)?.Name??_ui.Get("detail_unknown"))))],
            row.Enabled==true,row.Missing)).ToArray();
        view.ConfigureIntelligenceOverview(_repository,_ui,
            [new("name",IntelligenceMessage(0x632),620),new("person",IntelligenceMessage(0x8A1),796)],rows,
            ()=>CanEditIntelligence&&_intelligenceEditor is null&&_intelligenceOverview==view,
            id=>RunUiAction(()=>OpenIntelligenceHomeEditor(id)),()=>RunPlayAction(ConfirmIntelligenceOrders),
            ()=>RunUiAction(RequestIntelligenceBack),()=>RunUiAction(()=>
            {if(CanEditIntelligence&&_intelligenceEditor is null&&IntelligenceForceAdmission is null&&IntelligencePersonAdmission is null)
                {draft.AutoAssign();OpenIntelligenceOverview();}}),CurrentListScrollSpeed);
        view.ApplyIntelligenceOverviewDecision(draft.CanConfirm);
        var monthlyCost=draft.MonthlyGoldCost;
        view.ApplyIntelligenceOverviewEconomy(draft.Books,draft.BookCost,draft.CurrentGold,
            monthlyCost.Available?monthlyCost.Value:null);
        BindCurrentTutorialIntelligence();
        RefreshIntelligenceAdmission();
    }

    private void OpenIntelligenceHomeEditor(int forceId)
    {
        if(!CanEditIntelligence||_intelligenceHome is null||_intelligenceEditor is not null||
            IntelligenceForceAdmission?.Invoke(forceId)==false)
        {if(IsIntelligenceCurrent&&_intelligenceEditor is null)_intelligenceOverview?.ResumeIntelligenceOverviewContent();return;}
        try
        {
            _intelligenceHomeEditor=_intelligenceHome.BeginEditor(forceId);_intelligenceHomeTarget=forceId;
            OpenIntelligenceEditor();
            IntelligenceLastForceId=forceId;IntelligenceForceAcceptedSequence++;
        }
        catch
        {
            var view=_intelligenceEditor;_intelligenceEditor=null;_intelligenceHomeEditor=null;_intelligenceHomeTarget=null;
            view?.Abandon();view?.QueueFree();
            if(IsIntelligenceCurrent)_intelligenceOverview?.ResumeIntelligenceOverviewContent();
            throw;
        }
    }

    private void OpenIntelligenceEditor()
    {
        var view=new IntelligenceOrdersView();_intelligenceEditor=view;AddChild(view);
        _intelligenceOverview?.SuspendIntelligenceOverviewContent();BindCurrentTutorialIntelligence();
        view.Configure(_repository!,_ui,_intelligenceMode,
            ()=>CanEditIntelligence&&_intelligenceEditor==view,
            ()=>RunUiAction(RequestIntelligenceBack),()=>RunPlayAction(ConfirmIntelligenceEditor),
            ()=>RunUiAction(OpenIntelligenceTargetSelection),()=>RunUiAction(OpenIntelligencePersonSelection),
            ()=>RunUiAction(()=>{if(CanEditIntelligence&&_intelligenceHomeEditor is {} editor){editor.SetPerson(null);RefreshIntelligenceEditor();}}),
            amount=>RunUiAction(()=>
            {if(!CanEditIntelligence)return;if(_intelligenceAway is {} away)away.SetDuration(amount);else _intelligenceInspect?.SetLevel(amount);RefreshIntelligenceEditor();}), resources: TryBorrowApplicationUiResources(view));
        RefreshIntelligenceEditor();
    }

    private void RefreshIntelligenceEditor()
    {
        if(!IsIntelligenceCurrent||_intelligenceEditor is null)return;
        var preview=_intelligenceHomeEditor?.Preview??_intelligenceAway?.Preview??_intelligenceInspect?.Preview;
        var card=_intelligenceHomeEditor?.OfficerCard??_intelligenceAway?.OfficerCard??_intelligenceInspect?.OfficerCard;
        string target=_intelligenceMode switch
        {
            IntelligenceUiMode.Home=>_intelligenceHome!.Forces.FirstOrDefault(r=>r.Id==_intelligenceHomeTarget)?.Name??"",
            IntelligenceUiMode.Away=>_intelligenceAway!.Targets.FirstOrDefault(r=>r.Id==_intelligenceAway.SelectedTargetId)?.Name??"",
            _=>_intelligenceInspect!.Targets.FirstOrDefault(r=>r.Id==_intelligenceInspect.SelectedTargetId)?.Name??""
        };
        bool canConfirm=_intelligenceHomeEditor?.CanConfirm??_intelligenceAway?.CanConfirm??_intelligenceInspect?.CanConfirm??false;
        bool people=_intelligenceMode switch {IntelligenceUiMode.Home=>_intelligenceHomeEditor?.People.Any(r=>r.Enabled==true)==true,
            IntelligenceUiMode.Away=>_intelligenceAway!.CanChoosePerson,_=>_intelligenceInspect!.CanChoosePerson};
        bool derivedKnown=preview?.Missing is null;
        _intelligenceEditor.Apply(new(_intelligenceMode,target,_intelligenceCards!.Create(card?.Available==true?card.Value:null),
            preview?.Gold,derivedKnown?preview?.GoldAfter:null,derivedKnown?preview?.GoldCost:null,
            preview?.MilitaryIntelligence,derivedKnown?preview?.MilitaryIntelligenceAfter:null,
            preview?.Books,preview?.BooksAfter,preview?.TravelDays,_intelligenceAway?.Duration??_intelligenceInspect?.Level??0,
            _intelligenceMode!=IntelligenceUiMode.Home,people,SelectedIntelligenceOfficer.HasValue,canConfirm,
            _intelligenceMode==IntelligenceUiMode.Home?new HashSet<int>():
                _intelligenceMode==IntelligenceUiMode.Away?new HashSet<int>(new[]{5,10,15}.Where(_intelligenceAway!.CanSelectOption)):
                    new HashSet<int>(new[]{10,20,30}.Where(_intelligenceInspect!.CanSelectOption)),preview?.Missing));
        _intelligenceEditor.SetSuspended(_intelligenceSelector is not null||_intelligenceQuestion is not null);
        RefreshIntelligenceAdmission();
    }

    private void OpenIntelligenceTargetSelection()
    {
        if(!CanEditIntelligence||_intelligenceEditor is null||_intelligenceMode==IntelligenceUiMode.Home)return;
        IntelligenceSelectionTable table;
        DiplomacyTargetMapQuery map;
        if(_intelligenceMode==IntelligenceUiMode.Away)
        {
            var query=_intelligenceAway!.ReadTargetSelection();
            if(!query.Available||query.Value is not {} source){IntelligenceSelectionUnavailable(query.UnresolvedInput);return;}
            table=IntelligenceSelectionContent.Create(source,_ui.Get("detail_unknown"));map=source.MapSelection;
        }
        else
        {
            var query=_intelligenceInspect!.ReadTargetSelection();
            if(!query.Available||query.Value is not {} source){IntelligenceSelectionUnavailable(query.UnresolvedInput);return;}
            table=IntelligenceSelectionContent.Create(source,_ui.Get("detail_unknown"));map=source.MapSelection;
        }
        OpenIntelligenceSelection(_intelligenceMode==IntelligenceUiMode.Away?IntelligenceUiPage.AwayForce:IntelligenceUiPage.Other,
            table,id=>
            {
                if(_intelligenceMode==IntelligenceUiMode.Away&&IntelligenceForceAdmission?.Invoke(id)==false)return false;
                bool accepted=_intelligenceAway?.SelectTarget(id)??_intelligenceInspect!.SelectTarget(id);
                if(accepted&&_intelligenceMode==IntelligenceUiMode.Away)
                {IntelligenceLastForceId=id;IntelligenceForceAcceptedSequence++;}return accepted;
            },map);
    }

    private void OpenIntelligencePersonSelection()
    {
        if(!CanEditIntelligence||_intelligenceEditor is null)return;
        if(_intelligenceMode==IntelligenceUiMode.Away&&!_intelligenceAway!.CanChoosePerson||
            _intelligenceMode==IntelligenceUiMode.Inspect&&!_intelligenceInspect!.CanChoosePerson)return;
        var query=_intelligenceMode switch
        {
            IntelligenceUiMode.Home=>_intelligenceHomeEditor!.ReadPersonSelection(),
            IntelligenceUiMode.Away=>_intelligenceAway!.ReadPersonSelection(),
            _=>_intelligenceInspect!.ReadPersonSelection()
        };
        if(!query.Available||query.Value is not {} source){IntelligenceSelectionUnavailable(query.UnresolvedInput);return;}
        OpenIntelligenceSelection(_intelligenceMode==IntelligenceUiMode.Home?IntelligenceUiPage.HomePerson:
            _intelligenceMode==IntelligenceUiMode.Away?IntelligenceUiPage.AwayPerson:IntelligenceUiPage.Other,
            IntelligenceSelectionContent.Create(source,_ui.Get("detail_unknown")),id=>
            {
                if(IntelligencePersonAdmission?.Invoke([id])==false)return false;
                bool accepted=_intelligenceMode switch {IntelligenceUiMode.Home=>_intelligenceHomeEditor!.SetPerson(id),
                    IntelligenceUiMode.Away=>_intelligenceAway!.SelectPerson(id),_=>_intelligenceInspect!.SelectPerson(id)};
                if(accepted){IntelligenceLastPersonAcceptedMode=_intelligenceMode;
                    IntelligenceLastPersonIds=[id];IntelligencePersonAcceptedSequence++;}return accepted;
            });
    }

    private void OpenIntelligenceSelection(IntelligenceUiPage page,IntelligenceSelectionTable table,
        Func<int,bool> accept,DiplomacyTargetMapQuery? mapQuery=null)
    {
        if(table.Count==0||_intelligenceMap is not {} map||_intelligenceEditor is not {} editor)return;
        var view=new IntelligenceSelectionView();_intelligenceSelector=view;_intelligenceSelectorPage=page;AddChild(view);
        editor.SetSuspended(true);editor.Hide();
        bool Current()=>IsIntelligenceCurrent&&_intelligenceSelector==view&&_intelligenceEditor==editor;
        void BindMode(bool list)
        {
            if(!Current())return;
            map.EndDiplomacyTargetPick(view);map.SetDetailsOpen(list,keepMapVisible:true);
            if(!list)map.BeginDiplomacyTargetPick(view,_intelligenceWorld!,mapQuery!,
                ()=>Current()&&view.IsMapMode,view.CanChooseTarget,
                id=>RunUiAction(()=>{if(Current())view.PickMapTarget(id);}));
            HideDeploymentHud();
        }
        view.TreeExiting+=()=>{if(GodotObject.IsInstanceValid(map))map.EndDiplomacyTargetPick(view);};
        try
        {
            view.Configure(_repository!,_ui,table,()=>Current()&&_intelligenceQuestion is null,
                chosen=>RunUiAction(()=>
                {
                    if(!Current())return;
                    map.EndDiplomacyTargetPick(view);map.SetDetailsOpen(true,keepMapVisible:true);
                    bool accepted=chosen is null||accept(chosen.Value);
                    _intelligenceSelector=null;_intelligenceSelectorPage=IntelligenceUiPage.Other;
                    view.Abandon();view.QueueFree();editor.Show();
                    RefreshIntelligenceEditor();BindCurrentTutorialIntelligence();
                    if(!accepted)IntelligenceSelectionUnavailable("intelligence.selection_rejected");
                }),CurrentListScrollSpeed,mapQuery is null?null:new IntelligenceSelectionView.MapMode(
                    _intelligenceMode==IntelligenceUiMode.Away?0x36D:0x39A,
                    list=>RunUiAction(()=>BindMode(list)),map.SourceRightCancelReleased,
                    _intelligenceMode==IntelligenceUiMode.Away?new IntelligenceSelectionView.RelationToggle(
                        map.GetIntelligenceForceRelationTags(view),
                        relationship=>RunUiAction(()=>{if(Current())map.SetIntelligenceForceRelationTags(view,relationship);})):null));
            BindCurrentTutorialIntelligence();RefreshIntelligenceAdmission();
        }
        catch
        {
            map.EndDiplomacyTargetPick(view);map.SetDetailsOpen(true,keepMapVisible:true);
            _intelligenceSelector=null;_intelligenceSelectorPage=IntelligenceUiPage.Other;
            view.Abandon();view.QueueFree();editor.Show();editor.SetSuspended(false);throw;
        }
    }

    private void IntelligenceSelectionUnavailable(string? reason)
    {
        _playMessage.Text=_ui.Get("detail_unknown");_intelligenceEditor?.ApplyNotice(_playMessage.Text);
        if(reason is not null)GD.PushWarning(reason);
    }

    private void RefreshIntelligenceAdmission()
    {
        _intelligenceEditor?.SetReturnAllowed(IntelligenceSelectionReturnAllowed);
        _intelligenceEditor?.SetDecisionAllowed(_intelligenceMode==IntelligenceUiMode.Inspect||
            (IntelligenceSelectedForceId is int force&&IntelligenceForceAdmission?.Invoke(force)!=false&&
             IntelligencePersonAdmission?.Invoke(SelectedIntelligenceOfficer is int officer?[officer]:[])!=false));
        _intelligenceOverview?.ApplySelectionAdmission(IntelligenceForceAdmission,IntelligenceSelectionReturnAllowed);
        _intelligenceOverview?.ApplyIntelligenceAutomaticAdmission(IntelligenceForceAdmission is null&&IntelligencePersonAdmission is null);
        if(_intelligenceSelector is not {} selector)return;
        bool person=_intelligenceSelectorPage is IntelligenceUiPage.HomePerson or IntelligenceUiPage.AwayPerson;
        selector.ApplySelectionAdmission(person&&IntelligencePersonAdmission is {} people?id=>people([id]):
            _intelligenceSelectorPage==IntelligenceUiPage.AwayForce?IntelligenceForceAdmission:null,IntelligenceSelectionReturnAllowed);
        selector.SelectionConfirmationAdmission=person?IntelligencePersonAdmission:null;
    }

    private void ConfirmIntelligenceEditor()
    {
        if(!CanEditIntelligence)return;
        if(_intelligenceMode!=IntelligenceUiMode.Home){ConfirmIntelligenceOrders();return;}
        if(_intelligenceHome is null||_intelligenceHomeEditor is not {} editor||!editor.CanConfirm||
            IntelligenceForceAdmission?.Invoke(_intelligenceHomeTarget??0)==false||
            IntelligencePersonAdmission?.Invoke(SelectedIntelligenceOfficer is int person?[person]:[])==false)return;
        if(!_intelligenceHome.AdoptEditor(editor))return;
        IntelligenceLastEditorAcceptedMode=_intelligenceMode;IntelligenceLastEditorAcceptedForceId=_intelligenceHomeTarget;
        IntelligenceLastEditorAcceptedPersonIds=SelectedIntelligenceOfficer is int id?[id]:[];
        IntelligenceEditorAcceptedSequence++;ReturnIntelligenceHomeEditor();
    }

    private void ConfirmIntelligenceOrders()
    {
        if(!CanEditIntelligence)return;
        if(_intelligenceMode==IntelligenceUiMode.Away&&
            (IntelligenceSelectedForceId is not int target||IntelligenceForceAdmission?.Invoke(target)==false||
             IntelligencePersonAdmission?.Invoke(IntelligenceSelectedPersonIds)==false))return;
        if(!(_intelligenceMode switch {IntelligenceUiMode.Home=>_intelligenceHome?.CanConfirm,
            IntelligenceUiMode.Away=>_intelligenceAway?.CanConfirm,_=>_intelligenceInspect?.CanConfirm}??false))return;
        GameOrder? order=_intelligenceMode switch {IntelligenceUiMode.Home=>_intelligenceHome?.BuildOrder(),
            IntelligenceUiMode.Away=>_intelligenceAway?.BuildOrder(),_=>_intelligenceInspect?.BuildOrder()};
        if(order is null)return;
        var mode=_intelligenceMode;var force=IntelligenceSelectedForceId;
        var people=mode==IntelligenceUiMode.Home?_intelligenceHome!.Assignments.Where(a=>a.OfficerId!=0)
            .Select(a=>a.OfficerId).ToArray():IntelligenceSelectedPersonIds.ToArray();
        _intelligenceEditor?.SetSuspended(true);
        if(!ApplyPlayableOrder(order,()=>
        {IntelligenceLastAcceptedMode=mode;IntelligenceLastAcceptedForceId=force;IntelligenceLastAcceptedPersonIds=people;
            IntelligenceAcceptedSequence++;CloseIntelligenceOrders();}))
        {RefreshIntelligenceEditor();_intelligenceEditor?.ApplyNotice(_playMessage.Text);_intelligenceOverview?.ApplyNotice(_playMessage.Text);}
    }

    private void ReturnIntelligenceHomeEditor()
    {
        var view=_intelligenceEditor;_intelligenceEditor=null;_intelligenceHomeEditor=null;_intelligenceHomeTarget=null;
        view?.Abandon();view?.QueueFree();_intelligenceReturnedOverview=true;OpenIntelligenceOverview();
    }

    private void RequestIntelligenceBack()
    {
        if(!IsIntelligenceCurrent)return;
        if(_intelligenceQuestion is {} question){question.RequestCancel();return;}
        if(_intelligenceSelector is {} selector){selector.RequestCancel();return;}
        if(!IntelligenceSelectionReturnAllowed){_intelligenceOverview?.Show();return;}
        if(_intelligenceMode==IntelligenceUiMode.Home&&_intelligenceEditor is not null)
        {ReturnIntelligenceHomeEditor();return;}
        if(_intelligenceHome is { } home&&home.Forces.Any(force=>
            home.Assignments.FirstOrDefault(a=>a.TargetForceId==force.Id)?.OfficerId!=
            _intelligenceOriginalAssignments.FirstOrDefault(a=>a.TargetForceId==force.Id)?.OfficerId))
        {
            var dialog=new DeploymentTargetConfirmation();_intelligenceQuestion=dialog;AddChild(dialog);
            _intelligenceOverview?.Show();
            dialog.ConfigureWrappedMessage(_repository!,_ui,new AdministrationMessages(_repository!,_ui.SourceLocale).Format(0x56CA),answer=>RunUiAction(()=>
            {
                if(!IsIntelligenceCurrent||_intelligenceQuestion!=dialog)return;
                _intelligenceQuestion=null;dialog.Abandon();dialog.QueueFree();if(answer)CloseIntelligenceOrders();
                else {_intelligenceOverview?.Show();RefreshIntelligenceAdmission();}
            }));return;
        }
        CloseIntelligenceOrders();
    }

    private void CloseIntelligenceOrders()
    {
        var map=_intelligenceMap;
        if(_intelligenceSelector is {} picker&&map is not null&&GodotObject.IsInstanceValid(map))map.EndDiplomacyTargetPick(picker);
        _intelligenceMap=null;_intelligenceWorld=null;_intelligenceSession=null;
        _intelligenceHome=null;_intelligenceHomeEditor=null;_intelligenceAway=null;_intelligenceInspect=null;_intelligenceCards=null;
        var overview=_intelligenceOverview;_intelligenceOverview=null;overview?.Abandon();overview?.QueueFree();
        var editor=_intelligenceEditor;_intelligenceEditor=null;editor?.Abandon();editor?.QueueFree();
        var selector=_intelligenceSelector;_intelligenceSelector=null;selector?.Abandon();selector?.QueueFree();
        var question=_intelligenceQuestion;_intelligenceQuestion=null;question?.Abandon();question?.QueueFree();
        IntelligenceForceAdmission=null;IntelligencePersonAdmission=null;IntelligenceSelectionReturnAllowed=true;
        _intelligenceHomeTarget=null;_intelligenceSelectorPage=IntelligenceUiPage.Other;
        _intelligenceOriginalAssignments=[];
        if(map is not null&&GodotObject.IsInstanceValid(map)&&_residentMap==map)map.SetDetailsOpen(false);
        if(!_failed&&!_shuttingDown&&_residentMap is not null)SetMapFocus(_mapFocused);
    }
    private string IntelligenceMessage(int id)=>_repository!.LoadMessages(_ui.SourceLocale,MessageKind.Data)
        .Messages.Sections[1].Records[id].Text??throw new InvalidDataException($"Missing intelligence message {id:X4}.");
}
