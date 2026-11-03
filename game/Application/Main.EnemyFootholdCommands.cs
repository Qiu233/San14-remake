using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;
using San14.Client.Gameplay;

public partial class Main
{
    private sealed class EnemyFootholdWorkspace(GameplaySession session,GameState world,MapSceneView map,
        BaseKind kind,int id,Vector2 cursor)
    {
        internal GameplaySession Session {get;}=session;
        internal GameState World {get;set;}=world;
        internal MapSceneView Map {get;}=map;
        internal BaseKind Kind {get;}=kind;
        internal int Id {get;}=id;
        internal Vector2 Cursor {get;}=cursor;
        internal MapLocationCommandMenu? Menu {get;set;}
        internal Control? OwnedPage {get;set;}
        internal MapInformationListView? ResidentList {get;set;}
        internal OfficerDetailsView? ResidentDetail {get;set;}
        internal bool ChildActive {get;set;}
    }
    private EnemyFootholdWorkspace? _enemyFootholdCommands;
    private bool HasEnemyFootholdCommands=>_enemyFootholdCommands is {} owner&&
        (owner.OwnedPage is not null||owner.Menu?.IsVisibleInTree()==true);
    private bool IsEnemyFootholdCommandsCurrent=>_enemyFootholdCommands is {} owner&&EnemyFootholdCurrent(owner);
    private bool EnemyFootholdCurrent(EnemyFootholdWorkspace owner)=>!_failed&&!_shuttingDown&&
        ReferenceEquals(_enemyFootholdCommands,owner)&&ReferenceEquals(_session,owner.Session)&&
        ReferenceEquals(_world,owner.World)&&ReferenceEquals(owner.Session.World,owner.World)&&_residentMap==owner.Map;

    private void AdoptEnemyFootholdSpeechWorld(GameplaySession session,GameState previous,GameState replacement)
    {
        if(_enemyFootholdCommands is not {} owner||owner.Session!=session||owner.Map!=_residentMap||
            !ReferenceEquals(owner.World,previous))return;
        owner.World=replacement;
        owner.ResidentDetail?.AdoptRetainedSpeechWorld(replacement);
    }

    private void BindEnemyFootholdCommands(MapSceneView map)
        =>map.EnemyFootholdSelected+=(kind,id,point,level)=>RunUiAction(()=>OpenEnemyFootholdCommands(map,kind,id,point));

    private void OpenEnemyFootholdCommands(MapSceneView map,BaseKind kind,int id,Vector2 cursor)
    {
        if(_failed||_shuttingDown||_advancingPeriod||IsReturningToTitle||!TutorialEntryAllowed||
            HasAdministrationPage||HasMilitaryOrderPage||HasCampaignSavePage||HasMapSystemMenu||HasCommandWorkspace||
            HasActionReport||HasPersonnelConfirmation||_detailsView is not null||_repository is null||
            _residentMap!=map||_campaignComposition is null||_session is not {PlayerForceId:int} session||
            session.HasPendingOperation||session.World is not {Phase:TurnPhase.Strategy} world)return;
        var owner=new EnemyFootholdWorkspace(session,world,map,kind,id,cursor);
        _enemyFootholdCommands=owner;
        try {if(!ShowEnemyFootholdMenu(owner))CloseEnemyFootholdCommands();}
        catch {CloseEnemyFootholdCommands();throw;}
    }

    private AreaAdministrationQuery<EnemyFootholdCommandMenuProjection> QueryEnemyFootholdMenu(EnemyFootholdWorkspace owner)
        =>_campaignComposition!.PrepareArmyQueries(owner.World).QueryEnemyFootholdCommandMenu(owner.World,
            owner.Kind,owner.Id,owner.Map.ReadEnemyFootholdCameraLevel(),true);

    private bool ShowEnemyFootholdMenu(EnemyFootholdWorkspace owner)
    {
        if(_repository is null||!ReferenceEquals(_enemyFootholdCommands,owner))return false;
        var query=QueryEnemyFootholdMenu(owner);
        if(!query.Available||query.Value is not {} page)
        {EnemyFootholdUnavailable(query.UnresolvedInput??"enemy-foothold-menu.current_owner");return false;}
        var partial=page.Entries.SelectMany(entry=>new[]{entry.SourcePresent.UnresolvedInput,entry.SourceEnabled.UnresolvedInput})
            .FirstOrDefault(missing=>missing is not null);
        if(partial is not null)EnemyFootholdUnavailable(partial);
        owner.ChildActive=false;
        owner.Menu?.Abandon();owner.Menu?.QueueFree();
        var menu=new MapLocationCommandMenu{Name="EnemyFootholdCommandMenu"};owner.Menu=menu;AddChild(menu);
        menu.Configure(_repository,_ui,owner.Cursor,page.Entries.Select(entry=>new MapLocationCommandMenuEntry(
            entry.Command.ToString(),entry.Payload,entry.CaptionId,entry.SourcePresent,entry.SourceEnabled)).ToArray(),
            ()=>EnemyFootholdCurrent(owner)&&!owner.ChildActive,
            payload=>RunUiAction(()=>AcceptEnemyFootholdCommand(owner,payload)),
            ()=>RunUiAction(CloseEnemyFootholdCommands), resources:TryBorrowApplicationUiResources(menu));
        owner.Map.CloseCityCommandMenu();owner.Map.SetDetailsOpen(true,keepMapVisible:true);
        SetMapFocus(true);HideDeploymentHud();return true;
    }

    private void AcceptEnemyFootholdCommand(EnemyFootholdWorkspace owner,int payload)
    {
        if(!EnemyFootholdCurrent(owner)||owner.ChildActive)return;
        var query=QueryEnemyFootholdMenu(owner);
        var page=query.Value;
        var entry=page?.Entries.FirstOrDefault(row=>row.Payload==payload);
        if(!query.Available||entry is not {SourcePresent.Available:true,SourcePresent.Value:true,
            SourceEnabled.Available:true,SourceEnabled.Value:true})
        {EnemyFootholdUnavailable(query.UnresolvedInput??entry?.SourceEnabled.UnresolvedInput??"enemy-foothold-menu.changed");return;}
        owner.ChildActive=true;owner.Menu?.Hide();owner.Map.SetDetailsOpen(false,keepMapVisible:true);
        Action returned=()=>ReturnEnemyFootholdChild(owner);
        bool opened=false;
        try
        {
            switch(entry.Command)
            {
                case EnemyFootholdCommandMenuCommand.UnitTarget:
                    opened=OpenGeneralUnitTarget(new(DeploymentTargetKind.Location,owner.Id),returned);break;
                case EnemyFootholdCommandMenuCommand.Strategic:
                    OpenStrategicOrders(13,page!.AreaId,returned);opened=HasStrategicOrdersPage;break;
                case EnemyFootholdCommandMenuCommand.CityInformation:
                    OpenMapCityDetails(owner.Map,owner.Id,returned);opened=_detailsView is not null;break;
                case EnemyFootholdCommandMenuCommand.GateInformation:
                    opened=OpenEnemyGateInformation(owner);break;
                case EnemyFootholdCommandMenuCommand.AreaInformation:
                    OpenMapAreaDetails(owner.Map,page!.AreaId,returned);opened=_detailsView is not null;break;
                case EnemyFootholdCommandMenuCommand.Government:
                    opened=OpenEnemyGovernment(owner,page!.ForceId);break;
            }
        }
        catch {ReturnEnemyFootholdChild(owner);throw;}
        if(!opened)ReturnEnemyFootholdChild(owner);
    }

    private bool OpenEnemyGateInformation(EnemyFootholdWorkspace owner)
    {
        var publication=_campaignComposition!.PrepareArmyQueries(owner.World).QueryGateInformation(owner.World,owner.Id);
        var view=new GateDetailsView();owner.OwnedPage=view;AddChild(view);
        try
        {
            view.TransitionFailure = ReportPlayFailure;
            view.Configure(_repository!,_ui,publication,()=>RunUiAction(()=>ReturnEnemyFootholdChild(owner)),
                TryBorrowApplicationUiResources(view),
                ()=>RunUiAction(()=>OpenEnemyGateResidents(owner)));
            view.RegisterHeader(_sourceHeaders);
            owner.Map.SetDetailsOpen(true,keepMapVisible:true);HideDeploymentHud();return true;
        }
        catch {owner.OwnedPage=null;view.Abandon();view.QueueFree();throw;}
    }

    private bool OpenEnemyGovernment(EnemyFootholdWorkspace owner,int forceId)
    {
        var roster=_campaignComposition!.PrepareArmyQueries(owner.World).QueryGovernanceRoster(owner.World,forceId);
        if(!owner.World.Forces.TryGetValue(forceId,out var force)||roster.Groups.Count!=5)
        {EnemyFootholdUnavailable(roster.UnresolvedInput??"government.current_force");return false;}
        var messages=_repository!.LoadMessages(_ui.SourceLocale,MessageKind.Data).Messages.Sections[0].Records;
        var model=new GovernanceRosterPageModel(force.Name,
            roster.Advisor is {} advisor?Person(advisor.OfficerId,advisor.PendingValue):null,[],
            roster.Groups.Select(group=>new GovernanceRosterDepartment(group.Group,
                messages[group.NameMessageId].Text??throw new InvalidDataException("Missing government caption."),
                group.Capacity,group.Expansion,group.Members.Select(person=>Person(person.OfficerId,person.PendingValue)).ToArray(),[])).ToArray(),false);
        model=GovernanceRosterPresentation.Enrich(owner.World,forceId,model,
            _campaignComposition.PrepareArmyQueries(owner.World),
            _campaignComposition.DetailForces(owner.World,[forceId]).GetValueOrDefault(forceId),
            doctrines:_campaignComposition.Scenario.Snapshot.DoctrineParameters);
        var view=new GovernanceRosterPage();owner.OwnedPage=view;AddChild(view);
        try
        {
            view.Configure(_repository,_ui,_playCommandDeck.Theme,model,()=>ReturnEnemyFootholdChild(owner),
                ()=>ReturnEnemyFootholdChild(owner),_=>{});
            view.ApplyEvaluation(new(false,null,roster.PolicyCost,roster.CostLimit,null,""));
            view.SetMeta("read_only_enemy_force",forceId);
            owner.Map.SetDetailsOpen(true,keepMapVisible:true);HideDeploymentHud();return true;
        }
        catch {owner.OwnedPage=null;view.Abandon();view.QueueFree();throw;}
        GovernanceRosterOfficer Person(int id,int? pending)
        {
            var person=owner.World.Officers.GetValueOrDefault(id);
            string? policy=person?.PolicyId is int key?owner.World.Policies.GetValueOrDefault(key)?.Name:null;
            int? cost=roster.Candidates.FirstOrDefault(candidate=>candidate.OfficerId==id)?.PolicyCost;
            return new(id,person?.Name??_ui.Get("detail_unknown"),policy,cost,pending,false);
        }
    }

    private void ReturnEnemyFootholdChild(EnemyFootholdWorkspace owner)
    {
        if(!ReferenceEquals(_enemyFootholdCommands,owner))return;
        RetireEnemyFootholdOwnedPage(owner);
        if(_failed||_shuttingDown||IsReturningToTitle||!ReferenceEquals(_session,owner.Session)||_residentMap!=owner.Map||
            owner.Session.World.Phase!=TurnPhase.Strategy)
        {CloseEnemyFootholdCommands();return;}
        owner.World=owner.Session.World;
        if(!ShowEnemyFootholdMenu(owner))CloseEnemyFootholdCommands();
    }
    private void RequestEnemyFootholdCommandsBack()
    {
        if(_enemyFootholdCommands is not {} owner)return;
        if(!EnemyFootholdCurrent(owner)){CloseEnemyFootholdCommands();return;}
        if(owner.ResidentDetail is {} detail)detail.RequestClose();
        else if(owner.ResidentList is not null)CloseEnemyGateResidents(owner);
        else if(owner.OwnedPage is GateDetailsView gate)gate.RequestClose();
        else if(owner.OwnedPage is not null)ReturnEnemyFootholdChild(owner);
        else if(!owner.ChildActive)CloseEnemyFootholdCommands();
    }
    private void CloseEnemyFootholdCommands()
    {
        var owner=_enemyFootholdCommands;_enemyFootholdCommands=null;if(owner is null)return;
        RetireEnemyFootholdOwnedPage(owner);owner.Menu?.Abandon();owner.Menu?.QueueFree();
        if(GodotObject.IsInstanceValid(owner.Map)&&_residentMap==owner.Map)owner.Map.SetDetailsOpen(false);
        if(!_failed&&!_shuttingDown&&_residentMap is not null)SetMapFocus(_mapFocused);
    }
    private static void RetireEnemyFootholdOwnedPage(EnemyFootholdWorkspace owner)
    {
        var detail=owner.ResidentDetail;owner.ResidentDetail=null;detail?.Hide();detail?.QueueFree();
        var list=owner.ResidentList;owner.ResidentList=null;list?.Abandon();list?.QueueFree();
        var page=owner.OwnedPage;owner.OwnedPage=null;
        if(page is GateDetailsView gate)gate.Abandon();
        else if(page is GovernanceRosterPage government)government.Abandon();
        page?.QueueFree();
    }
    private void EnemyFootholdUnavailable(string missing)
    {_playMessage.Text=_ui.Get("information_partial");GD.PushWarning(missing);}
}
