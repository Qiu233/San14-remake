using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private TalentAutoSearchView? _talentAutoView;
    private TalentPersonSelectionView? _talentAutoPersons;
    private TalentAutoSearchDraft? _talentAutoDraft;
    private GameplaySession? _talentAutoSession;
    private GameState? _talentAutoWorld;
    private MapSceneView? _talentAutoMap;
    private bool HasTalentAutoSearchPage=>_talentAutoView is not null;
    private bool IsTalentAutoSearchCurrent=>!_failed&&!_shuttingDown&&_talentAutoView is not null&&_talentAutoDraft is not null&&
        ReferenceEquals(_session,_talentAutoSession)&&ReferenceEquals(_world,_talentAutoWorld)&&_residentMap==_talentAutoMap&&_talentAutoMap is not null;
    private bool CanEditTalentAutoSearch=>IsTalentAutoSearchCurrent&&_talentAutoPersons is null;

    private void OpenTalentAutoSearch()
    {
        if(_failed||_shuttingDown||_advancingPeriod||IsReturningToTitle||HasAdministrationPage||HasMilitaryOrderPage||
            HasCampaignSavePage||HasMapSystemMenu||HasCommandWorkspace||HasActionReport||HasPersonnelConfirmation||
            _detailsView is not null||_repository is null||_campaignComposition is not {} composition||
            _session is not { PlayerForceId:int force } session||session.HasPendingOperation||
            session.World is not { Phase:TurnPhase.Strategy,NativeArmySimulation:not null } world||
            _residentMap is not { IsPlayableMapReady:true } map)return;
        var district=world.Districts.Values.SingleOrDefault(value=>value.ForceId==force&&value.Number==1);
        if(district is null){_playMessage.Text=_ui.Get("detail_unknown");return;}
        TalentAutoSearchDraft draft;
        try{draft=composition.PrepareArmyQueries(world).OpenTalentAutoSearchDraft(world,force,district.Id,new(_presentationRandom.State));}
        catch(InvalidDataException error){_playMessage.Text=_ui.Get("talent_rule_unavailable");GD.PushWarning(error.ToString());return;}
        _talentAutoDraft=draft;_talentAutoSession=session;_talentAutoWorld=world;_talentAutoMap=map;
        SynchronizeTalentAutoRandom();
        var view=new TalentAutoSearchView();_talentAutoView=view;AddChild(view);
        try
        {
            SetMapFocus(true);map.CloseCityCommandMenu();map.SetDetailsOpen(true,keepMapVisible:true);HideDeploymentHud();
            view.Configure(_repository,_ui,()=>CanEditTalentAutoSearch,
                ()=>RunUiAction(RequestTalentAutoSearchBack),()=>RunUiAction(OpenTalentAutoPersonSelection),
                (group,value)=>RunUiAction(()=>ChangeTalentAutoSearchOption(group,value)));
            RefreshTalentAutoSearch();
        }
        catch{CloseTalentAutoSearch();throw;}
    }

    private void RefreshTalentAutoSearch()
    {
        if(!IsTalentAutoSearchCurrent)return;
        var draft=_talentAutoDraft!;
        _talentAutoView!.Apply(new(draft.ExecutorPolicy,draft.TravelRange,draft.AllocationPolicy,
            draft.AvailableBooks,draft.PlannedBooks,draft.PreviewExecutorCount,draft.CanPrepareSelection,
            draft.UnresolvedInput is not null?_ui.Get("talent_rule_unavailable"):null));
        _talentAutoView.SetBusy(!CanEditTalentAutoSearch);
    }

    private void ChangeTalentAutoSearchOption(int group,int value)
    {
        if(!CanEditTalentAutoSearch)return;
        var draft=_talentAutoDraft!;
        try
        {
            switch(group)
            {
                case 0:draft.SetExecutorPolicy(value);break;
                case 1:draft.SetTravelRange(value);break;
                case 2:draft.SetAllocationPolicy(value);break;
                default:throw new ArgumentOutOfRangeException(nameof(group));
            }
        }
        catch(InvalidDataException error){GD.PushWarning(error.ToString());}
        finally{SynchronizeTalentAutoRandom();}
        RefreshTalentAutoSearch();
    }

    private void OpenTalentAutoPersonSelection()
    {
        if(!CanEditTalentAutoSearch||_repository is null||_talentAutoDraft?.CanPrepareSelection!=true)return;
        var draft=_talentAutoDraft!;
        try{draft.PrepareSelection();}
        catch(InvalidDataException error){GD.PushWarning(error.ToString());RefreshTalentAutoSearch();return;}
        finally{SynchronizeTalentAutoRandom();}
        if(!draft.CanPrepareSelection){RefreshTalentAutoSearch();return;}
        var world=_talentAutoWorld!;
        var rows=draft.Candidates.Select(choice=>
        {
            var actor=world.Officers[choice.OfficerId];var card=draft.QueryOfficerCard(choice.OfficerId);
            int? Ability(int dimension)=>card.Available?card.Value!.Abilities.FirstOrDefault(value=>value.Dimension==dimension)?.Effective.Value:null;
            string location=actor.LocationId is int city?world.Cities.GetValueOrDefault(city)?.Name??world.Gates.GetValueOrDefault(city)?.Name??_ui.Get("detail_unknown"):_ui.Get("detail_unknown");
            string force=actor.ForceId is int owner?world.Forces.GetValueOrDefault(owner)?.Name??_ui.Get("detail_unknown"):_ui.Get("detail_unknown");
            return new TalentPersonChoice(choice.OfficerId,actor.Name,location,force,Ability(2),Ability(3),Ability(4));
        }).ToArray();
        var selector=new TalentPersonSelectionView();_talentAutoPersons=selector;AddChild(selector);
        _talentAutoView!.SetBusy(true);_talentAutoView.Hide();
        try
        {
            selector.Configure(_repository,_ui,rows,draft.PreviewSelectedExecutorOfficerIds,draft.MaximumExecutors,false,
                ()=>IsTalentAutoSearchCurrent&&_talentAutoPersons==selector,selected=>RunUiAction(()=>
                {
                    if(!IsTalentAutoSearchCurrent||_talentAutoPersons!=selector||_talentAutoDraft!=draft)return;
                    _talentAutoPersons=null;selector.Abandon();selector.QueueFree();_talentAutoView!.Show();
                    if(selected is not null)
                    {
                        try{draft.SelectExecutors(selected);}
                        catch(InvalidDataException error){GD.PushWarning(error.ToString());RefreshTalentAutoSearch();return;}
                        finally{SynchronizeTalentAutoRandom();}
                        RunPlayAction(ConfirmTalentAutoSearch);
                    }
                    else RefreshTalentAutoSearch();
                }),CurrentListScrollSpeed,titleMessage:0x058F);
        }
        catch{_talentAutoPersons=null;selector.Abandon();selector.QueueFree();_talentAutoView!.Show();RefreshTalentAutoSearch();throw;}
    }

    private void ConfirmTalentAutoSearch()
    {
        if(!CanEditTalentAutoSearch||_talentAutoDraft?.CanSubmit!=true){RefreshTalentAutoSearch();return;}
        var view=_talentAutoView!;view.SetBusy(true);
        try
        {
            if(!ApplyPlayableOrder(_talentAutoDraft.BuildOrder(),CloseTalentAutoSearch))
            {RefreshTalentAutoSearch();view.ApplyNotice(_playMessage.Text);}
        }
        catch{if(IsTalentAutoSearchCurrent)RefreshTalentAutoSearch();throw;}
    }

    private void SynchronizeTalentAutoRandom()
    {if(_talentAutoDraft?.AppRandomState is {} random)_presentationRandom.InstallCommittedState(random.State);}
    private void RequestTalentAutoSearchBack()
    {
        if(!IsTalentAutoSearchCurrent)return;
        if(_talentAutoPersons is {} selector){selector.RequestCancel();return;}
        CloseTalentAutoSearch();OpenTalentOrders(TalentOrderKind.Search);
    }
    private void CloseTalentAutoSearch()
    {
        if(_talentAutoView is null&&_talentAutoDraft is null&&_talentAutoMap is null)return;
        var map=_talentAutoMap;_talentAutoMap=null;_talentAutoSession=null;_talentAutoWorld=null;_talentAutoDraft=null;
        var persons=_talentAutoPersons;_talentAutoPersons=null;var view=_talentAutoView;_talentAutoView=null;
        persons?.Abandon();persons?.QueueFree();view?.Abandon();view?.QueueFree();
        if(map is not null&&GodotObject.IsInstanceValid(map)&&_residentMap==map)map.SetDetailsOpen(false);
        if(!_failed&&!_shuttingDown&&_residentMap is not null)SetMapFocus(_mapFocused);
    }
}
