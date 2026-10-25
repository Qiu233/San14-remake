using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private DeploymentNarrationView? _personnelPresentation;
    private GameplaySession? _personnelPresentationSession;
    private GameState? _personnelPresentationWorld;
    private MapSceneView? _personnelPresentationMap;
    private GameState? _personnelPresentationInspectedOwner;
    private bool _personnelPresentationOwnsVoice;
    private bool HasPersonnelPresentation=>_personnelPresentation is not null||HasMonthlyFeedback;
    private static GameState PersonnelPresentationOwner(GameplaySession session)=>session.PendingNormalPeriod?.Candidate??session.World;
    private bool IsPersonnelPresentationCurrent=>HasMonthlyFeedback?IsMonthlyFeedbackCurrent:_personnelPresentation is not null&&
        ReferenceEquals(_session,_personnelPresentationSession)&&_session is {} session&&
        ReferenceEquals(PersonnelPresentationOwner(session),_personnelPresentationWorld)&&_residentMap==_personnelPresentationMap;

    private bool TryOpenPersonnelPresentation()
    {
        if (HasForcePresentation) return true;
        if(HasPersonnelPresentation)
        {
            if(IsPersonnelPresentationCurrent)return true;
            ClosePersonnelPresentation();
        }
        if(_failed||_shuttingDown||_advancingPeriod||IsReturningToTitle||HasAdministrationPage||HasMilitaryOrderPage||
            HasEnvironmentSettings||HasCampaignSavePage||HasMapSystemMenu||HasCommandWorkspace||HasPersonnelConfirmation||_detailsView is not null||
            _diplomacyNegotiation is not null||_repository is null||_campaignComposition is not {} composition||
            _session is not {PlayerForceId:not null} session||
            (session.HasPendingOperation&&session.PendingNormalPeriod is null)||
            _residentMap is not {IsPlayableMapReady:true} map)return false;
        var owner=PersonnelPresentationOwner(session);
        RefreshDisasterScene(session,owner,map);
        RefreshArmyMapSpeechBinding(session,owner,map);
        if(ReferenceEquals(_personnelPresentationInspectedOwner,owner))return TryOpenForceExperiencePresentation() || TryResumePersonnelDevelopment() || TryResumeDistrictAdministration() || TryResumeStrategicOrders();
        while(PersonnelPresentationQueue.Next(owner) is {} entry)
        {
            if(entry.Request.MonthlyMaintenance?.Operation is MonthlyMaintenanceOperation.DisasterSceneEnter or
                MonthlyMaintenanceOperation.DisasterSceneExit)
            {
                if(!TryPresentDisasterScene(entry,session,owner,map))return false;
                owner=PersonnelPresentationOwner(session);
                continue;
            }
            if(entry.Request.MonthlyMaintenance?.Operation==MonthlyMaintenanceOperation.TerritoryOccupation)
                return TryPresentMonthlyTerritory(entry,session,owner,map);
            var payload=PersonnelPresentationQueue.Message(entry);
            var growth=entry.Request.OfficerGrowthFeedback;
            var scenery=entry.Request.TalentSceneryReward;
            var voice=payload?.Voice??scenery?.Voice;
            if(growth?.Operation==OfficerGrowthFeedbackOperation.SoundCue)
            {
                _sceneAudioBinding?.PlayOfficerGrowthFeedbackSound();
                if(!session.TryCompletePersonnelPresentation(owner,entry.BatchSequence,entry.RequestIndex))return false;
                owner=PersonnelPresentationOwner(session);
                continue;
            }
            if(entry.Request.DiplomacyArrivalAudio is not null||payload?.Operation==TalentCommandPresentationOperation.SoundCue)
            {
                bool audioRequired=entry.Source is CampaignEffectSource.MonthlyMaintenance or CampaignEffectSource.StrategicCommands;
                //Native SE/arrival calls enqueue audio and continue immediately.
                //A request receipt does not pretend that audio has finished.
                if(entry.Request.DiplomacyArrivalAudio is null||_preferences.Voice.Enabled)
                {
                    if(_sceneAudioBinding is {} audio)
                    {
                        bool queued=audio.PlayPersonnelPresentationAudio(payload,entry.Request.DiplomacyArrivalAudio,_preferences.Voice.Language);
                        if(!queued&&audioRequired)
                            throw new InvalidDataException("Source presentation sound could not be queued: "+entry.Source);
                    }
                    else if(audioRequired)
                        throw new InvalidOperationException("Source presentation needs the active scene audio owner: "+entry.Source);
                    else GD.PushWarning("Personnel presentation audio is unavailable in this scene.");
                }
                if(!session.TryCompletePersonnelPresentation(owner,entry.BatchSequence,entry.RequestIndex))return false;
                owner=PersonnelPresentationOwner(session);
                continue;
            }
            if(payload is null&&growth?.Narration is null&&scenery?.DiscoveryTextAtSource is null)break;
            RetirePeriodReportView();CloseActionReport();
            var view=new DeploymentNarrationView { KeyboardInput = HandleGameplayNarrationKeyboard };_personnelPresentation=view;
            _personnelPresentationSession=session;_personnelPresentationWorld=owner;_personnelPresentationMap=map;
            AddChild(view);
            try
            {
                if(scenery is not null)
                {
                    var projected=composition.PrepareArmyQueries(owner).QuerySceneryDiscoveryPanel(owner,scenery);
                    var panel=projected.Available?projected.Value!:
                        throw new InvalidDataException(projected.UnresolvedInput??"Scenery discovery panel is unavailable.");
                    LoadedSprite[] layers=[];
                    if(panel.Executor.StandingPortrait is {} portrait)
                    {
                        var sprites=new LocalizedUiSpriteSource(_repository);
                        layers=new[]{portrait.SpriteB,portrait.SpriteA,portrait.SpriteC}
                            .Select(name=>sprites.LoadTexturePackSprite(portrait.ResourceName,name)).ToArray();
                    }
                    else
                    {
                        var reason=panel.Executor.PortraitUnresolvedInput??"Scenery executor portrait is unavailable.";
                        GD.PushWarning(reason);view.SetMeta("portrait_unavailable",reason);
                    }
                    view.ConfigureSceneryDiscovery(_repository,_ui,panel,scenery.DiscoveryTextAtSource!,layers,
                        Completed,()=>_preferences.AutomaticMessageAdvance,Admitted);
                }
                else if(growth?.Narration is {} growthText)
                    view.ConfigureAnnouncement(_repository,_ui,growthText.Text,Completed,
                        ()=>_preferences.AutomaticMessageAdvance,Admitted);
                else if(payload!.Operation==TalentCommandPresentationOperation.Announcement)
                    view.ConfigureAnnouncement(_repository,_ui,payload.TextAtSource!,Completed,
                        ()=>_preferences.AutomaticMessageAdvance,Admitted);
                else
                {
                    var queries=composition.PrepareArmyQueries(owner);
                    if(payload.RecruitConversation is {} conversation)
                    {
                        var actors=queries.QueryRecruitConversationSpeakers(owner,conversation);
                        bool executorSpeaking=payload.OfficerId==conversation.ExecutorOfficerId;
                        var sprites=new LocalizedUiSpriteSource(_repository);
                        var executor=actors.Value?.Executor;var target=actors.Value?.Target;
                        var executorLayers=StandingLayers(executor);var targetLayers=StandingLayers(target);
                        string speaker=executorSpeaking?conversation.ExecutorNameAtSource:conversation.TargetNameAtSource;
                        if(entry.Source==CampaignEffectSource.PersonnelAdministrationCommands)
                            view.Configure(_repository,_ui,speaker,payload.TextAtSource!,
                                executorSpeaking?executorLayers:targetLayers,Completed,
                                ()=>_preferences.AutomaticMessageAdvance,Admitted);
                        else view.ConfigureRecruitConversation(_repository,_ui,speaker,payload.TextAtSource!,
                            executorLayers,targetLayers,executorSpeaking,Completed,
                            ()=>_preferences.AutomaticMessageAdvance,Admitted);
                        if(!actors.Available)GD.PushWarning(actors.UnresolvedInput);

                        LoadedSprite[] StandingLayers(PersonnelPresentationSpeaker? actor)
                        {
                            if(actor?.StandingPortrait is {} standing)
                                return new[]{standing.SpriteB,standing.SpriteA,standing.SpriteC}
                                    .Select(name=>sprites.LoadTexturePackSprite(standing.ResourceName,name)).ToArray();
                            string reason=actor?.PortraitUnresolvedInput??actors.UnresolvedInput??"Recruit conversation portrait is unavailable.";
                            GD.PushWarning(reason);view.SetMeta("portrait_unavailable",reason);
                            return [];
                        }
                    }
                    else
                    {
                        var query=payload.Speaker is { } sourceSpeaker
                            ? queries.QueryPersonnelPresentationSpeaker(owner,sourceSpeaker)
                            : queries.QueryPersonnelPresentationSpeaker(owner,payload.OfficerId!.Value);
                        var speaker=query.Value;
                        var sprites=new LocalizedUiSpriteSource(_repository);
                        bool monthlySpeaker=entry.Request.MonthlyMaintenance?.Operation==MonthlyMaintenanceOperation.ConditionalSpeakerText;
                        bool standingSpeaker=monthlySpeaker||entry.Source==CampaignEffectSource.DoctrineResearchCommands||
                            entry.Source==CampaignEffectSource.StrategicCommands&&payload.Operation==TalentCommandPresentationOperation.OfficerText;
                        if(payload.Operation==TalentCommandPresentationOperation.ConditionalOfficerText)
                        {
                            LoadedSprite? portrait=null;
                            if(speaker?.Face2Portrait is {Kind:OfficerPortraitRouteKind.StockNamed,ResourceName:not null,SpriteName:not null} smallFace)
                                portrait=sprites.LoadTexturePackSprite(smallFace.ResourceName,smallFace.SpriteName);
                            else GD.PushWarning(speaker?.PortraitUnresolvedInput??query.UnresolvedInput??"Strategic subtitle portrait is unavailable.");
                            view.ConfigurePersonMessage(_repository,_ui,speaker?.Name??payload.Speaker?.NameAtSource??"",payload.TextAtSource!,
                                portrait,Completed,()=>_preferences.AutomaticMessageAdvance,Admitted);
                        }
                        else if(!standingSpeaker&&speaker?.Face2Portrait is {Kind:OfficerPortraitRouteKind.StockNamed,ResourceName:not null,SpriteName:not null} face)
                            view.ConfigurePersonMessage(_repository,_ui,speaker.Name,payload.TextAtSource!,
                                sprites.LoadTexturePackSprite(face.ResourceName!,face.SpriteName!),Completed,
                                ()=>_preferences.AutomaticMessageAdvance,Admitted);
                        else if(speaker?.StandingPortrait is {} standing)
                        {
                            var layers=new[]{standing.SpriteB,standing.SpriteA,standing.SpriteC}
                                .Select(name=>sprites.LoadTexturePackSprite(standing.ResourceName,name)).ToArray();
                            view.Configure(_repository,_ui,speaker.Name,payload.TextAtSource!,layers,Completed,
                                ()=>_preferences.AutomaticMessageAdvance,Admitted);
                        }
                        else
                        {
                            string reason=speaker?.PortraitUnresolvedInput??query.UnresolvedInput??"Personnel message portrait is unavailable.";
                            GD.PushWarning(reason);view.SetMeta("portrait_unavailable",reason);
                            //Keep the source message and layout usable while the
                            //unimplemented animated/legacy appearance stays absent.
                            if(!standingSpeaker&&payload.OfficerId==5012)
                                view.ConfigurePersonMessage(_repository,_ui,speaker?.Name??"",payload.TextAtSource!,null,
                                    Completed,()=>_preferences.AutomaticMessageAdvance,Admitted);
                            else view.Configure(_repository,_ui,speaker?.Name??"",payload.TextAtSource!,[],Completed,
                                ()=>_preferences.AutomaticMessageAdvance,Admitted);
                        }
                    }
                }
                if(voice is not null&&_preferences.Voice.Enabled)
                {
                    _sceneAudioBinding?.PlayPersonnelNarrationVoice(voice);
                    _personnelPresentationOwnsVoice=true;
                }
                view.Name="PersonnelPresentation";view.ZIndex=95;
                map.CloseCityCommandMenu();map.SetDetailsOpen(true,keepMapVisible:true);SetMapFocus(false);HideDeploymentHud();
                return true;
            }
            catch{ClosePersonnelPresentation();throw;}

            bool Admitted()=>CanAdvanceNarration&&_personnelPresentation==view&&IsPersonnelPresentationCurrent;
            void Completed()=>RunUiAction(()=>
            {
                if(_personnelPresentation!=view)return;
                if(!IsPersonnelPresentationCurrent){ClosePersonnelPresentation();return;}
                ClosePersonnelPresentation();
                if(!session.TryCompletePersonnelPresentation(owner,entry.BatchSequence,entry.RequestIndex))return;
                //Only the effect outbox changed. Keep the resident map resources;
                //the next domain publication refreshes them normally.
                if(!TryOpenPersonnelPresentation())
                {RefreshPlayControls();RefreshDiplomaticNegotiation();}
            });
        }
        _personnelPresentationInspectedOwner=owner;
        return TryOpenForceExperiencePresentation() || TryResumePersonnelDevelopment() || TryResumeDistrictAdministration() || TryResumeStrategicOrders();
    }

    private void ClosePersonnelPresentation()
    {
        CloseMonthlyFeedback();
        if(_personnelPresentationOwnsVoice)_sceneAudioBinding?.StopNarrationVoice();
        _personnelPresentationOwnsVoice=false;
        var view=_personnelPresentation;var map=_personnelPresentationMap;
        _personnelPresentation=null;_personnelPresentationSession=null;_personnelPresentationWorld=null;_personnelPresentationMap=null;
        if(view is not null){view.Abandon();view.GetParent()?.RemoveChild(view);view.QueueFree();}
        if(map is not null&&GodotObject.IsInstanceValid(map)&&_residentMap==map)
        {map.SetDetailsOpen(false);if(!_failed&&!_shuttingDown)SetMapFocus(_mapFocused);}
    }
}
