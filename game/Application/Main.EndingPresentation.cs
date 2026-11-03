using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private sealed class EndingWorkspace(GameplaySession session, CampaignComposition composition,
        MapSceneView map, ArmyCampaignRuleAdapter rules, bool returnMapFocus)
    {
        internal readonly GameplaySession Session = session;
        internal readonly CampaignComposition Composition = composition;
        internal readonly MapSceneView Map = map;
        internal readonly ArmyCampaignRuleAdapter Rules = rules;
        internal readonly bool ReturnMapFocus = returnMapFocus;
        internal readonly CancellationTokenSource Lifetime = new();
        internal GameState World = session.World;
        internal Control? Shield;
        internal Control? View;
        internal CampaignServiceChanges? PublishedServices;
        internal bool Waiting, Blocked, MovieAudioPrepared;
    }

    private EndingWorkspace? _endingPresentation;
    private GameState? _endingFailedWorld;
    private bool HasEndingPresentation => _endingPresentation is not null;
    private bool IsEndingCurrent(EndingWorkspace owner) => ReferenceEquals(_endingPresentation, owner) &&
        ReferenceEquals(_session, owner.Session) && ReferenceEquals(_residentMap, owner.Map) &&
        ReferenceEquals(owner.Session.World, owner.World) && !owner.Lifetime.IsCancellationRequested;
    private bool EndingInputAdmitted(EndingWorkspace owner) => IsEndingCurrent(owner) &&
        !owner.Blocked && !_advancingPeriod && CanAdvanceNarration;

    private bool TryOpenEndingPresentation(GameplaySession session, CampaignComposition composition, MapSceneView map)
    {
        if (_endingPresentation is { } current)
        {
            if (IsEndingCurrent(current)) return true;
            CloseEndingPresentation();
        }
        var world = session.World;
        if (ReferenceEquals(_endingFailedWorld, world) || session.HasPendingOperation || PersonnelPresentationQueue.Next(world) is not null ||
            world.EndingProgress?.Completed == true || world.EndingProgress is null &&
            (world.CampaignTurnStop is not { Reason: CampaignTurnStopReason.OrdinaryForcesResolved } stop ||
             stop.EndingRequested == false)) return false;
        var rules = composition.PrepareArmyQueries(world);
        rules.ConfigurePersonnelNarrationVoice(_preferences.Voice.Capture());
        var owner = new EndingWorkspace(session, composition, map, rules, _mapFocused);
        _endingPresentation = owner;
        try
        {
            CaptureNormalTurnStoryBindings(session, composition, map, owner.Lifetime.Token);
            if (map.SourceMainCameraTerrain is { } terrain) rules.ConfigureStoryCameraTerrain(terrain);
            RetirePeriodReportView(); CloseActionReport(); map.CloseCityCommandMenu();
            map.SetDetailsOpen(true, keepMapVisible: true); SetMapFocus(false); HideDeploymentHud();
            owner.Shield = new Control { Name = "EndingInputScope", ZIndex = 90, MouseFilter = Control.MouseFilterEnum.Stop };
            AddChild(owner.Shield); owner.Shield.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            PumpEnding(owner);
            return HasEndingPresentation;
        }
        catch (Exception error) { FailEnding(owner, error); return true; }
    }

    private void AdoptEndingWorld(EndingWorkspace owner)
    {
        owner.World = owner.Session.World;
        AdoptStoryRandom(owner.World);
        // Data adds source-time deltas only after the corresponding business
        // stage. Publishing the outbox is idempotent and never grants from stop.
        if (!ReferenceEquals(owner.PublishedServices, owner.World.PendingCampaignServices))
        {
            owner.Composition.PublishApplicationServices(owner.World);
            owner.PublishedServices = owner.World.PendingCampaignServices;
        }
    }

    private void PumpEnding(EndingWorkspace owner)
    {
        if (!IsEndingCurrent(owner) || owner.Waiting || owner.Blocked) return;
        try
        {
            // The native stage13 fades music before checking MOVIE_PLAY. Data
            // can pass directly to14 when disabled, so observe this boundary
            // before asking it to finish the final branch story as well.
            if (owner.World.EndingProgress is { } before && (before.Stage >= 13 ||
                before.Stage is >= 6 and <= 12 && before.Story is { } finalStory &&
                finalStory.NextStep == finalStory.Steps.Length)) PrepareEndingMovieAudio(owner);
            if (!owner.Session.TryPrepareEndingStep(owner.World, owner.Rules, out var progress, out var missing))
                throw new InvalidDataException(missing ?? "ending.prepare");
            AdoptEndingWorld(owner);
            if (progress is null || progress.Completed) { CloseEndingPresentation(); RefreshEndingParent(); return; }
            owner.Waiting = true;
            if (progress.Story is not null) { OpenEndingStory(owner); return; }
            var pending = progress.Pending ?? throw new InvalidDataException("ending.presentation_pending");
            switch (pending.Kind)
            {
                case CampaignEndingPresentationKind.Opening:
                    _ = PresentEndingOpening(owner, progress, pending); break;
                case CampaignEndingPresentationKind.AbdicationChoice:
                {
                    var view = new DeploymentTargetConfirmation(); owner.View = view; AddChild(view);
                    view.ConfigureWrappedMessage(_repository!, _ui, pending.TextAtSource!, accepted => RunUiAction(() =>
                    {
                        if (!IsEndingCurrent(owner)) return;
                        if (!owner.Session.TryConfirmEndingAbdication(owner.World, progress, pending.Sequence,
                            accepted, owner.Rules, out var reason)) throw new InvalidDataException(reason);
                        FinishEndingWait(owner);
                    }));
                    view.ZIndex = 100; break;
                }
                case CampaignEndingPresentationKind.KingdomNameChoice:
                {
                    var view = new EndingKingdomSelectionView(); owner.View = view; AddChild(view);
                    view.Configure(_repository!, _ui, owner.Rules.PrepareKingdomNameSelection(pending.KingdomChoices), () => EndingInputAdmitted(owner),
                        selected => RunUiAction(() =>
                        {
                            if (!IsEndingCurrent(owner)) return;
                            string? reason;
                            bool accepted = selected is int id
                                ? owner.Session.TryConfirmEndingKingdom(owner.World, progress, pending.Sequence, id, owner.Rules, out reason)
                                : owner.Session.TryCancelEndingKingdom(owner.World, progress, pending.Sequence, owner.Rules, out reason);
                            if (!accepted) throw new InvalidDataException(reason);
                            FinishEndingWait(owner);
                        }), CurrentListScrollSpeed);
                    view.ZIndex = 100; break;
                }
                case CampaignEndingPresentationKind.OfficerModal:
                    ShowEndingMessage(owner, pending.TextAtSource!, () => CompleteEndingWait(owner, progress, pending), pending.Speaker);
                    break;
                case CampaignEndingPresentationKind.Movie:
                    OpenEndingMovie(owner, progress, pending); break;
                case CampaignEndingPresentationKind.CompletionNotices:
                    _ = PresentEndingNotice(owner, progress, pending, 0); break;
                case CampaignEndingPresentationKind.Exit:
                    // The child state returns to its real map/report parent.
                    // It does not manufacture a return-to-title application exit.
                    CloseEndingPresentation();
                    if (!owner.Session.TryCompleteEndingPresentation(owner.World, progress, pending.Sequence,
                        owner.Rules, out var exitMissing)) throw new InvalidDataException(exitMissing);
                    AdoptEndingWorld(owner); RefreshEndingParent(); break;
                default: throw new InvalidDataException("ending.presentation_kind");
            }
        }
        catch (Exception error) { FailEnding(owner, error); }
    }

    private void CompleteEndingWait(EndingWorkspace owner, CampaignEndingProgress progress,
        CampaignEndingPresentation pending)
    {
        if (!IsEndingCurrent(owner)) return;
        if (!owner.Session.TryCompleteEndingPresentation(owner.World, progress, pending.Sequence,
            owner.Rules, out var missing)) throw new InvalidDataException(missing ?? "ending.receipt");
        FinishEndingWait(owner);
    }

    private void FinishEndingWait(EndingWorkspace owner)
    {
        ReleaseEndingView(owner); AdoptEndingWorld(owner); owner.Waiting = false;
        Callable.From(() => PumpEnding(owner)).CallDeferred();
    }

    private void ShowEndingMessage(EndingWorkspace owner, string message, Action completed,
        TalentOfficerPresentationContext? speaker = null)
    {
        var view = new DeploymentNarrationView { Name = "EndingMessage", ZIndex = 100,
            KeyboardInput = input => HandleNarrationKeyboard(input, (_endingPresentation?.View as DeploymentNarrationView)) };
        owner.View = view; AddChild(view);
        if (speaker is null)
            view.ConfigureAnnouncement(_repository!, _ui, message, () => RunUiAction(completed),
                () => _preferences.AutomaticMessageAdvance, () => EndingInputAdmitted(owner));
        else
        {
            var query = owner.Rules.QueryPersonnelPresentationSpeaker(owner.World, speaker);
            var publication = query.Value ?? throw new InvalidDataException(query.UnresolvedInput);
            var route = publication.StandingPortrait ?? throw new InvalidDataException(
                publication.PortraitUnresolvedInput ?? "ending.ruler_standing_portrait");
            var sprites = new LocalizedUiSpriteSource(_repository!);
            var layers = route.LoadSprites(sprites.LoadTexturePackSprite);
            view.Configure(_repository!, _ui, publication.Name, message, layers, () => RunUiAction(completed),
                () => _preferences.AutomaticMessageAdvance, () => EndingInputAdmitted(owner));
        }
    }

    private async Task PresentEndingOpening(EndingWorkspace owner, CampaignEndingProgress progress,
        CampaignEndingPresentation pending)
    {
        try
        {
            var audio = _sceneAudioBinding ?? throw new InvalidOperationException("ending.audio_owner");
            audio.BeginEndingAudio();
            await audio.PlayEndingOpeningMusicAsync(_ui.SourceLocale, _preferences.MusicSelection, owner.Lifetime.Token);
            if (!IsEndingCurrent(owner)) return;
            ShowEndingMessage(owner, pending.TextAtSource!, () => { _ = CloseOpening(); });
            async Task CloseOpening()
            {
                try
                {
                    ReleaseEndingView(owner);
                    await audio.CompleteEndingOpeningMusicAsync(_ui.SourceLocale, _preferences.MusicSelection, owner.Lifetime.Token);
                    CompleteEndingWait(owner, progress, pending);
                }
                catch (OperationCanceledException) when (owner.Lifetime.IsCancellationRequested) { }
                catch (Exception error) { FailEnding(owner, error); }
            }
        }
        catch (OperationCanceledException) when (owner.Lifetime.IsCancellationRequested) { }
        catch (Exception error) { FailEnding(owner, error); }
    }

    private async Task PresentEndingNotice(EndingWorkspace owner, CampaignEndingProgress progress,
        CampaignEndingPresentation pending, int index)
    {
        try
        {
            if (!IsEndingCurrent(owner)) return;
            if (index == pending.Notices.Length) { CompleteEndingWait(owner, progress, pending); return; }
            var notice = pending.Notices[index];
            if (notice.SoundId is int sound)
                await (_sceneAudioBinding ?? throw new InvalidOperationException("ending.notice_audio_owner"))
                    .PlayNormalSoundAsync(sound, owner.Lifetime.Token);
            if (!IsEndingCurrent(owner)) return;
            ShowEndingMessage(owner, notice.TextAtSource, () =>
            {
                ReleaseEndingView(owner);
                _ = PresentEndingNotice(owner, progress, pending, index + 1);
            });
        }
        catch (OperationCanceledException) when (owner.Lifetime.IsCancellationRequested) { }
        catch (Exception error) { FailEnding(owner, error); }
    }

    private void OpenEndingStory(EndingWorkspace owner)
    {
        var audio = _sceneAudioBinding ?? throw new InvalidOperationException("ending.story_audio_owner");
        var host = new StoryPlaybackHost { ZIndex = 100 }; owner.View = host; AddChild(host);
        host.Configure(_repository!, _ui, new(owner.Rules, () => owner.Session.World,
            () => owner.Session.World.EndingProgress?.Story,
            () =>
            {
                if (!owner.Session.TryPrepareEndingStoryStep(owner.World, owner.Rules, out var prepared))
                    throw new InvalidDataException(prepared.UnresolvedOwner ?? "ending.story_prepare");
                AdoptEndingWorld(owner);
            }, receipt =>
            {
                if (!IsEndingCurrent(owner) || owner.World.EndingProgress is not { } progress ||
                    !owner.Session.TryCompleteEndingStoryStep(owner.World, progress, receipt)) return false;
                AdoptEndingWorld(owner); return true;
            }), owner.Map, audio, _preferences.MusicSelection, () => EndingInputAdmitted(owner),
            () => _preferences.AutomaticMessageAdvance, CurrentListScrollSpeed, HandleNarrationKeyboard,
            _ => AdoptEndingWorld(owner), () => FinishEndingWait(owner), error => FailEnding(owner, error), inputWaitKeyboard: HandleNarrationKeyboard, preparedUiResources: TryBorrowApplicationPreparedResources(host));
    }

    private void OpenEndingMovie(EndingWorkspace owner, CampaignEndingProgress progress,
        CampaignEndingPresentation pending)
    {
        PrepareEndingMovieAudio(owner);
        var playback = _repository!.LoadMoviePlayback(pending.MovieId!.Value, _ui.SourceLocale);
        var view = new CollectionMoviePlaybackView(); owner.View = view; AddChild(view);
        view.Configure(_repository, _ui, playback, ResolveMovieNativeLibrary(), () => EndingInputAdmitted(owner),
            () => _preferences.Audio.Movie / 100f, () => { _ = ReturnMovie(); }, error => FailEnding(owner, error));
        view.ZIndex = 100;
        async Task ReturnMovie()
        {
            try
            {
                view.Abandon();
                var retirement = view.Retirement;
                if (ReferenceEquals(owner.View, view)) owner.View = null;
                // Free the player node so it releases its playback reference;
                // Retirement still owns decoder/audio resources until drained.
                view.QueueFree();
                await retirement;
                if (!IsEndingCurrent(owner) || !ReferenceEquals(owner.World.EndingProgress, progress)) return;
                CompleteEndingWait(owner, progress, pending);
            }
            catch (Exception error) { FailEnding(owner, error); }
        }
    }

    private void PrepareEndingMovieAudio(EndingWorkspace owner)
    {
        if (owner.MovieAudioPrepared) return;
        (_sceneAudioBinding ?? throw new InvalidOperationException("ending.movie_audio_owner")).PrepareEndingMovieAudio();
        owner.MovieAudioPrepared = true;
    }

    private bool HandleEndingInput(InputEvent input)
    {
        if (_endingPresentation is not { } owner) return false;
        var message = owner.View is StoryPlaybackHost host ? host.Message : owner.View as DeploymentNarrationView;
        if (HandleNarrationKeyboard(input, message)) return true;
        if (input is not (InputEventKey { Keycode: Key.Escape, Pressed: true, Echo: false } or
            InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: false })) return false;
        if (owner.Blocked) { CloseEndingFailure(owner); return true; }
        if (!EndingInputAdmitted(owner)) return true;
        switch (owner.View)
        {
            case StoryPlaybackHost story: story.RequestSkip(); return true;
            case CollectionMoviePlaybackView movie: movie.RequestBack(); return true;
            case DeploymentTargetConfirmation choice: choice.RequestCancel(); return true;
            case EndingKingdomSelectionView kingdom: kingdom.RequestCancel(); return true;
            default: return false; // The narration's actual widget owns its close input.
        }
    }

    private void ReleaseEndingView(EndingWorkspace owner)
    {
        var view = owner.View; owner.View = null;
        switch (view)
        {
            case StoryPlaybackHost story: story.Abandon(); break;
            case DeploymentNarrationView message: message.Abandon(); break;
            case DeploymentTargetConfirmation choice: choice.Abandon(); break;
            case EndingKingdomSelectionView kingdom: kingdom.Abandon(); break;
            case CollectionMoviePlaybackView movie:
                movie.Abandon();
                var retirement = movie.Retirement;
                movie.QueueFree(); _ = RetireMovie(); return;
                async Task RetireMovie()
                {
                    try { await retirement; }
                    catch (Exception error) { GD.PushWarning(error.ToString()); }
                }
        }
        if (view is not null) { view.GetParent()?.RemoveChild(view); view.QueueFree(); }
    }

    private void FailEnding(EndingWorkspace owner, Exception error)
    {
        if (!IsEndingCurrent(owner)) return;
        owner.Blocked = true; ReleaseEndingView(owner);
        ReportPlayFailure(error); // Retain the actual pending step; no false receipt.
        try
        {
            var notice = new DeploymentNarrationView { Name = "EndingUnavailable", ZIndex = 100 };
            owner.View = notice; AddChild(notice);
            notice.ConfigureAnnouncement(_repository!, _ui, error.Message, () => CloseEndingFailure(owner),
                () => _preferences.AutomaticMessageAdvance, () => IsEndingCurrent(owner) && CanAdvanceNarration);
        }
        catch (Exception notificationError)
        {
            GD.PushWarning(notificationError.ToString()); CloseEndingFailure(owner);
        }
    }

    private void CloseEndingFailure(EndingWorkspace owner)
    {
        if (!IsEndingCurrent(owner)) return;
        _endingFailedWorld = owner.World;
        CloseEndingPresentation(); RefreshEndingParent();
    }

    private void CloseEndingPresentation()
    {
        var owner = _endingPresentation; _endingPresentation = null;
        if (owner is null) return;
        owner.Lifetime.Cancel(); ReleaseEndingView(owner);
        if (owner.Shield is { } shield) { shield.GetParent()?.RemoveChild(shield); shield.QueueFree(); }
        if (GodotObject.IsInstanceValid(owner.Map) && ReferenceEquals(_residentMap, owner.Map))
        {
            owner.Map.SetDetailsOpen(false);
            if (!_failed && !_shuttingDown) SetMapFocus(owner.ReturnMapFocus);
        }
    }

    private void RefreshEndingParent()
    {
        RefreshSceneAudioContext(); RefreshPlayControls(); RefreshPlayablePeriodReport();
    }
}
