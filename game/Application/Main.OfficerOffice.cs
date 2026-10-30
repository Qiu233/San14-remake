using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;
using San14.Client.Gameplay;

public partial class Main
{
    private OfficerOfficePage? _officerOfficePage;
    private OfficerOfficeDraft? _officerOfficeDraft;
    private OfficerOfficePersonSelectionView? _officerOfficePeople;
    private DeploymentTargetConfirmation? _officerOfficeConfirmation;
    private GameplaySession? _officerOfficeSession;
    private GameState? _officerOfficeWorld;
    private MapSceneView? _officerOfficeMap;
    private bool HasOfficerOfficePage => _officerOfficePage is not null;
    private bool IsOfficerOfficeCurrent => !_failed && !_shuttingDown && HasOfficerOfficePage &&
        ReferenceEquals(_session,_officerOfficeSession) && ReferenceEquals(_world,_officerOfficeWorld) &&
        _residentMap == _officerOfficeMap;

    private void AdoptOfficerOfficeSpeechWorld(GameplaySession session, GameState previous, GameState replacement)
    {
        // Keep the captured rules, edited appointments and pending selector/confirmation.
        if (_officerOfficePage is null || _officerOfficeDraft is null || _officerOfficeSession != session ||
            _officerOfficeMap is null || _officerOfficeMap != _residentMap ||
            !ReferenceEquals(_officerOfficeWorld, previous)) return;
        _officerOfficeWorld = replacement;
    }

    private void OpenOfficerOffices()
    {
        if (_failed || _shuttingDown || _advancingPeriod || IsReturningToTitle || HasAdministrationPage ||
            HasMilitaryOrderPage || HasCampaignSavePage || HasMapSystemMenu || HasCommandWorkspace ||
            HasActionReport || HasPersonnelConfirmation || _detailsView is not null || _repository is null ||
            _campaignComposition is not { } composition || _residentMap is not { IsPlayableMapReady:true } map ||
            _session is not { PlayerForceId:int player } session || session.HasPendingOperation ||
            session.World is not { Phase:TurnPhase.Strategy } world ||
            PlayableOutcome.Evaluate(world,player) != PlayableResult.Ongoing) return;
        var draft = composition.PrepareArmyQueries(world).OpenOfficerOfficeDraft(world,player);
        var view = new OfficerOfficePage(); _officerOfficePage=view; _officerOfficeDraft=draft;
        _officerOfficeSession=session; _officerOfficeWorld=world; _officerOfficeMap=map;
        AddChild(view);
        try
        {
            view.Configure(_repository,_ui,draft,()=>IsOfficerOfficeCurrent,OpenOfficerOfficePeople,
                RequestOfficerOfficesBack,()=>RunPlayAction(CommitOfficerOffices),
                ()=>EditOfficerOffices(draft.AutoPromote),()=>EditOfficerOffices(draft.AutoAssign),
                ()=>EditOfficerOffices(draft.DismissAll),CurrentListScrollSpeed);
            map.CloseCityCommandMenu(); map.SetDetailsOpen(true,keepMapVisible:true);
            SetMapFocus(true); HideDeploymentHud();
        }
        catch { CloseOfficerOffices(); throw; }
    }

    private void EditOfficerOffices(Func<bool> edit)
    {
        if (!IsOfficerOfficeCurrent || _officerOfficePeople is not null || _officerOfficeConfirmation is not null) return;
        RunUiAction(()=> { if(IsOfficerOfficeCurrent) { edit(); _officerOfficePage!.Apply(); } });
    }

    private void OpenOfficerOfficePeople(int officeId)
    {
        if (!IsOfficerOfficeCurrent || _officerOfficeDraft is not { } draft || _officerOfficePeople is not null ||
            _officerOfficeConfirmation is not null || _repository is null || _officerOfficeWorld is not { } world ||
            _officerOfficeSession?.PlayerForceId is not int player) return;
        var slot = draft.Rows.FirstOrDefault(row=>row.OfficeId==officeId);
        if(slot is not { Enabled:true }) return;
        var prepared=draft.ReadPeopleSelection(officeId);
        if(!prepared.Available || prepared.Value is not {} model)
        {
            if(prepared.UnresolvedInput is {} missing)GD.PushWarning(missing);
            return;
        }
        var selector=new OfficerOfficePersonSelectionView();_officerOfficePeople=selector;AddChild(selector);
        _officerOfficePage!.SetBusy(true);_officerOfficePage.Hide();
        try
        {
            selector.Configure(_repository,_ui,model,slot.OfficeName,
                ()=>IsOfficerOfficeCurrent&&_officerOfficePeople==selector,
                selected=>RunUiAction(()=>
                {
                    if(!IsOfficerOfficeCurrent||_officerOfficePeople!=selector)return;
                    CloseOfficerOfficePeople();
                    if(selected is int id)draft.Assign(officeId,id);
                    _officerOfficePage!.Apply();
                }),()=>RunUiAction(()=>
                {
                    if(!IsOfficerOfficeCurrent||_officerOfficePeople!=selector)return;
                    if(!draft.Assign(officeId,null))return;
                    //The original Dismiss refreshes this dialog and closes
                    //with no selected Person. It does not reopen a selector.
                    CloseOfficerOfficePeople();_officerOfficePage!.Apply();
                }),CurrentListScrollSpeed);
        }
        catch { CloseOfficerOfficePeople(); throw; }
    }

    private void CommitOfficerOffices()
    {
        if(!IsOfficerOfficeCurrent||_officerOfficeDraft is not { } draft||_officerOfficePeople is not null||
            _officerOfficeConfirmation is not null)return;
        if(draft.BuildOrder() is not { } order){_officerOfficePage!.Apply();return;}
        if(!ApplyPlayableOrder(order,CloseOfficerOffices))_officerOfficePage?.Apply();
    }

    private void RequestOfficerOfficesBack()
    {
        if(!IsOfficerOfficeCurrent)return;
        if(_officerOfficePeople is { } people){people.RequestCancel();return;}
        if(_officerOfficeConfirmation is { } current){current.RequestCancel();return;}
        if(_officerOfficeDraft?.CanCommit!=true){CloseOfficerOffices();return;}
        var question=new DeploymentTargetConfirmation();_officerOfficeConfirmation=question;AddChild(question);
        _officerOfficePage!.SetBusy(true);
        try
        {
            question.ConfigureGameMessage(_repository!,_ui,0x5979,accepted=>RunUiAction(()=>
            {
                if(!IsOfficerOfficeCurrent||_officerOfficeConfirmation!=question)return;
                _officerOfficeConfirmation=null;question.Abandon();question.QueueFree();
                if(accepted)CloseOfficerOffices();else _officerOfficePage!.SetBusy(false);
            }));
        }
        catch
        {
            _officerOfficeConfirmation=null;question.Abandon();question.QueueFree();
            _officerOfficePage!.SetBusy(false);throw;
        }
    }

    private void CloseOfficerOfficePeople()
    {
        var view=_officerOfficePeople;_officerOfficePeople=null;
        if(view is not null){view.Abandon();view.GetParent()?.RemoveChild(view);view.QueueFree();}
        _officerOfficePage?.SetBusy(false);_officerOfficePage?.Show();
    }

    private void CloseOfficerOffices()
    {
        CloseOfficerOfficePeople();
        var question=_officerOfficeConfirmation;_officerOfficeConfirmation=null;
        if(question is not null){question.Abandon();question.QueueFree();}
        var view=_officerOfficePage;var map=_officerOfficeMap;
        _officerOfficePage=null;_officerOfficeDraft=null;_officerOfficeSession=null;_officerOfficeWorld=null;_officerOfficeMap=null;
        if(view is not null){view.Abandon();view.GetParent()?.RemoveChild(view);view.QueueFree();}
        if(!_failed&&!_shuttingDown&&map is not null&&map==_residentMap&&GodotObject.IsInstanceValid(map))
        {map.SetDetailsOpen(false);SetMapFocus(true);}
    }
}
