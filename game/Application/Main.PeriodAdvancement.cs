using Godot;
using San14.Core;
using San14.Client.Gameplay;

public partial class Main
{
    private bool _advancingPeriod;
    private Task? _periodAdvanceTask;
    private CancellationTokenSource? _periodAdvanceCancellation;
    private Control? _periodAdvanceOverlay;
    private CampaignPeriodPlayback? _periodPlayback;
    private MapSceneView? _periodPlaybackMap;
    private int _periodPlaybackSpeed = 1;
    private void ActivateForceHudAdvance()
    {
        if(_advancingPeriod)TryTogglePeriodPlayback();
        else RunPlayAction(AdvancePlayable);
    }

    private bool TryTogglePeriodPlayback()
    {
        if (!_advancingPeriod || _periodPlayback is not {} playback || _shuttingDown) return false;
        playback.TogglePause();
        if (GodotObject.IsInstanceValid(_periodPlaybackMap))
            _periodPlaybackMap!.SetPeriodPlaybackPaused(playback, playback.IsPaused || playback.ManualCommandSelectionOpen);
        RefreshPeriodPlaybackControls(); return true;
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
            hud.ApplyExecution(_advancingPeriod&&_periodPlayback is not null,_periodPlayback?.IsPaused==true,
                _periodPlayback?.Speed??_periodPlaybackSpeed,
                _periodPlayback is{} period?period.Candidate?.NativeMapSeason?.ThirdDate:_world?.NativeMapSeason?.ThirdDate);
    }

    private void PublishPeriodPlayback(CampaignPeriodPlayback owner, MapSceneView? map, GameState candidate)
    {
        if (!ReferenceEquals(_periodPlayback, owner) || _shuttingDown || _failed || !IsInsideTree()) return;
        if (GodotObject.IsInstanceValid(map)) map!.PublishPeriodPlayback(owner, candidate);
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
                    RetirePeriodDuelCaptureAnnouncement(playback!);
                    RetirePeriodDuelPresentation(playback!);
                    CloseManualTacticCommands();
                    RetireCommanderCardRequests();
                    if (GodotObject.IsInstanceValid(playbackMap))
                        playbackMap!.ReleasePeriodPlaybackCandidate(playback!);
                    RefreshManualTacticCommands(null);
                    RefreshPeriodPlaybackControls();
                }, cancellation.Token, beginTutorial, updateTutorial, endTutorial);
            _periodPlayback = playback; _periodPlaybackMap = playbackMap;
            if (playbackMap is not null) RefreshOrdinaryTacticBinding(source, playbackMap);
            playback.SetSpeed(_periodPlaybackSpeed);
            if (!continuingStory) overlay = ShowPeriodAdvanceProgress(playbackMap, playback);
            var voice = _preferences.Voice.Capture();
            if (playbackMap?.SourceMainCameraTerrain is { } terrain)
                composition.ConfigureBaseCapturePresentationTerrain(terrain);
            composition.ConfigureArmyTraitSpeechViews(playbackMap?.CaptureArmyTraitSpeechViews());
            composition.ConfigureFacilityPeriodicArmyViews(playbackMap is null ? null : playbackMap.CaptureFacilityArmyView);
            composition.ConfigureArmyFireViews(playbackMap is null ? null : playbackMap.CaptureArmyFireView);
            if (playbackMap is not null) composition.ConfigureTacticPresentationViews(playbackMap.TacticPresentationViews);
            var storyBindings = CaptureNormalTurnStoryBindings(source, composition, playbackMap, cancellation.Token);
            var result = campaignChoice switch
            {
                null => await composition.AdvancePeriodAsync(source, cancellation.Token,
                    new(_presentationRandom.State), voice, playback.Checkpoint, storyBindings),
                ConfirmDiplomaticNegotiationOrder diplomatic => await composition.ConfirmDiplomaticNegotiationAsync(
                    source, diplomatic, cancellation.Token, new(_presentationRandom.State), voice, playback.Checkpoint, storyBindings),
                ConfirmSelfRecommendationOrder recommendation => await composition.ConfirmSelfRecommendationAsync(
                    source, recommendation, cancellation.Token, new(_presentationRandom.State), voice, playback.Checkpoint, storyBindings),
                ConfirmCoalitionInvitationOrder invitation => await composition.ConfirmCoalitionInvitationAsync(
                    source, invitation, cancellation.Token, new(_presentationRandom.State), voice, playback.Checkpoint, storyBindings),
                ConfirmCapturedRulerTreatmentOrder treatment => await composition.ConfirmCapturedRulerTreatmentAsync(
                    source, treatment, cancellation.Token, new(_presentationRandom.State), voice, playback.Checkpoint, storyBindings),
                ConfirmRulerSuccessionOrder succession => await composition.ConfirmRulerSuccessionAsync(
                    source, succession, cancellation.Token, new(_presentationRandom.State), voice, playback.Checkpoint, storyBindings),
                _ => throw new InvalidOperationException("Unsupported campaign decision.")
            };
            if (_failed || _shuttingDown || !GodotObject.IsInstanceValid(this) || !IsInsideTree() ||
                !result.IsCurrent(_session)) return;

            _session = result.Session;
            AdoptStoryPresentationSession(source,result.Session,result.Composition,playbackMap);
            AdoptOrdinaryTacticSession(source, result.Session, playbackMap);
            SynchronizeApplicationCrtRandom(result.Session.PendingNormalPeriod?.Candidate ?? result.Session.World);
            if ((result.Session.PendingNormalPeriod?.Candidate??result.Session.World).CampaignAppRandom32 is { } appRandom)
                _presentationRandom.InstallCommittedState(appRandom.State);
            adopted = result.Session;
            _sceneAudioBinding?.AdoptPlayableSession(source, result.Session);
            _campaignComposition = result.Composition;
            try { result.Composition.PublishApplicationServices(PersonnelPresentationOwner(result.Session)); }
            catch (Exception error) { applicationServiceFailure = error; }
            _loadedMap = result.Composition.Map;
            _map = _loadedMap.Map;
            _mapOverview = result.Composition.Overview;
            PublishCampaignPresentation(result.Presentation);
            _playMessage.Text = "";
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
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
            bool ownsAdvance = ReferenceEquals(_periodAdvanceCancellation, cancellation);
            if (ownsAdvance)
            {
                _advancingPeriod = false;
                _periodAdvanceCancellation = null;
                _periodPlayback = null;
                _periodPlaybackMap = null;
            }
            bool resume = ownsAdvance && ReferenceEquals(_session, adopted ?? source);
            // This is the map that borrowed this operation, including during
            // shutdown or campaign replacement. A newer map owns itself.
            if (playback is not null && GodotObject.IsInstanceValid(playbackMap))
            {
                // A returned session can still own a private waiting period.
                // Only its committed world, or its active story world, owns
                // the map after this borrow; never briefly redraw the old one
                // through presentation owners advanced by the candidate.
                bool showingStory = resume && IsStoryPresentationCurrent;
                var displayedWorld = resume ? showingStory ? _storyPresentation!.World : _world : null;
                bool adoptPresentation = resume && adopted is not null &&
                    (adopted.PendingNormalPeriod is null || showingStory);
                playbackMap!.EndPeriodPlayback(playback, adoptPresentation, displayedWorld);
            }
            cancellation.Dispose();
            if (GodotObject.IsInstanceValid(this) && IsInsideTree() && !_shuttingDown && !_failed)
            {
                if (resume) RunUiAction(() =>
                {
                    ClosePeriodAdvanceProgress(overlay, playbackMap, resumeMap: !continuingStory);
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
