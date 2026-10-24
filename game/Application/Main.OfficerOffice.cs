using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;
using San14.Client.Gameplay;

public partial class Main
{
    private OfficerOfficePage? _officerOfficePage;
    private OfficerOfficeDraft? _officerOfficeDraft;
    private TalentPersonSelectionView? _officerOfficePeople;
    private DeploymentTargetConfirmation? _officerOfficeConfirmation;
    private GameplaySession? _officerOfficeSession;
    private GameState? _officerOfficeWorld;
    private MapSceneView? _officerOfficeMap;
    private bool HasOfficerOfficePage => _officerOfficePage is not null;
    private bool IsOfficerOfficeCurrent => !_failed && !_shuttingDown && HasOfficerOfficePage &&
        ReferenceEquals(_session,_officerOfficeSession) && ReferenceEquals(_world,_officerOfficeWorld) &&
        _residentMap == _officerOfficeMap;

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
        var candidates = draft.QueryCandidates(officeId).Select(candidate=>new TalentPersonChoice(
            candidate.OfficerId,candidate.Name,candidate.CurrentOfficeName,world.Forces[player].Name,
            candidate.Abilities.Value?.Intelligence,candidate.Abilities.Value?.Politics,candidate.Abilities.Value?.Charm,
            candidate.Enabled,candidate.DisabledReason)).ToArray();
        var messages=_repository.LoadMessages(_ui.SourceLocale,MessageKind.Data).Messages.Sections[1];
        string Message(int id)=>messages.Records[id].Text??throw new InvalidDataException("Missing office selector caption.");
        var selector=new TalentPersonSelectionView();_officerOfficePeople=selector;AddChild(selector);
        _officerOfficePage!.SetBusy(true);
        try
        {
            selector.Configure(_repository,_ui,candidates,[],1,true,
                ()=>IsOfficerOfficeCurrent&&_officerOfficePeople==selector,
                selected=>RunUiAction(()=>
                {
                    if(!IsOfficerOfficeCurrent||_officerOfficePeople!=selector)return;
                    CloseOfficerOfficePeople();
                    if(selected is {Count:1})draft.Assign(officeId,selected[0]);
                    _officerOfficePage!.Apply();
                }),CurrentListScrollSpeed,titleMessage:0x032E,firstContextCaption:Message(0x05DD),
                extraCaption:Message(0x029E),extraAction:()=>RunUiAction(()=>
                {
                    if(!IsOfficerOfficeCurrent||_officerOfficePeople!=selector)return;
                    if(!draft.Assign(officeId,null))return;
                    // Keep the actual person-selection stage open with its
                    // updated draft eligibility; no order has been issued.
                    CloseOfficerOfficePeople();OpenOfficerOfficePeople(officeId);
                }),extraEnabled:slot.AssignedOfficer is not null);
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
        _officerOfficePage?.SetBusy(false);
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
