using Godot;
using San14.Core;
using San14.Client.Gameplay;

public partial class Main
{
    private bool _advancingPeriod;
    private bool _periodReplyOnly;
    private Task? _periodAdvanceTask;
    private CancellationTokenSource? _periodAdvanceCancellation;
    private Control? _periodAdvanceOverlay;
    private CampaignPeriodPlayback? _periodPlayback;
    private MapSceneView? _periodPlaybackMap;
    private int _periodPlaybackSpeed = 1;
    private GameplaySession? _periodProgressSession;
    private CampaignProgressFrameAdmission? _periodProgressAdmission;
    private void ActivateForceHudAdvance()
    {
        if(_advancingPeriod)TryTogglePeriodPlayback();
        else RunPlayAction(AdvancePlayable);
    }

    private bool TryTogglePeriodPlayback()
    {
        if (!_advancingPeriod || _periodReplyOnly || _periodPlayback is not {} playback || _shuttingDown) return false;
        playback.TogglePause();
        if (GodotObject.IsInstanceValid(_periodPlaybackMap))
            _periodPlaybackMap!.SetPeriodPlaybackPaused(playback, playback.IsPaused || playback.ManualCommandSelectionOpen);
        RefreshPeriodPlaybackControls(); return true;
    }

    private void PausePeriodPlaybackForRulerFocus(MapSceneView map)
    {
        if (!_advancingPeriod || _periodReplyOnly || _periodPlayback is not { } playback ||
            !ReferenceEquals(_periodPlaybackMap, map) || _shuttingDown) return;
        // This source command sets the pause bit, even when the camera
        // is already busy. Camera completion does not resume simulation.
        playback.SetPaused(true);
        map.SetPeriodPlaybackPaused(playback, true);
        RefreshPeriodPlaybackControls();
    }

    private bool TrySetPeriodPlaybackSpeed(int speedIndex)
    {
        if (_shuttingDown || _failed || (uint)speedIndex > 2 ||
            _session?.PlayerForceId is null || _residentMap is not {IsPlayableMapReady:true}) return false;
        _periodPlaybackSpeed = speedIndex;
        _periodPlayback?.SetSpeed(speedIndex); RefreshPeriodPlaybackControls(); return true;
    }

    private void RefreshPeriodPlaybackControls()
    {
        if(_forceHud is{} hud&&GodotObject.IsInstanceValid(hud))
            hud.ApplyExecution(_advancingPeriod&&!_periodReplyOnly&&_periodPlayback is not null,_periodPlayback?.IsPaused==true,
                _periodPlayback?.Speed??_periodPlaybackSpeed,
                _periodReplyOnly&&_session is{} owner?PersonnelPresentationOwner(owner).NativeMapSeason?.ThirdDate:
                    _periodPlayback is{} period?period.Candidate?.NativeMapSeason?.ThirdDate:_world?.NativeMapSeason?.ThirdDate);
    }

    private void PublishPeriodPlayback(CampaignPeriodPlayback owner, MapSceneView? map, GameState candidate)
    {
        if (!ReferenceEquals(_periodPlayback, owner) || _shuttingDown || _failed || !IsInsideTree()) return;
        if (GodotObject.IsInstanceValid(map)) map!.PublishPeriodPlayback(owner, candidate);
        if (TryBeginPeriodMenuBorrow(owner, candidate)) return;
        if (TryBeginCampaignHelpBorrow(owner, candidate)) return;
        if (BeginPeriodMapMusic(owner, map, candidate)) return;
        var refreshed = PresentPeriodArmyViews(owner, map, candidate);
        if (!ReferenceEquals(refreshed, candidate))
        {
            candidate = refreshed;
            if (GodotObject.IsInstanceValid(map)) map!.PublishPeriodPlayback(owner, candidate);
        }
        BeginPeriodOccupationPresentation(owner,map,candidate);
        BeginPeriodDuelPresentation(owner,map,candidate);
        BeginPeriodDuelCaptureAnnouncement(owner,map,candidate);
        RefreshManualTacticCommands(candidate);
        RefreshPeriodPlaybackControls();
        RefreshCommanderCards(candidate);
        FlushCommanderCardRequests(owner);
    }

    private async Task AdvancePlayableAsync(GameplaySession source, CampaignComposition composition,
        string? automaticSaveNotice = null, GameOrder? campaignChoice = null)
    {
        var cancellation = new CancellationTokenSource();
        _periodAdvanceCancellation = cancellation;
        bool replyOnly=source.PendingNormalPeriod is null&&
            (CampaignComposition.IsManualRulerReply(source, campaignChoice) ||
                (source.PendingArmyProgressCandidate is not null && campaignChoice is not null) ||
                campaignChoice is ConfirmArmyReturnCaptiveBatchOrder or ConfirmArmyReturnCaptiveExecutionOrder or CompleteArmyReturnCaptivePresentationOrder ||
                campaignChoice is ConfirmRulerSuccessionOrder&&StoryOwner(source).ArmyReturnCaptiveContinuation is not null);
        _periodReplyOnly=replyOnly;
        MapSceneView? playbackMap = _residentMap;
        CampaignPeriodPlayback? playback = null;
        Control? overlay = null;
        GameplaySession? adopted = null;
        bool continuingStory = _storyPresentation is { } storyBinding &&
            ReferenceEquals(storyBinding.Session,source) && IsStoryBetweenExecutionSlices(storyBinding) ||
            source.PendingNormalPeriod?.Candidate.NativeNormalTurnContinuation?.StoryPlayback is { IsReturningCamera: false };
        Exception? applicationServiceFailure = null;
        try
        {
            RetireReportMapMusic();
            if (!replyOnly && (campaignChoice is null && source.PendingNormalPeriod is null ||
                !HasPeriodReportMusicScene(source, playbackMap)))
                await CapturePeriodReportMusicScene(source, playbackMap, cancellation.Token);
            Action<GameState>? beginTutorial = null;
            Func<GameState, (GameState State, bool Hold)>? updateTutorial = null;
            Func<GameState, GameState>? endTutorial = null;
            if (HasTutorialLesson)
            {
                var tutorialMap = playbackMap ?? throw new InvalidOperationException("tutorial.period.map_owner");
                long observedUpdate = 0;
                beginTutorial = candidate =>
                {
                    observedUpdate = tutorialMap.SourceAnimationUpdateSequence;
                    BeginTutorialPeriodBorrow(candidate);
                };
                updateTutorial = candidate =>
                {
                    long currentUpdate = tutorialMap.SourceAnimationUpdateSequence;
                    bool pulse = currentUpdate != observedUpdate;
                    observedUpdate = currentUpdate;
                    var update = TickTutorialPeriodBorrow(candidate, tutorialMap.SourceActiveMilliseconds,
                        !playback!.IsPaused && pulse);
                    if (GodotObject.IsInstanceValid(playbackMap))
                        playbackMap!.SetPeriodPlaybackPaused(playback, playback.IsPaused || update.Hold);
                    return update;
                };
                endTutorial = EndTutorialPeriodBorrow;
            }
            playback = new(SynchronizationContext.Current ?? throw new InvalidOperationException(
                "Period playback requires the host UI synchronization context."),
                candidate => PublishPeriodPlayback(playback!, playbackMap, candidate),
                () =>
                {
                    RetirePeriodMapMusic(playback!);
                    RetirePeriodDuelCaptureAnnouncement(playback!);
                    RetirePeriodDuelPresentation(playback!);
                    CloseManualTacticCommands();
                    RetireCommanderCardRequests();
                    if (GodotObject.IsInstanceValid(playbackMap))
                        playbackMap!.ReleasePeriodPlaybackCandidate(playback!);
                    RefreshManualTacticCommands(null);
                    RefreshPeriodPlaybackControls();
                }, cancellation.Token, beginTutorial, updateTutorial, endTutorial,
                ReferenceEquals(_periodProgressSession, source) ? _periodProgressAdmission : null);
            _periodPlayback = playback; _periodPlaybackMap = playbackMap;
            if(!replyOnly)playbackMap?.SetPeriodProgressFrameDriver(playback);
            if (playbackMap is not null) RefreshOrdinaryTacticBinding(source, playbackMap);
            playback.SetSpeed(_periodPlaybackSpeed);
            if (!continuingStory&&!replyOnly) overlay = ShowPeriodAdvanceProgress(playbackMap, playback);
            var voice = _preferences.Voice.Capture();
            if (playbackMap?.SourceMainCameraTerrain is { } terrain)
                composition.ConfigureBaseCapturePresentationTerrain(terrain);
            var armySpeechViews = playbackMap?.CaptureArmyTraitSpeechViews();
            composition.ConfigureArmyTraitSpeechViews(armySpeechViews);
            playback.ConfigureArmyTraitSpeechViews(armySpeechViews);
            composition.ConfigureFacilityPeriodicArmyViews(playbackMap is null ? null : playbackMap.CaptureFacilityArmyView);
            composition.ConfigureArmyFireViews(playbackMap is null ? null : playbackMap.CaptureArmyFireView);
            if (playbackMap is not null) composition.ConfigureTacticPresentationViews(playbackMap.TacticPresentationViews);
            var storyBindings = CaptureNormalTurnStoryBindings(source, composition, playbackMap, cancellation.Token);
            var result = campaignChoice switch
            {
                null => await composition.AdvancePeriodAsync(source, cancellation.Token,
                    new(_presentationRandom.State), voice, playback.Checkpoint, storyBindings, playback.AdmitStage),
                ConfirmDiplomaticNegotiationOrder diplomatic => await composition.ConfirmDiplomaticNegotiationAsync(
                    source, diplomatic, cancellation.Token, new(_presentationRandom.State), voice, playback.Checkpoint, storyBindings, playback.AdmitStage),
                ConfirmSelfRecommendationOrder recommendation => await composition.ConfirmSelfRecommendationAsync(
                    source, recommendation, cancellation.Token, new(_presentationRandom.State), voice, playback.Checkpoint, storyBindings, playback.AdmitStage),
                ConfirmCoalitionInvitationOrder invitation => await composition.ConfirmCoalitionInvitationAsync(
                    source, invitation, cancellation.Token, new(_presentationRandom.State), voice, playback.Checkpoint, storyBindings, playback.AdmitStage),
                ConfirmArmyReturnCaptiveBatchOrder captiveBatch => await composition.ConfirmArmyReturnCaptiveBatchAsync(
                    source, captiveBatch, cancellation.Token, new(_presentationRandom.State), voice, playback.Checkpoint, storyBindings, playback.AdmitStage),
                ConfirmArmyReturnCaptiveExecutionOrder captiveExecution => await composition.ConfirmArmyReturnCaptiveExecutionAsync(
                    source, captiveExecution, cancellation.Token, new(_presentationRandom.State), voice, playback.Checkpoint, storyBindings, playback.AdmitStage),
                CompleteArmyReturnCaptivePresentationOrder captivePresentation => await composition.CompleteArmyReturnCaptivePresentationAsync(
                    source, captivePresentation, cancellation.Token, new(_presentationRandom.State), voice, playback.Checkpoint, storyBindings, playback.AdmitStage),
                ConfirmCapturedRulerTreatmentOrder treatment => await composition.ConfirmCapturedRulerTreatmentAsync(
                    source, treatment, cancellation.Token, new(_presentationRandom.State), voice, playback.Checkpoint, storyBindings, playback.AdmitStage),
                ConfirmRulerSuccessionOrder succession => await composition.ConfirmRulerSuccessionAsync(
                    source, succession, cancellation.Token, new(_presentationRandom.State), voice, playback.Checkpoint, storyBindings, playback.AdmitStage),
                _ => throw new InvalidOperationException("Unsupported campaign decision.")
            };
            cancellation.Token.ThrowIfCancellationRequested();
            if (_failed || _shuttingDown || !GodotObject.IsInstanceValid(this) || !IsInsideTree() ||
                !result.IsCurrent(_session)) return;

            _session = result.Session;
            AdoptStoryPresentationSession(source,result.Session,result.Composition,playbackMap);
            AdoptOrdinaryTacticSession(source, result.Session, playbackMap);
            SynchronizeApplicationCrtRandom(PersonnelPresentationOwner(result.Session));
            if (PersonnelPresentationOwner(result.Session).CampaignAppRandom32 is { } appRandom)
                _presentationRandom.InstallCommittedState(appRandom.State);
            adopted = result.Session;
            _sceneAudioBinding?.AdoptPlayableSession(source, result.Session);
            AdoptPeriodReportMusicScene(source, result.Session);
            _campaignComposition = result.Composition;
            try { result.Composition.PublishApplicationServices(PersonnelPresentationOwner(result.Session)); }
            catch (Exception error) { applicationServiceFailure = error; }
            _loadedMap = result.Composition.Map;
            _map = _loadedMap.Map;
            _mapOverview = result.Composition.Overview;
            PublishCampaignPresentation(result.Presentation);
            _playMessage.Text = "";
        }
        catch (OperationCanceledException error) when (cancellation.IsCancellationRequested &&
            error.CancellationToken == cancellation.Token) { }
        catch (Exception error)
        {
            if (!_shuttingDown && GodotObject.IsInstanceValid(this) && IsInsideTree() && ReferenceEquals(_session, source))
            {
                if (campaignChoice is ConfirmDiplomaticNegotiationOrder)
                    _diplomacyNegotiationNotice = _ui.Get("play_diplomacy_offer_failed");
                ReportPlayFailure(error);
            }
        }
        finally
        {
            if (adopted is null) playback?.DiscardInitializedArmySpeechViews();
            bool ownsAdvance = ReferenceEquals(_periodAdvanceCancellation, cancellation);
            if (ownsAdvance)
            {
                _periodProgressSession = adopted?.PendingNormalPeriod is not null ? adopted : null;
                _periodProgressAdmission = _periodProgressSession is not null ? playback?.Admission : null;
                ClosePeriodMenuForPeriod(playback);
                CloseCampaignHelpForPeriod(playback);
                _advancingPeriod = false;
                _periodReplyOnly = false;
                _periodAdvanceCancellation = null;
                _periodPlayback = null;
                _periodPlaybackMap = null;
            }
            bool resume = ownsAdvance && ReferenceEquals(_session, adopted ?? source);
            if (!replyOnly && playback is not null && GodotObject.IsInstanceValid(playbackMap))
                playbackMap!.ClearPeriodProgressFrameDriver(playback);
            // This is the map that borrowed this operation, including during
            // shutdown or campaign replacement. A newer map owns itself.
            if (!replyOnly && playback is not null && GodotObject.IsInstanceValid(playbackMap))
            {
                // A returned session can still own a private waiting period.
                // A typed source wait retains its reached candidate for map
                // presentation, alongside the existing active Story owner.
                // The authoritative command world remains committed.
                bool showingStory = resume && IsStoryPresentationCurrent;
                var displayedWorld = resume ? showingStory ? _storyPresentation!.World :
                    _world is { } committedRoot ? ResidentPresentationWorld(committedRoot) : null : null;
                bool showingTypedWait = !showingStory && displayedWorld is not null &&
                    !ReferenceEquals(displayedWorld, _world);
                bool adoptPresentation = resume && adopted is not null &&
                    (adopted.PendingNormalPeriod is null || showingStory || showingTypedWait);
                playbackMap!.EndPeriodPlayback(playback, adoptPresentation, displayedWorld);
            }
            cancellation.Dispose();
            if (GodotObject.IsInstanceValid(this) && IsInsideTree() && !_shuttingDown && !_failed)
            {
                if (resume) RunUiAction(() =>
                {
                    ClosePeriodAdvanceProgress(overlay, playbackMap, resumeMap: !continuingStory&&!replyOnly);
                    RefreshCurrentPlayWorld();
                    if (adopted is not null && automaticSaveNotice is not null)
                        _playMessage.Text = automaticSaveNotice;
                    if (applicationServiceFailure is not null)
                        ReportPlayFailure(applicationServiceFailure);
                    if (_world?.Phase == TurnPhase.Reporting && !HasForcePresentation) SetMapFocus(false);
                });
                else
                {
                    // A replaced campaign owns its own modal state. Retire
                    // only this calculation's overlay after a late result.
                    try { ClosePeriodAdvanceProgress(overlay, playbackMap, resumeMap: false); }
                    catch (Exception error) { GD.PushWarning(error.ToString()); }
                }
            }
        }
    }

    private Control ShowPeriodAdvanceProgress(MapSceneView? map, CampaignPeriodPlayback owner)
    {
        // This lease has no alternate progress artwork. The resident original
        // header owns pause, speed and its ThirdDate-driven UV progress ring.
        var overlay=new Control {Name="AdvancePeriodProgress",MouseFilter=MouseFilterEnum.Ignore};
        _periodAdvanceOverlay=overlay;AddChild(overlay);
        RefreshPeriodPlaybackControls();
        if (GodotObject.IsInstanceValid(map)) map!.BeginPeriodPlayback(owner);
        SetMapFocus(true);
        return overlay;
    }

    private void ClosePeriodAdvanceProgress(Control? overlay, MapSceneView? map, bool resumeMap)
    {
        bool ownsOverlay = ReferenceEquals(_periodAdvanceOverlay, overlay);
        if (ownsOverlay)
        {
            _periodAdvanceOverlay = null;
            RefreshPeriodPlaybackControls();
        }
        if (overlay is not null && GodotObject.IsInstanceValid(overlay))
        {
            overlay.Hide();
            if (overlay.GetParent() is Node parent) parent.RemoveChild(overlay);
            overlay.QueueFree();
        }
        if (resumeMap && ownsOverlay && ReferenceEquals(_residentMap, map))
        {
            if (GodotObject.IsInstanceValid(map)) map!.SetDetailsOpen(false);
            SetMapFocus(_mapFocused);
        }
    }
}
