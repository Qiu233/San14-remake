using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private DeploymentSetupView? _deploymentSetup;
    private DeploymentSetupPresenter? _deploymentSetupPresenter;
    private DeploymentTargetView? _deploymentTarget;
    private DeploymentTargetPresenter? _deploymentTargetPresenter;
    private DeploymentNarrationView? _deploymentNarration;
    private MapSceneView? _deploymentSetupMap;
    private DeploymentCityDraft? _deploymentCityDraft;
    private int? _deploymentDraftCityId;
    private GameplaySession? _deploymentDraftSession;
    private MapSceneView? _deploymentDraftMap;
    private Action<GameState,GameState>? _deploymentAdoptSpeechWorld;
    // One explicit host seed per live Main. Start and Load retain this owner.
    // Simulations capture private snapshots and commit back to this same owner.
    private readonly SharedSourceRandom32 _presentationRandom = new(
        BitConverter.ToUInt32(System.Security.Cryptography.RandomNumberGenerator.GetBytes(sizeof(uint))));
    private bool HasMilitaryOrderPage=>_cityDeploymentCancellation is not null||_transportSetup is not null||_transportTarget is not null||_transportNarration is not null||_unitOrderTarget is not null||_deploymentSetup is not null||_deploymentTarget is not null||_deploymentNarration is not null||HasCampaignCommandPage||HasGeneralUnitTargetPage||HasManualTacticCommands;

    private bool IsDeploymentCurrent(GameState world,MapSceneView map)=>
        !_failed&&!_shuttingDown&&ReferenceEquals(_world,world)&&_residentMap==map&&
        ReferenceEquals(_session,_deploymentDraftSession)&&ReferenceEquals(_session?.World,world)&&_deploymentDraftMap==map;

    private void OpenDeploymentSetup(int cityId, bool fromCommandWorkspace=false)
    {
        if(HasMilitaryOrderPage||HasAdministrationPage||HasCampaignSavePage||HasMapSystemMenu||HasCommandWorkspace&&!fromCommandWorkspace||_detailsView is not null||_repository is null||_world is not { } world||
            _campaignComposition is not { } composition||_residentMap is not { } map||
            _session is not { } session||!ReferenceEquals(session.World,world)||
            _playerForceId is not int force||!world.Cities.TryGetValue(cityId,out var city))return;
        var availability=DeploymentBatchView.Create(world,force,cityId,_selectedCell?.Id);
        if(!availability.CanCompose)return;
        if(fromCommandWorkspace)CloseCommandWorkspace();
        // Only explicit development controls create this legacy in-memory draft.
        // Retain it without resetting; normal drafts use the restored source UI.
        if(DeveloperWorkspaceEnabled&&_deploymentComposition?.HasDraft==true){OpenDeploymentEditor();return;}
        if(_deploymentDraftSession!=session||_deploymentDraftMap!=map||_deploymentCityDraft?.IsFor(world,cityId)!=true)
            _deploymentCityDraft=new(world,cityId);
        _deploymentDraftSession=session;_deploymentDraftMap=map;
        _deploymentDraftCityId=cityId;_deploymentSetupMap=map;
        SetMapFocus(true);map.CloseCityCommandMenu();map.SetDetailsOpen(true,keepMapVisible:true);HideDeploymentHud();
        var view=new DeploymentSetupView();_deploymentSetup=view;AddChild(view);
        try
        {
            view.Configure(_repository,_ui,city,
                availability.AvailableTroops??throw new InvalidDataException("Deployment troop availability is unknown."),
                availability.AvailableBooks,()=>CancelDeploymentMake(world,map,cityId),
                ()=>_deploymentSetupPresenter?.OpenOfficerSelection());
            _deploymentSetupPresenter=new(view,_repository,_ui,world,composition.Snapshot,
                composition.PrepareArmyQueries(world),cityId,rows=>RunPlayAction(()=>
                {
                    if(!IsDeploymentCurrent(world,map)||_deploymentSetup!=view)return;
                    var targets=_deploymentCityDraft!.AcceptMake(rows);
                    CloseDeploymentMakeView();OpenDeploymentTarget(world,map,cityId,force,targets);
                }),DeploymentUnavailable,_sourceHeaders,_deploymentCityDraft.AcceptedRows,CurrentListScrollSpeed);
            _deploymentAdoptSpeechWorld=(previous,replacement)=>
            {
                if(_deploymentSetup!=view||!ReferenceEquals(world,previous))return;
                _deploymentSetupPresenter!.AdoptSpeechWorld(previous,replacement);world=replacement;
            };
            BindCurrentTutorialDeployment();
            view.RegisterHeader(_sourceHeaders);
        }
        catch{CloseDeploymentSetup();throw;}
    }

    private void OpenDeploymentTarget(GameState world,MapSceneView map,int city,int force,IReadOnlyList<DeploymentDraftRow> rows)
    {
        var view=new DeploymentTargetView();_deploymentTarget=view;_deploymentSetupMap=map;AddChild(view);
        try
        {
            _deploymentTargetPresenter=new(view,_repository!,_ui,world,_campaignComposition!.PrepareArmyQueries(world),city,force,rows,
                map.ProjectDeploymentTarget,map.ProjectWaypointMarker,
                accepted=>RunPlayAction(()=>PrepareDeploymentNarration(world,map,city,force,view,accepted)),
                ()=>
                {
                    if(!IsDeploymentCurrent(world,map)||_deploymentTarget!=view)return;
                    _deploymentCityDraft!.ReturnTarget(_deploymentTargetPresenter!.Draft.Rows,
                        _deploymentTargetPresenter.Draft.EditedPolicyOfficers);
                    ReturnToDeploymentCity(world,map,city);
                },DeploymentUnavailable,open=>SetDeploymentModal(map,open),
                saveDefaults:(behavior,accepted)=>
                {
                    bool saved=false;
                    RunPlayAction(()=>
                    {
                        if(!IsDeploymentCurrent(world,map)||_deploymentTarget!=view)return;
                        var expected=world;
                        saved=ApplyPlayableOrder(new SaveDeploymentDefaultsOrder(force,behavior),()=>
                        {
                            var replacement=_session!.World;
                            _deploymentCityDraft!.AcceptPreferenceWorld(expected,replacement);
                            world=replacement;accepted(replacement);
                        });
                        if(saved)HideDeploymentHud();
                    });
                    return saved;
                },editedPolicyOfficers:_deploymentCityDraft!.EditedPolicyOfficers,scrollSpeed:CurrentListScrollSpeed,
                tutorialRestrictsFacilities:_campaignApplication?.TutorialRestrictsFacilities,
                chooseFalseReportDestination:(action,goal,accept)=>
                {
                    if(action is not {ActionKey:"capture",Target.Kind:DeploymentTargetKind.Unit}||!Current())return;
                    if(!OpenFalseReportTarget(world,map,force,action.Target.Id,view,Current,BindTargetPicker,
                        hex=>accept(action with{FalseReportDestinationHexId=hex})))
                        DeploymentUnavailable("deployment.false_report_destination");
                },routePreviewChanged:map.SetDeploymentRoutePreviews);
            _deploymentAdoptSpeechWorld=(previous,replacement)=>
            {
                if(_deploymentTarget!=view||!ReferenceEquals(world,previous))return;
                _deploymentTargetPresenter!.AdoptSpeechWorld(previous,replacement);world=replacement;
            };
            BindCurrentTutorialDeployment();
            view.RegisterHeader(_sourceHeaders);
            map.SetDetailsOpen(false,keepMapVisible:true);HideDeploymentHud();
            BindTargetPicker();
        }
        catch{CloseDeploymentSetup();throw;}

        bool Current()=>IsDeploymentCurrent(world,map)&&_deploymentTarget==view;
        void BindTargetPicker()
        {
            if(!Current()||_deploymentTargetPresenter is not { } presenter)return;
            map.SetCommandTargetPicker(hex=>presenter.PickTarget(hex,map.SourceSelectionShiftHeld),
                presenter.AllowPointer,presenter.HoverTarget,supportsShiftWaypoint:true);
        }
    }

    private void PrepareDeploymentNarration(GameState world,MapSceneView map,int city,int force,
        DeploymentTargetView target,IReadOnlyList<DeploymentDraftRow> accepted)
    {
        if(!IsDeploymentCurrent(world,map)||_deploymentTarget!=target)return;
        var rows=DeploymentCityDraft.CopyRows(accepted);
        // Application presentation random choices are prepared once after Yes.
        // Drawing and advancing retain the expanded message without requerying.
        var preparation=_campaignComposition!.PrepareArmyQueries(world).QueryDeploymentNarration(world,rows,
            _ui.SourceLocale,_presentationRandom.Next);
        if(!preparation.Available){DeploymentUnavailable(preparation.UnresolvedInput!);return;}
        var route=preparation.PortraitRoute??throw new InvalidDataException("Deployment standing portrait is unavailable.");
        var sprites=new LocalizedUiSpriteSource(_repository!);
        var layers=route.LoadSprites(sprites.LoadTexturePackSprite);
        var narration=new DeploymentNarrationView { KeyboardInput = HandleGameplayNarrationKeyboard };_deploymentNarration=narration;AddChild(narration);
        try
        {
            narration.Configure(_repository!,_ui,preparation.SpeakerName!,preparation.Text!,layers,()=>RunPlayAction(()=>
            {
                if(_deploymentNarration!=narration)return;
                if(!IsDeploymentCurrent(world,map)){CloseDeploymentSetup();return;}
                var requests=rows.Select(row=>row.Request with{NavalFormationId=row.NavalFormationId}).ToArray();
                // Visible page consumption is not an order receipt. Only this
                // normal permission/rule transaction creates armies/debits stock.
                if(!ApplyPlayableOrder(new PrepareDeploymentOrder(force,city,requests),CloseDeploymentSetup))
                    ReturnToDeploymentCity(world,map,city);
            }), () => _preferences.AutomaticMessageAdvance,
                () => CanAdvanceNarration&&_deploymentNarration==narration&&IsDeploymentCurrent(world,map));
            narration.BindPreparedVoices(preparation.Voices, _sceneAudioBinding);
            _deploymentCityDraft!.ReturnTarget(rows,_deploymentTargetPresenter!.Draft.EditedPolicyOfficers);
            CloseDeploymentTargetView();map.SetCommandTargetPicker(null);SetDeploymentModal(map,true);
            _deploymentAdoptSpeechWorld=(previous,replacement)=>
            {if(_deploymentNarration==narration&&ReferenceEquals(world,previous))world=replacement;};
        }
        catch
        {
            _deploymentNarration=null;narration.Abandon();narration.QueueFree();throw;
        }
    }

    private void CancelDeploymentMake(GameState world,MapSceneView map,int city)
    {
        if(!IsDeploymentCurrent(world,map)){CloseDeploymentSetup();return;}
        _deploymentCityDraft!.CancelMake();ReturnToDeploymentCity(world,map,city);
    }

    private void ReturnToDeploymentCity(GameState world,MapSceneView map,int city)
    {
        CloseDeploymentPages();
        if(!IsDeploymentCurrent(world,map))return;
        map.SetDetailsOpen(false);SetMapFocus(true);
        if(world.Cities[city].HexId is int hex)SelectMapTarget(hex);
        map.ReopenCityCommandMenu(city);
    }

    private void DeploymentUnavailable(string reason)
    {
        string message=_ui.Get("play_deployment_unavailable");_playMessage.Text=message;
        _deploymentSetup?.ApplyHint(message);_deploymentTarget?.ApplyHint(message);GD.PushWarning(reason);
    }
    private void HideDeploymentHud(){_forceHud?.Hide();_mapFocusToggle.Hide();}
    private void SetDeploymentModal(MapSceneView map,bool open)
    {
        if(_residentMap!=map||_deploymentSetupMap!=map)return;
        map.SetDetailsOpen(open,keepMapVisible:true);if(HasMilitaryOrderPage)HideDeploymentHud();
    }

    private void CloseDeploymentMakeView()
    {
        var presenter=_deploymentSetupPresenter;var view=_deploymentSetup;
        _deploymentSetupPresenter=null;_deploymentSetup=null;presenter?.Dispose();
        if(view is not null){view.Abandon();view.QueueFree();}
    }
    private void CloseDeploymentTargetView()
    {
        var presenter=_deploymentTargetPresenter;var view=_deploymentTarget;
        CloseFalseReportTarget(view);
        _deploymentTargetPresenter=null;_deploymentTarget=null;presenter?.Dispose();
        if(view is not null){view.Hide();view.QueueFree();}
    }
    private void CloseDeploymentPages()
    {
        var map=_deploymentSetupMap;_deploymentSetupMap=null;_deploymentAdoptSpeechWorld=null;
        if(map is not null&&GodotObject.IsInstanceValid(map))map.SetCommandTargetPicker(null);
        var narration=_deploymentNarration;_deploymentNarration=null;
        if(narration is not null){narration.Abandon();narration.QueueFree();}
        CloseDeploymentMakeView();CloseDeploymentTargetView();
    }
    private void ClearDeploymentCityDraft()
    {_deploymentCityDraft=null;_deploymentDraftCityId=null;_deploymentDraftSession=null;_deploymentDraftMap=null;}
    private void CloseDeploymentSetup()
    {
        var map=_deploymentSetupMap;CloseDeploymentPages();ClearDeploymentCityDraft();
        if(map is not null&&GodotObject.IsInstanceValid(map)&&_residentMap==map)map.SetDetailsOpen(false);
        if(!_failed&&!_shuttingDown&&_residentMap is not null)SetMapFocus(_mapFocused);
    }
    private void OpenDeploymentEditor()
    {
        if (!DeveloperWorkspaceEnabled) return;
        RefreshPlayControls();_playCommandDeck.Select(PlayableCommandDeck.Page.Military);
        SetMapFocus(false);_playOfficer.GrabFocus();
    }
}
