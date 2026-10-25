using Godot;
using San14.Core;
using San14.Data;

public partial class Main
{
    private AreaAdministrationParentView? _administrationParent;
    private AreaAdministrationPresenter? _administrationPresenter;
    private DeploymentNarrationView? _administrationNarration;
    private MapSceneView? _administrationMap;
    private GameState? _administrationWorld;
    private AreaAdministrationQuery<AreaAdministrationHandover>? _administrationHandover;
    private long _administrationHandoverRevision=-1;
    private bool HasAdministrationPage=>_administrationParent is not null||_administrationNarration is not null||HasCityDomesticPage||HasCityMerchantPage||HasCityOfficerMovementPage||HasGovernanceRosterPage||HasPersonnelItemPage||HasOfficerOfficePage||HasPersonnelAdministrationPage||HasPersonnelGroupsPage||HasDistrictAdministrationPage||HasDoctrineResearchPage||HasStrategicOrdersPage||HasCommanderSelectionPage||HasTalentOrdersPage||HasTalentAutoSearchPage||HasDiplomacyOrdersPage||_governanceNarration is not null||HasPersonnelPresentation||HasForcePolicyPresentation||HasForceExperienceTransition||HasStoryPresentation;

    private bool IsAdministrationCurrent(GameState world,MapSceneView map)=>
        !_failed&&!_shuttingDown&&ReferenceEquals(_world,world)&&_residentMap==map&&
        ReferenceEquals(_administrationWorld,world)&&_administrationMap==map;

    private void OpenAreaAdministration(int cityId)
    {
        if(HasAdministrationPage||HasMilitaryOrderPage||HasCampaignSavePage||HasMapSystemMenu||HasCommandWorkspace||_detailsView is not null||_repository is null||
            _world is not { } world||_campaignComposition is not { } composition||_residentMap is not { IsPlayableMapReady:true } map||
            _playerForceId is not int force||!world.Cities.ContainsKey(cityId)||
            !San14.Client.Gameplay.DeploymentBatchView.CanComposeAt(world,force,cityId))return;
        var opened=composition.PrepareArmyQueries(world).OpenAreaAdministrationDraft(world,cityId);
        if(!opened.Available){AdministrationUnavailable(opened.UnresolvedInput!);return;}
        _administrationWorld=world;_administrationMap=map;
        var view=new AreaAdministrationParentView();_administrationParent=view;AddChild(view);
        try
        {
            map.BeginAdministrationMapDisplay(opened.Value!);
            SetMapFocus(true);map.CloseCityCommandMenu();HideDeploymentHud();
            _administrationPresenter=new(view,_repository,_ui,opened.Value!,
                ()=>IsAdministrationCurrent(world,map)&&_administrationParent==view,RunUiAction,
                ()=>ReturnFromAdministration(world,map,cityId),
                ()=>RunPlayAction(()=>PrepareAdministrationCommit(world,map,cityId,force,view)),
                enabled=>SetAdministrationMapInput(map,enabled),AdministrationUnavailable,_presentationRandom.Next,CurrentListScrollSpeed);
        }
        catch
        {
            opened.Value!.Dispose();CloseAreaAdministration();throw;
        }
    }

    private void SetAdministrationMapInput(MapSceneView map,bool enabled)
    {
        if(_administrationMap!=map||_residentMap!=map)return;
        // A dedicated picker owns even rejected clicks. Keeping it installed
        // prevents ordinary city double-click behavior leaking into this page.
        map.SetCommandTargetPicker(hex=>_administrationPresenter?.PickMapTarget(hex),
            hex=>_administrationPresenter?.AllowMapPointer(hex)==true);
        map.SetDetailsOpen(!enabled,keepMapVisible:true);HideDeploymentHud();
    }

    private void PrepareAdministrationCommit(GameState world,MapSceneView map,int city,int force,
        AreaAdministrationParentView view)
    {
        if(!IsAdministrationCurrent(world,map)||_administrationParent!=view||
            _administrationPresenter is not { } presenter||_administrationNarration is not null)return;
        var draft=presenter.Draft;
        if(_administrationHandover is null||_administrationHandoverRevision!=draft.Revision)
        {
            _administrationHandover=draft.PrepareHandover(_ui.SourceLocale,_presentationRandom.Next);
            _administrationHandoverRevision=draft.Revision;
        }
        // Retain failed preparations as well: refreshing or clicking again
        // cannot redraw a more convenient speaker or message branch.
        var preparation=_administrationHandover;
        if(!preparation.Available){AdministrationUnavailable(preparation.UnresolvedInput!);return;}
        var changes=draft.GetCommitChanges();
        if(!changes.Available){AdministrationUnavailable(changes.UnresolvedInput!);return;}
        var accepted=changes.Value!.ToArray();
        var handover=preparation.Value!;
        if(handover.DraftRevision!=draft.Revision)return;
        if(accepted.Length==0){ReturnFromAdministration(world,map,city);return;}
        if(handover.Kind==AreaAdministrationHandoverKind.DirectCommit)
        {
            Submit();return;
        }
        var speech=handover.Narration??throw new InvalidDataException("Administration handover text is unavailable.");
        var route=speech.PortraitRoute??throw new InvalidDataException("Administration standing portrait is unavailable.");
        var sprites=new LocalizedUiSpriteSource(_repository!);
        var layers=new[]{route.SpriteB,route.SpriteA,route.SpriteC}
            .Select(name=>sprites.LoadTexturePackSprite(route.ResourceName,name)).ToArray();
        var narration=new DeploymentNarrationView { KeyboardInput = HandleGameplayNarrationKeyboard };_administrationNarration=narration;AddChild(narration);
        try
        {
            presenter.SetBusy(true);
            narration.Configure(_repository!,_ui,speech.SpeakerName,speech.Text,layers,()=>RunPlayAction(()=>
            {
                if(_administrationNarration!=narration)return;
                if(!IsAdministrationCurrent(world,map)||_administrationPresenter!=presenter||draft.Revision!=handover.DraftRevision)
                {CloseAreaAdministration();return;}
                _administrationNarration=null;narration.Abandon();narration.QueueFree();
                Submit();
            }), () => _preferences.AutomaticMessageAdvance,
                () => CanAdvanceNarration&&_administrationNarration==narration&&IsAdministrationCurrent(world,map)&&_administrationPresenter==presenter&&draft.Revision==handover.DraftRevision);
        }
        catch
        {
            _administrationNarration=null;narration.Abandon();narration.QueueFree();presenter.SetBusy(false);throw;
        }

        void Submit()
        {
            if(!IsAdministrationCurrent(world,map)||_administrationPresenter!=presenter||draft.Revision!=handover.DraftRevision)return;
            if(!ApplyPlayableOrder(new SetAreaAdministrationOrder(force,city,accepted),CloseAreaAdministration))
            {presenter.SetBusy(false);presenter.RefreshParent();presenter.ApplyNotice(_playMessage.Text);}
        }
    }

    private void AdministrationUnavailable(string reason)
    {
        string message=_ui.Get("detail_unknown");_playMessage.Text=message;
        _administrationPresenter?.ApplyNotice(message);GD.PushWarning(reason);
    }
    private void ReturnFromAdministration(GameState world,MapSceneView map,int city)
    {
        CloseAreaAdministration();
        if(_failed||_shuttingDown||!ReferenceEquals(_world,world)||_residentMap!=map)return;
        SetMapFocus(true);
        if(world.Cities[city].HexId is int hex)SelectMapTarget(hex);
        map.ReopenCityCommandMenu(city);
    }
    private void CloseAreaAdministration()
    {
        if(!HasAdministrationPage&&_administrationMap is null&&_administrationPresenter is null)return;
        var map=_administrationMap;_administrationMap=null;_administrationWorld=null;
        var presenter=_administrationPresenter;var parent=_administrationParent;
        _administrationPresenter=null;_administrationParent=null;
        _administrationHandover=null;_administrationHandoverRevision=-1;
        var narration=_administrationNarration;_administrationNarration=null;
        if(narration is not null){narration.Abandon();narration.QueueFree();}
        presenter?.Dispose();
        if(parent is not null){parent.Abandon();parent.QueueFree();}
        if(map is not null&&GodotObject.IsInstanceValid(map)&&_residentMap==map)
        {map.EndAdministrationMapDisplay();map.SetCommandTargetPicker(null);map.SetDetailsOpen(false);}
        if(!_failed&&!_shuttingDown&&_residentMap is not null)SetMapFocus(_mapFocused);
    }
}
