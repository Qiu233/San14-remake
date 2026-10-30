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
    private bool _personnelPresentationIntelligenceWait;
    private bool HasPersonnelPresentation=>HasDuelCaptureAnnouncement||HasDuelPresentation||_personnelPresentation is not null||HasEncirclementPresentation||HasFirePresentation||HasDamagePresentation||HasFacilityEffectsPresentation||HasFacilityConstructionPresentation||HasFacilityRepairPresentation||HasMonthlyFeedback||_resourceNotice is not null||_personnelSound is not null||_terminalMusic is not null||_coalitionCameraCommand is not null;
    private static GameState PersonnelPresentationOwner(GameplaySession session)=>session.PendingNormalPeriod?.Candidate??session.World;
    //A presentation receipt changes only its real outbox owner. Keep the
    //same retained Story scene attached to that publication, including manual
    //Events whose owner is World, without reopening its media or advancing it.
    private GameState SynchronizePersonnelPresentationOwner(GameplaySession session)
    {
        var owner = PersonnelPresentationOwner(session);
        if (_storyPresentation is { } scene && ReferenceEquals(scene.Session, session) &&
            StoryPlayback(session)?.Story is { } story && scene.Sequence == story.Sequence &&
            scene.Source == story.Source && scene.ContentRevision == story.ContentRevision)
            scene.World = owner;
        return owner;
    }
    private bool IsPersonnelPresentationCurrent=>HasDuelCaptureAnnouncement?IsDuelCaptureAnnouncementCurrent:HasDuelPresentation?IsDuelPresentationCurrent:HasEncirclementPresentation?IsEncirclementPresentationCurrent:HasFirePresentation?IsFirePresentationCurrent:HasDamagePresentation?IsDamagePresentationCurrent:HasFacilityEffectsPresentation?IsFacilityEffectsPresentationCurrent:HasFacilityRepairPresentation?IsFacilityRepairPresentationCurrent:HasFacilityConstructionPresentation?IsFacilityConstructionPresentationCurrent:_coalitionCameraCommand is not null?IsCoalitionCameraCurrent:_terminalMusic is not null?IsTerminalMusicCurrent:_personnelSound is not null?IsPersonnelSoundCurrent:_resourceNotice is not null?IsResourceNoticeCurrent:HasMonthlyFeedback?IsMonthlyFeedbackCurrent:_personnelPresentation is not null&&
        ReferenceEquals(_session,_personnelPresentationSession)&&_session is {} session&&
        ReferenceEquals(PersonnelPresentationOwner(session),_personnelPresentationWorld)&&_residentMap==_personnelPresentationMap;

    private bool TryOpenPersonnelPresentation()
    {
        if (_session is { } currentSession) SynchronizePersonnelPresentationOwner(currentSession);
        if (_endingPresentation is { } ending)
        {
            if (IsEndingCurrent(ending)) return true;
            CloseEndingPresentation();
        }
        if (HasForcePresentation) return true;
        if(HasPersonnelPresentation)
        {
            if(IsPersonnelPresentationCurrent)return true;
            ClosePersonnelPresentation();
        }
        if(_failed||_shuttingDown||_advancingPeriod||(IsReturningToTitle&&!HasTerminalReturn)||(HasAdministrationPageExceptStory||(HasStoryPresentation&&!IsStoryPresentationCurrent))||HasMilitaryOrderPage||
            HasEnvironmentSettings||HasCampaignSavePage||HasMapSystemMenu||HasCommandWorkspace||HasPersonnelConfirmation||_detailsView is not null||
            _diplomacyNegotiation is not null||_repository is null||_campaignComposition is not {} composition||
            _session is not {PlayerForceId:not null} session||
            (session.HasPendingOperation&&session.PendingNormalPeriod is null&&session.PendingCampaignEventCommand is null&&!HasCreatedEventResults)||
            _residentMap is not {IsPlayableMapReady:true} map)return false;
        var owner=SynchronizePersonnelPresentationOwner(session);
        if(CampaignDuelPresentationPlaybackRules.Pending(owner) is not null&&TryOpenDuelPresentation(session,owner,map,composition))return true;
        if (owner.PendingMapMusicIntent is { } pendingMusic)
            return TryPresentMapMusic(null, pendingMusic, session, owner, map);
        RestoreCoalitionCamera(owner, map);
        RefreshFacilityRepairScene(session,owner,map);
        RefreshDisasterScene(session,owner,map);
        RefreshMonthlyScene(session,owner,map);
        RestoreFoodWarnings(session,owner,map);
        RestoreCityMapAttention(owner,map);
        RefreshArmyMapSpeechBinding(session,owner,map);
        if(ReferenceEquals(_personnelPresentationInspectedOwner,owner))return TryOpenForceExperiencePresentation() || TryResumePersonnelDevelopment() || TryResumeDistrictAdministration() || TryResumeStrategicOrders() || TryOpenEndingPresentation(session, composition, map);
        GameState? armyViewRefreshSource = null;
        while(PersonnelPresentationQueue.Next(owner) is {} entry)
        {
            if (session.PendingEditedEventPlayback is { Phase: EditedEventPlaybackPhase.Results or EditedEventPlaybackPhase.PostResult or EditedEventPlaybackPhase.Summary } &&
                _createdEventWorkspace is { } editedOwner && !EnsureCreatedEventMedia(editedOwner)) return true;
            //A preceding FIFO request can complete synchronously. Check the
            //actual head again on every iteration before opening this notice.
            if (StoryPlayback(session) is { Phase: NormalTurnStoryResultPhase.PostResult,
                    StateControl.AwaitingPresentationBatch: long awaited } && entry.BatchSequence == awaited &&
                !EnsurePostResultSceneBeforePersonnel(session, composition, map, awaited)) return true;
            //A departure or any other intervening intent can change the
            //actual view publication. Only consecutive trait receipts share it.
            if(entry.Request.ArmyTraitViewRefresh is null)armyViewRefreshSource=null;
            if (entry.Source == CampaignEffectSource.ProposalCommands)
                return TryPresentProposalEffects(session, composition, map);
            if (entry.Request.ForceCoalitionVisual is { } coalition)
            {
                if (coalition.Operation is ForceCoalitionVisualOperation.CameraFocus or ForceCoalitionVisualOperation.CameraRestore)
                    return TryPresentCoalitionCamera(entry, coalition, session, owner, map);
                if (!TryPresentCoalitionVisual(entry, coalition, session, owner, map)) return false;
                owner = SynchronizePersonnelPresentationOwner(session);
                continue;
            }
            if (entry.Request.MonthlyMaintenance?.Operation is MonthlyMaintenanceOperation.EmperorSceneEnter or
                MonthlyMaintenanceOperation.EmperorSceneExit)
            {
                if (!TryPresentEmperorScene(entry, session, owner, map)) return false;
                owner = SynchronizePersonnelPresentationOwner(session);
                continue;
            }
            if (entry.Request.MapMusicIntent is { } music)
                return TryPresentMapMusic(entry, music, session, owner, map);
            if (entry.Request.ApplicationExit is { } exit)
            {
                if (!TryPresentApplicationExit(entry, exit, session, owner)) return false;
                if (_returnToTitleTask is { IsCompleted: false }) return true;
                owner = SynchronizePersonnelPresentationOwner(session);
                continue;
            }
            if (entry.Request.ArmyDeparture is { } departure)
            {
                if (!map.TryPresentArmyDeparturePresentation(owner, departure, out var missing))
                {
                    GD.PushWarning(missing);
                    _personnelPresentationInspectedOwner = owner;
                    return false;
                }
                if (!session.TryCompleteArmyDeparturePresentation(owner, entry.BatchSequence, entry.RequestIndex)) return false;
                owner = SynchronizePersonnelPresentationOwner(session);
                continue;
            }
            if (entry.Request.ArmyTraitViewRefresh is { } refresh)
            {
                //Consecutive view requests read one actual domain publication.
                //Their receipts only remove outbox entries, not Army state.
                armyViewRefreshSource ??= owner;
                if (!map.TryRefreshArmyTraitView(armyViewRefreshSource, refresh.ArmyId, out var missing))
                {
                    GD.PushWarning(missing);
                    _personnelPresentationInspectedOwner = owner;
                    return false;
                }
                if (!session.TryCompleteArmyTraitViewRefresh(owner, entry.Source, entry.BatchSequence, entry.RequestIndex)) return false;
                owner = SynchronizePersonnelPresentationOwner(session);
                continue;
            }
            if (entry.Source == CampaignEffectSource.ArmyCommandMaintenance &&
                entry.Request.Speech?.Event == CombatSpeechEvent.ArmyAreaOccupationCompleted)
            {
                // Older builds misclassified the chronology notification as
                // speech. Its original labels and date were never retained.
                // Preserve the packet instead of inventing a later history.
                GD.PushWarning("legacy.army_area_occupation_chronicle.source_labels_and_date_missing");
                _personnelPresentationInspectedOwner = owner;
                return false;
            }
            if (entry.Request.Speech is { Event: CombatSpeechEvent.ArmyOrderFinished or CombatSpeechEvent.ArmyHoldingPosition or
                CombatSpeechEvent.ArmyDestinationReachedHolding or CombatSpeechEvent.ArmyEnteredBase })
            {
                if (!session.TryPrepareArmyCommandSpeech(owner, entry.Source, entry.BatchSequence, entry.RequestIndex,
                    composition.PrepareArmyQueries(owner), out var prepared, new(_presentationRandom.State)))
                {
                    if (prepared?.UnresolvedOwner is { } missing) GD.PushWarning(missing);
                    _personnelPresentationInspectedOwner = owner;
                    return false;
                }
                owner = SynchronizePersonnelPresentationOwner(session);
                if (session.PendingNormalPeriod is null && owner.CampaignAppRandom32 is { } random)
                    _presentationRandom.InstallCommittedState(random.State);
                RefreshArmyMapSpeechBinding(session, owner, map);
                continue;
            }
            if (entry.Request.ArmyFireCast is not null)
                return TryPresentFire(entry, session, owner, map);
            if (entry.Request.ArmyFireAnnouncement is not null)
            {
                if (!session.TryPrepareFireAnnouncement(owner, entry.Source, entry.BatchSequence,
                    entry.RequestIndex, entry.Request, composition.PrepareArmyQueries(owner),
                    out var prepared, new(_presentationRandom.State)))
                {
                    if (prepared?.UnresolvedOwner is { } missing) GD.PushWarning(missing);
                    _personnelPresentationInspectedOwner = owner;
                    return false;
                }
                owner = SynchronizePersonnelPresentationOwner(session);
                if (session.PendingNormalPeriod is null && owner.CampaignAppRandom32 is { } random)
                    _presentationRandom.InstallCommittedState(random.State);
                RefreshArmyMapSpeechBinding(session, owner, map);
                continue;
            }
            if (entry.Request.FireDamagePresentation is not null || entry.Request.TerrainDamagePresentation is not null)
                return TryPresentDamage(entry, session, owner, map);
            if(entry.Request.FacilityAuraReset is not null || entry.Request.MilitaryMusicPulse is not null ||
                entry.Request.FacilityProjectile is not null || entry.Request.FacilityStatisticPresentation is not null ||
                entry.Request.FacilitySupportAura is not null || entry.Request.ArmyTrapPresentation is not null ||
                entry.Request.FacilityTrapPresentation is not null || entry.Request.ArmyFacilityDamagePresentation is not null)
                return TryPresentFacilityEffects(entry,session,owner,map);
            if(entry.Request.FacilityConstructionPresentation is not null)
                return TryPresentFacilityConstruction(entry,session,owner,map);
            if(entry.Request.FacilityRepairPresentation is not null)
                return TryPresentFacilityRepair(entry,session,owner,map);
            if(entry.Request.Speech is { Event: CombatSpeechEvent.ArmyFacilityConstructionStarted or CombatSpeechEvent.ArmyFacilityCompleted or
                CombatSpeechEvent.ArmyUnderFacilityAttack or CombatSpeechEvent.TrapPitfall or CombatSpeechEvent.TrapRockfall or
                CombatSpeechEvent.TrapCountered or CombatSpeechEvent.ConfusionApplied or CombatSpeechEvent.ImmobilizationApplied })
            {
                if(!session.TryPrepareFacilityArmySpeech(owner,entry.Source,entry.BatchSequence,entry.RequestIndex,
                    composition.PrepareArmyQueries(owner),out var prepared,new(_presentationRandom.State)))
                {
                    if(prepared?.UnresolvedOwner is {} missing)GD.PushWarning(missing);
                    _personnelPresentationInspectedOwner=owner;
                    return false;
                }
                owner=SynchronizePersonnelPresentationOwner(session);
                if(session.PendingNormalPeriod is null&&owner.CampaignAppRandom32 is {} random)
                    _presentationRandom.InstallCommittedState(random.State);
                RefreshArmyMapSpeechBinding(session,owner,map);
                continue;
            }
            if (entry.Request.BaseAbnormalFeedback is not null)
            {
                if (!session.TryPrepareBaseAbnormalFeedback(owner, entry.Source, entry.BatchSequence,
                    entry.RequestIndex, composition.PrepareArmyQueries(owner), out var prepared,
                    new(_presentationRandom.State)))
                {
                    if (prepared?.UnresolvedOwner is { } missing) GD.PushWarning(missing);
                    _personnelPresentationInspectedOwner = owner;
                    return false;
                }
                owner = SynchronizePersonnelPresentationOwner(session);
                RefreshArmyMapSpeechBinding(session, owner, map);
                continue;
            }
            if (entry.Request.DuelCaptureAnnouncement is { } capture)
                return TryPresentDuelCaptureAnnouncement(entry, capture, session, owner, map, composition);
            if (entry.Request.PersonDeathReport is { } death)
            {
                var publication = composition.PrepareArmyQueries(owner).QueryPersonDeathPresentation(owner, death);
                var presentation = publication.Value ?? throw new InvalidDataException(publication.UnresolvedInput);
                if (presentation.UnresolvedOwner is { } missing)
                { GD.PushWarning(missing); _personnelPresentationInspectedOwner = owner; return false; }
                if (death.Operation is PersonDeathReportOperation.EventEnter or PersonDeathReportOperation.EventExit)
                {
                    if (presentation.Illustration is { } illustration) ShowMonthlyScene(session, map, illustration);
                    else CloseMonthlyScene();
                    if (!session.TryCompletePersonnelPresentation(owner, entry.BatchSequence, entry.RequestIndex)) return false;
                    owner = SynchronizePersonnelPresentationOwner(session);
                    continue;
                }
                if (presentation.SoundId is int soundId)
                    return TryPresentPersonnelSound(entry, session, owner, map, soundId);
            }
            if (entry.Request.MonthlyMaintenance?.Operation == MonthlyMaintenanceOperation.ResetPersonnelGroupMoveDestinations)
            {
                composition.ApplicationGameplay.PersonnelGroupMoveDestinations.Clear();
                if (!session.TryCompletePersonnelPresentation(owner,entry.BatchSequence,entry.RequestIndex)) return false;
                owner=SynchronizePersonnelPresentationOwner(session);
                continue;
            }
            if (entry.Request.NormalTurnFoodWarningPresentation is not null)
            {
                if (!TryPresentFoodWarnings(entry,session,owner,map)) return false;
                owner=SynchronizePersonnelPresentationOwner(session);
                continue;
            }
            if (entry.Request.MonthlyMaintenance?.Operation is MonthlyMaintenanceOperation.FinalOrderSceneEnter or
                MonthlyMaintenanceOperation.FinalOrderSceneExit)
            {
                if (!TryPresentFinalOrderScene(entry,session,owner,map)) return false;
                owner=SynchronizePersonnelPresentationOwner(session);
                continue;
            }
            if (entry.Request.NormalTurnForceResourceNotice is not null)
                return TryPresentResourceNotice(entry,session,owner,map);
            if(entry.Request.MonthlyMaintenance?.Operation is MonthlyMaintenanceOperation.DisasterSceneEnter or
                MonthlyMaintenanceOperation.DisasterSceneExit)
            {
                if(!TryPresentDisasterScene(entry,session,owner,map))return false;
                owner=SynchronizePersonnelPresentationOwner(session);
                continue;
            }
            if(entry.Request.MonthlyMaintenance?.Operation==MonthlyMaintenanceOperation.TerritoryOccupation)
                return TryPresentMonthlyTerritory(entry,session,owner,map);
            if(entry.Request.MapObjectPresentation?.BaseCapture is not null)
                return TryPresentMonthlyTerritory(entry,session,owner,map);
            if(MapObjectPresentationSources.Supports(entry.Source,entry.Request.MapObjectPresentation))
                return TryPresentMonthlyTerritory(entry,session,owner,map);
            var payload=PersonnelPresentationQueue.Message(entry);
            var growth=entry.Request.OfficerGrowthFeedback;
            var scenery=entry.Request.TalentSceneryReward;
            var voice=payload?.Voice??scenery?.Voice;
            if(growth?.Operation==OfficerGrowthFeedbackOperation.SoundCue)
            {
                _sceneAudioBinding?.PlayOfficerGrowthFeedbackSound();
                if(!session.TryCompletePersonnelPresentation(owner,entry.BatchSequence,entry.RequestIndex))return false;
                owner=SynchronizePersonnelPresentationOwner(session);
                continue;
            }
            if (payload is { Operation: TalentCommandPresentationOperation.SoundCue, Voice: null } sourceSound)
            {
                // An ordinary SE index names a cue, not a sample or a dedicated
                // personnel channel. Queue it through the shared normal manager
                // and retain this request until actual admission completes.
                return TryPresentPersonnelSound(entry, session, owner, map, sourceSound.SoundId ??
                    throw new InvalidDataException("personnel.presentation.sound_index"));
            }
            if(entry.Request.DiplomacyArrivalAudio is not null||payload?.Operation==TalentCommandPresentationOperation.SoundCue)
            {
                bool audioRequired=entry.Source is CampaignEffectSource.MonthlyMaintenance or CampaignEffectSource.StrategicCommands or
                    CampaignEffectSource.ArmyBattleDefeat;
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
                owner=SynchronizePersonnelPresentationOwner(session);
                continue;
            }
            if(payload is null&&growth?.Narration is null&&scenery?.DiscoveryTextAtSource is null)break;
            RetirePeriodReportView();CloseActionReport();
            var view=new DeploymentNarrationView { KeyboardInput = HandleGameplayNarrationKeyboard };_personnelPresentation=view;
            _personnelPresentationSession=session;_personnelPresentationWorld=owner;_personnelPresentationMap=map;
            _personnelPresentationIntelligenceWait = entry.Source == CampaignEffectSource.IntelligenceCommands &&
                IntelligenceNarrationContinuation(owner)?.NarrationBatchSequence == entry.BatchSequence;
            AddChild(view);
            try
            {
                if(scenery is not null)
                {
                    var projected=composition.PrepareArmyQueries(owner).QuerySceneryDiscoveryPanel(owner,scenery);
                    var panel=projected.Available?projected.Value!:
                        throw new InvalidDataException(projected.UnresolvedInput??"Scenery discovery panel is unavailable.");
                    IReadOnlyList<LoadedSprite> layers=[];
                    if(panel.Executor.StandingPortrait is {} portrait)
                    {
                        var sprites=new LocalizedUiSpriteSource(_repository);
                        layers=portrait.LoadSprites(sprites.LoadTexturePackSprite);
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

                        IReadOnlyList<LoadedSprite> StandingLayers(PersonnelPresentationSpeaker? actor)
                        {
                            if(actor?.StandingPortrait is {} standing)
                                return standing.LoadSprites(sprites.LoadTexturePackSprite);
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
                        bool standingSpeaker=monthlySpeaker||entry.Source is CampaignEffectSource.PersonDeath or CampaignEffectSource.DoctrineResearchCommands||
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
                            var layers=standing.LoadSprites(sprites.LoadTexturePackSprite);
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
                view.Name="PersonnelPresentation";
                view.ZIndex=StoryPlayback(session)?.Phase==NormalTurnStoryResultPhase.PostResult || HasCreatedEventResults?97:95;
                map.CloseCityCommandMenu();map.SetDetailsOpen(true,keepMapVisible:true);SetMapFocus(false);HideDeploymentHud();
                return true;
            }
            catch{ClosePersonnelPresentation();throw;}

            bool Admitted()=>CanAdvanceNarration&&_personnelPresentation==view&&IsPersonnelPresentationCurrent;
            void Completed()=>RunUiAction(()=>
            {
                if(_personnelPresentation!=view)return;
                if(!IsPersonnelPresentationCurrent){ClosePersonnelPresentation();return;}
                //A real map-speech commit may have replaced this same view's
                //owner. Capture its adopted owner before closing the view.
                var receiptOwner = _personnelPresentationIntelligenceWait ? _personnelPresentationWorld! : owner;
                ClosePersonnelPresentation();
                bool intelligenceWait = entry.Source == CampaignEffectSource.IntelligenceCommands &&
                    IntelligenceNarrationContinuation(receiptOwner) is
                        { Phase: PersonnelIntelligenceNarrationPhase.AwaitingNarration } wait && wait.NarrationBatchSequence == entry.BatchSequence;
                bool consumed = intelligenceWait
                    ? session.TryCompleteIntelligenceNarration(receiptOwner, entry.BatchSequence, entry.RequestIndex, new(_presentationRandom.State))
                    : session.TryCompletePersonnelPresentation(owner, entry.BatchSequence, entry.RequestIndex);
                if(!consumed)return;
                //Only the effect outbox changed. Keep the resident map resources;
                //the next domain publication refreshes them normally.
                if(!TryOpenPersonnelPresentation())
                {RefreshPlayControls();RefreshDiplomaticNegotiation();}
            });
        }
        if(owner.RegionOccupationFollowups?.EncirclementPresentations.Count>0)
            return TryPresentEncirclement(session,owner,map,composition);
        _personnelPresentationInspectedOwner=owner;
        return TryOpenForceExperiencePresentation() || TryResumePersonnelDevelopment() || TryResumeDistrictAdministration() || TryResumeStrategicOrders() || TryOpenEndingPresentation(session, composition, map);
    }

    private void ClosePersonnelPresentation()
    {
        CloseDuelCaptureAnnouncement();
        CloseDuelPresentation();
        CloseFirePresentation();
        CloseDamagePresentation();
        CloseFacilityEffectsPresentation();
        CloseFacilityRepairPresentation();
        CloseFacilityConstructionPresentation();
        CloseTerminalMusic();
        CloseCoalitionCameraCommand();
        ClosePersonnelSound();
        CloseResourceNotice();
        CloseMonthlyFeedback();
        CloseEncirclementPresentation();
        if(_personnelPresentationOwnsVoice)_sceneAudioBinding?.StopNarrationVoice();
        _personnelPresentationOwnsVoice=false;
        _personnelPresentationIntelligenceWait=false;
        var view=_personnelPresentation;var map=_personnelPresentationMap;
        _personnelPresentation=null;_personnelPresentationSession=null;_personnelPresentationWorld=null;_personnelPresentationMap=null;
        if(view is not null){view.Abandon();view.GetParent()?.RemoveChild(view);view.QueueFree();}
        if(map is not null&&GodotObject.IsInstanceValid(map)&&_residentMap==map)
        {map.SetDetailsOpen(false);if(!_failed&&!_shuttingDown)SetMapFocus(_mapFocused);}
    }
}
