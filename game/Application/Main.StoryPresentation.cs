using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private sealed class StoryPresentationBinding(GameplaySession session, GameState world,
        MapSceneView map, ArmyCampaignRuleAdapter rules, StoryPresentationView view, CampaignStoryRequest story, bool mapFocused)
    {
        internal GameplaySession Session = session;
        internal readonly long Sequence = story.Sequence;
        internal readonly EventSourceReference Source = story.Source;
        internal readonly string ContentRevision = story.ContentRevision;
        internal GameState World = world;
        internal readonly MapSceneView Map = map;
        internal readonly bool ReturnMapFocus = mapFocused;
        internal ArmyCampaignRuleAdapter Rules = rules;
        internal readonly StoryPresentationView View = view;
        internal readonly CancellationTokenSource Cancellation = new();
        internal int StartedStep = -1;
        internal bool OwnsVoice;
        internal Task MediaRestoration = Task.CompletedTask;
        internal bool SkipRequested, FastRequested;
        internal PoemEventView? PoemEvent;
        internal StoryLogView? Log;
        internal Action? AfterLog;
    }
    private StoryPresentationBinding? _storyPresentation;
    private bool HasStoryPresentation => _storyPresentation is not null;
    private bool IsStoryPresentationCurrent => _storyPresentation is { } binding && IsStoryCurrent(binding);
    private bool IsStoryCurrent(StoryPresentationBinding binding) =>
        ReferenceEquals(_storyPresentation, binding) && ReferenceEquals(_session, binding.Session) &&
        ReferenceEquals(_residentMap, binding.Map) &&
        ReferenceEquals(StoryOwner(binding.Session), binding.World);
    private bool StoryInputAdmitted(StoryPresentationBinding binding) => IsStoryCurrent(binding) &&
        StoryPlayback(binding.Session)?.Phase != NormalTurnStoryResultPhase.PostResult &&
        CanAdvanceNarration && !_advancingPeriod && !HasRulerDecision && !HasNormalStoryResult && !HasNormalStoryForceRank && binding.Log is null && binding.PoemEvent is null &&
        binding.MediaRestoration.IsCompletedSuccessfully;

    private bool IsStoryAtManagerBarrier(StoryPresentationBinding binding) => IsStoryCurrent(binding) &&
        StoryPlayback(binding.Session) is { } playback &&
        playback.Story.Sequence == binding.Sequence && playback.Story.Source == binding.Source &&
        playback.Phase is NormalTurnStoryResultPhase.Result or NormalTurnStoryResultPhase.Confirmed or NormalTurnStoryResultPhase.PostResult or
            NormalTurnStoryResultPhase.Exiting or NormalTurnStoryResultPhase.Exited;

    private bool TryOpenStoryPresentation(GameplaySession session, NormalPeriodOperation? pending,
        CampaignComposition composition, MapSceneView map, CampaignStoryRequest? retained = null, bool restoreMedia = true,
        long? restoringPostResultBatch = null)
    {
        var story = PendingCampaignStory(session) ?? retained;
        if (story is null) return false;
        //An unhandled earlier source effect is still a queue head. Do not
        //skip it merely because a later event has already been prepared.
        bool restorePostResultOwner = restoringPostResultBatch is long batch &&
            StoryPlayback(session) is { Phase: NormalTurnStoryResultPhase.PostResult,
                StateControl.AwaitingPresentationBatch: long awaiting } playback && batch == awaiting &&
            ReferenceEquals(story, playback.Story) &&
            PersonnelPresentationQueue.Next(StoryOwner(session))?.BatchSequence == batch &&
            CampaignStoryStateControlProgress.IsAwaitingPresentation(StoryOwner(session));
        if (restoringPostResultBatch is not null && !restorePostResultOwner)
            throw new InvalidDataException("story.post_result.restore_batch_owner");
        if (PersonnelPresentationQueue.Next(StoryOwner(session)) is not null && !restorePostResultOwner || HasForcePresentation) return false;
        var rules = composition.PrepareArmyQueries(StoryOwner(session));
        rules.ConfigurePersonnelNarrationVoice(_preferences.Voice.Capture());
        if (map.SourceMainCameraTerrain is { } terrain) rules.ConfigureStoryCameraTerrain(terrain);
        var view = new StoryPresentationView { KeyboardInput = HandleGameplayNarrationKeyboard };
        var binding = new StoryPresentationBinding(session, StoryOwner(session), map, rules, view, story, _mapFocused);
        view.MessageModeRequested = action => RequestStoryMessageMode(binding, action);
        view.InputWaitKeyboard = input => HandleNarrationKeyboard(input, view.RequestInputWaitAction);
        view.ReadAccelerated = () => binding.SkipRequested || binding.FastRequested;
        view.OpenLog = () => RunUiAction(() => OpenStoryLog(binding));
        _storyPresentation = binding; AddChild(view);
        if (_sceneAudioBinding is { } audio)
            //The ordinary presentation owner has no nested-Exec audio suppression.
            //Invocation entry/exit is owned separately across all business slices.
            audio.StorySoundSuppressed = () => IsStoryCurrent(binding) ? false : null;
        try
        {
            RetirePeriodReportView(); CloseActionReport();
            view.Configure(_repository!, _ui, () => StoryInputAdmitted(binding));
            //The event observes the completed private Army period. Its map
            //reads that adopted candidate while command authority stays on
            //the committed session world until the normal Turn completes.
            map.RefreshPlayableWorld(binding.World);
            map.BeginStoryPresentation(binding); SetMapFocus(false); HideDeploymentHud();
            RestoreStoryScene(binding);
            if (restoreMedia) StartStoryMediaRestoration(binding, story);
            return true;
        }
        catch { CloseStoryPresentation(); throw; }
    }

    private bool EnsurePostResultSceneBeforePersonnel(GameplaySession session, CampaignComposition composition,
        MapSceneView map, long batch)
    {
        var playback = StoryPlayback(session) ?? throw new InvalidDataException("story.post_result.restore_invocation");
        //Resume saved invocation facts before restoring its actual media. This
        //does not replay entry, retire the scene, or acknowledge any request.
        _ = TryHandleNormalStoryAudio(session, session.PendingNormalPeriod, composition, map);
        if (_normalStoryAudio is not { } audio || audio.Source != playback.Story.Source || audio.Sequence != playback.Story.Sequence)
            throw new InvalidDataException("story.post_result.restore_audio_owner");
        if (_storyPresentation is null && !TryOpenStoryPresentation(session, session.PendingNormalPeriod,
                composition, map, playback.Story, restoringPostResultBatch: batch))
            throw new InvalidDataException("story.post_result.restore_scene");
        var scene = _storyPresentation ?? throw new InvalidDataException("story.post_result.restore_scene_owner");
        if (!IsStoryCurrent(scene) || !ReferenceEquals(scene.Session, session) || !ReferenceEquals(scene.Map, map) ||
            scene.Source != playback.Story.Source || scene.Sequence != playback.Story.Sequence ||
            scene.ContentRevision != playback.Story.ContentRevision)
            throw new InvalidDataException("story.post_result.restore_scene_identity");
        if (scene.MediaRestoration.IsFaulted)
        {
            ReportPlayFailure(scene.MediaRestoration.Exception!.GetBaseException());
            return false;
        }
        return scene.MediaRestoration.IsCompletedSuccessfully;
    }

    private void RestoreStoryScene(StoryPresentationBinding binding)
    {
        var story = StoryPlayback(binding.Session)!.Story;
        var scene = story.Scene;
        binding.View.SetBackground(scene.Background?.IllustrationId ?? 0, scene.Background?.ResourceKind ?? 15);
        binding.View.SetIllustration(scene.Illustration?.IllustrationId ?? 0);
        binding.View.SetSmallIllustration(scene.SmallIllustration?.IllustrationId ?? 0);
        binding.View.SetTutorialIllustration(scene.TutorialIllustration?.IllustrationId ?? 0);
        binding.View.SetItemIllustration(scene.ItemIllustration?.IllustrationId);
        binding.View.RestoreBlackout(scene.Blackout is { FadeIn: false });
        ApplyStoryActors(binding, story, immediateDark: true);
        if (scene.PreparedText is { } text) binding.View.RestoreMessage(text.SpeakerName, text.Text, text.SpeechOnly);
        if (scene.RestoredCamera is { } restored)
        {
            if (!binding.Map.RestoreStoryCamera(binding, restored, immediate: true))
                throw new InvalidDataException("story.camera.restore_scene_owner");
        }
        else if (scene.Camera is { HexId: int hex })
        {
            var target = binding.Rules.QueryStoryCameraTarget(binding.World, hex);
            if (!target.Available || !binding.Map.MoveStoryCamera(binding, target.Value!, 0))
                throw new InvalidDataException(target.UnresolvedInput ?? "story.camera.restore_scene_request");
        }
    }

    private void ApplyStoryActors(StoryPresentationBinding binding, CampaignStoryRequest story, bool immediateDark = false)
    {
        var query = binding.Rules.QueryStoryPresentation(binding.World, story);
        var publication = query.Available ? query.Value! :
            throw new InvalidDataException(query.UnresolvedInput ?? "story.presentation.publication");
        var sprites = new LocalizedUiSpriteSource(_repository!);
        var actors = publication.Actors.Select(actor =>
        {
            if (actor.UnresolvedOwner is not null || !actor.StandingPortrait.Available || actor.StandingPortrait.Value is not { } route)
                throw new InvalidDataException(actor.UnresolvedOwner ?? actor.StandingPortrait.UnresolvedInput ?? "story.presentation.actor");
            var layers = route.LoadSprites(sprites.LoadTexturePackSprite);
            return new StoryActorVisual(actor.Slot, actor.Speaker.OfficerId, layers) { Dark = actor.Dark };
        }).ToArray();
        binding.View.ApplyActors(actors, publication.HighlightedSlot, publication.PortraitDarkFlags, immediateDark);
    }

    private bool IsStoryBetweenExecutionSlices(StoryPresentationBinding binding) =>
        IsStoryCurrent(binding) && binding.Log is null &&
        StoryPlayback(binding.Session) is
            { ExecutionCursor: { IsCompleted: false }, Story: { } story } &&
        story.Sequence == binding.Sequence && story.Source == binding.Source &&
        story.ContentRevision == binding.ContentRevision && story.NextStep == story.Steps.Length;

    // Only the actual worker adoption may transfer this presentation to its
    // successor session. The same event retains its view, map effect slots,
    // camera, text, music and consumed-step position across business slices.
    private void AdoptStoryPresentationSession(GameplaySession source, GameplaySession adopted,
        CampaignComposition composition, MapSceneView? map)
    {
        if (_storyPresentation is not { } binding || !ReferenceEquals(binding.Session, source)) return;
        if (!ReferenceEquals(binding.Map,map) || StoryPlayback(adopted)?.Story is not { } story ||
            story.Sequence != binding.Sequence || story.Source != binding.Source ||
            story.ContentRevision != binding.ContentRevision)
        { CloseStoryPresentation(); return; }
        var rules = composition.PrepareArmyQueries(StoryOwner(adopted));
        rules.ConfigurePersonnelNarrationVoice(_preferences.Voice.Capture());
        if (binding.Map.SourceMainCameraTerrain is { } terrain) rules.ConfigureStoryCameraTerrain(terrain);
        binding.Session = adopted; binding.World = StoryOwner(adopted); binding.Rules = rules;
        binding.Map.RefreshPlayableWorld(binding.World);
        Callable.From(() => RunUiAction(() => PumpStoryStep(binding))).CallDeferred();
    }

    private void PumpStoryStep(StoryPresentationBinding binding)
    {
        if (!IsStoryCurrent(binding)) return;
        var story = PendingCampaignStory(binding.Session);
        if (story is null)
        {
            if (IsStoryBetweenExecutionSlices(binding) || IsStoryAtManagerBarrier(binding))
            { RefreshDiplomaticNegotiation(); return; }
            CloseStoryPresentation();
            if (!TryOpenPersonnelPresentation()) RefreshDiplomaticNegotiation();
            return;
        }
        int stepIndex = story.NextStep;
        if (binding.StartedStep == stepIndex) return;
        binding.StartedStep = stepIndex;
        var step = story.Steps[stepIndex];
        void Complete() => CompleteStoryStep(binding, story.Sequence, stepIndex);
        switch (step)
        {
            case StoryStanding:
                ApplyStoryActors(binding, story, immediateDark: true);
                Complete(); break;
            case StoryPeople or StoryNames or StoryResetStandingRegistration:
                //These source steps register parameters and actors. They do
                //not reveal all registered characters before they speak.
                Complete(); break;
            case StoryHideMessage:
                binding.View.HideMessage();
                binding.View.BeginWait(0, Complete); break;
            case CampaignStoryInputWait:
                bool bypassInput = binding.Rules.ReadStoryInputWaitBypassCurrent() ??
                    throw new InvalidDataException("story.input_wait.application_owner");
                if (bypassInput) Complete();
                else binding.View.BeginInputWait(() => _preferences.AutomaticMessageAdvance, Complete);
                break;
            case StoryWait wait:
                binding.View.BeginSourceWait(binding.SkipRequested || binding.FastRequested ? 0 : wait.Milliseconds, Complete);
                break;
            case StoryMessage or StoryNarration or StoryFormatOnlyMessage:
                if (step is StoryFormatOnlyMessage) binding.View.HideMessage();
                if (!binding.Session.TryPrepareCampaignStoryStep(binding.World, binding.Rules, out var prepared) ||
                    prepared.Prepared?.Text is not { } text)
                    throw new InvalidDataException(prepared.UnresolvedOwner ?? "story.text.prepare");
                binding.World = StoryOwner(binding.Session);
                if (binding.World.CampaignAppRandom32 is { } app) _presentationRandom.InstallCommittedState(app.State);
                SynchronizeApplicationCrtRandom(binding.World);
                if (text.FormatOnly)
                {
                    //Function4 submits sound during formatting, before the
                    //invalid-speaker display gate. It is nonblocking and is
                    //not owned by a message wait which immediately retires.
                    if (text.Voices.Length != 0)
                    {
                        if (_sceneAudioBinding is not { } formatAudio) throw new InvalidDataException("story.voice.scene_owner");
                        foreach (var voice in text.Voices) formatAudio.PlayPersonnelNarrationVoice(voice);
                    }
                    Complete(); break;
                }
                ApplyStoryActors(binding, PendingCampaignStory(binding.Session)!);
                if (text.Voices.Length != 0)
                {
                    if (_sceneAudioBinding is null) throw new InvalidDataException("story.voice.scene_owner");
                    binding.OwnsVoice = true;
                    foreach (var voice in text.Voices) _sceneAudioBinding.PlayPersonnelNarrationVoice(voice);
                }
                binding.View.PresentMessage(text.SpeakerName, text.Text, Complete, () => _preferences.AutomaticMessageAdvance, text.SpeechOnly);
                break;
            case StoryPoemEvent poem:
                OpenStoryPoem(binding,poem,story.Sequence,stepIndex); break;
            case StoryBackground background:
                binding.View.SetBackground(background.IllustrationId, background.ResourceKind);
                if (background.FadeIn) binding.View.BeginTransition(true, SourceStoryDuration(background.TransitionSetting, 500), Complete);
                else binding.View.BeginWait(0, Complete);
                break;
            case StoryIllustration illustration:
                binding.View.SetIllustration(illustration.IllustrationId);
                binding.View.BeginWait(0, Complete); break;
            case StorySmallIllustration small:
                binding.View.SetSmallIllustration(small.IllustrationId);
                binding.View.BeginWait(0, Complete); break;
            case StoryItemIllustration item:
                binding.View.SetItemIllustration(item.IllustrationId,
                    _sceneAudioBinding?.EventPlaybackAudioSuppressed ?? false);
                binding.View.BeginWait(0, Complete); break;
            case StoryOwnedEffect:
                _ = ExecuteStoryEffectsAsync(binding, story); break;
            case StoryTutorialIllustration tutorial:
                binding.View.SetTutorialIllustration(tutorial.IllustrationId);
                binding.View.BeginWait(0, Complete); break;
            case StoryHideCharacters hide:
                binding.View.HideCharacters(hide.Characters);
                binding.View.BeginWait(0, Complete); break;
            case StoryTransition transition:
                binding.View.BeginTransition(transition.FadeIn, SourceStoryDuration(transition.Setting, 500), Complete); break;
            case StoryCamera camera:
                BeginStoryCamera(binding, camera, Complete); break;
            case StoryRestoreCampaignCamera restore:
                if (!binding.Map.RestoreStoryCamera(binding, restore.Snapshot))
                    throw new InvalidDataException("story.camera.restore_request");
                binding.View.BeginWait(0, Complete, () => binding.Map.IsStoryCameraIdle(binding)); break;
            case StoryAudio audio:
                _ = PresentStoryAudioAsync(binding, audio, story.Sequence, stepIndex); break;
            default:
                throw new InvalidDataException("story.presentation.unsupported_step." + step.GetType().Name);
        }
    }

    private static int SourceStoryDuration(int setting, int defaultMilliseconds) => setting < 0 ? defaultMilliseconds : setting;

    private void BeginStoryCamera(StoryPresentationBinding binding, StoryCamera camera, Action complete)
    {
        void Blackout() => binding.View.BeginTransition(false, SourceStoryDuration(camera.BlackoutSetting, 500),
            () => binding.View.BeginWait(camera.WaitMilliseconds, complete));
        if (camera.HexId is not int hex) { Blackout(); return; }
        var query = binding.Rules.QueryStoryCameraTarget(binding.World, hex);
        int milliseconds = SourceStoryDuration(camera.CameraSetting, 1500);
        if (!query.Available || !binding.Map.MoveStoryCamera(binding, query.Value!, milliseconds))
            throw new InvalidDataException(query.UnresolvedInput ?? "story.camera.request_owner");
        binding.View.BeginWait(milliseconds, Blackout, () => binding.Map.IsStoryCameraIdle(binding));
    }

    private async Task PresentStoryAudioAsync(StoryPresentationBinding binding, StoryAudio audio, long sequence, int step)
    {
        try
        {
            if (audio.AudioKind is (StoryAudioKind.SoundIfNotSkipped or StoryAudioKind.FadeInSoundIfNotSkipped) && binding.SkipRequested)
            {
                //The first source manager call is conditional on this actual
                //playback manager's skip latch at arrival, not at staging.
                CompleteStoryStep(binding, sequence, step); return;
            }
            if (_sceneAudioBinding is not { } owner) throw new InvalidDataException("story.audio.scene_owner");
            if (audio.AudioKind is StoryAudioKind.Music or StoryAudioKind.DirectMusic or StoryAudioKind.FadeOutMusic)
                await owner.PlayStoryMusicAsync(audio, _ui.SourceLocale, _preferences.MusicSelection, binding.Cancellation.Token);
            else await owner.PlayStorySoundAsync(audio, binding.Cancellation.Token);
            if (IsStoryCurrent(binding)) RunUiAction(() => CompleteStoryStep(binding, sequence, step));
        }
        catch (OperationCanceledException) when (binding.Cancellation.IsCancellationRequested) { }
        catch (Exception error)
        { if (IsStoryCurrent(binding)) RunUiAction(() => ReportPlayFailure(error)); }
    }

    private bool RequestStoryMessageMode(StoryPresentationBinding binding, int action)
    {
        if (!StoryInputAdmitted(binding)) return false;
        if (action == 23) binding.SkipRequested = true;
        else if (action == 24) binding.FastRequested = !binding.FastRequested;
        else return false;
        binding.View.SetMeta("story_skip_requested", binding.SkipRequested);
        binding.View.SetMeta("story_fast_requested", binding.FastRequested);
        return true;
    }

    private void CompleteStoryStep(StoryPresentationBinding binding, long sequence, int step,
        StoryOwnedEffectReceipt? effects = null)
    {
        if (!IsStoryCurrent(binding)) return;
        if (binding.Log is not null) { binding.AfterLog = () => CompleteStoryStep(binding, sequence, step, effects); return; }
        if (!binding.Session.TryCompleteCampaignStoryStep(binding.World, new CampaignStoryStepReceipt(sequence, step) { OwnedEffectResult = effects })) return;
        if (binding.OwnsVoice) { _sceneAudioBinding?.StopNarrationVoice(); binding.OwnsVoice = false; }
        binding.World = StoryOwner(binding.Session);
        //Do not recurse through a long parameter-only sequence, and do not
        //let the input that completed a page reach its successor's wait.
        Callable.From(() => RunUiAction(() => PumpStoryStep(binding))).CallDeferred();
    }

    private void StartStoryMediaRestoration(StoryPresentationBinding binding, CampaignStoryRequest story)
        => binding.MediaRestoration = RestoreStoryMediaAsync(binding, story);

    private async Task RestoreStoryMediaAsync(StoryPresentationBinding binding, CampaignStoryRequest story)
    {
        try
        {
            if (story.Scene.OwnedEffects is { Length: > 0 })
            {
                var result = await binding.Map.RestoreStoryEffectsAsync(binding,
                    binding.View.OwnedEffectOutput, story, binding.Cancellation.Token);
                if (result.Receipt is null) throw new InvalidDataException(result.UnresolvedOwner ?? "story.effects.restore_owner");
            }
            if (!IsStoryCurrent(binding)) return;
            if (story.Scene.Music is { } music)
            {
                var audio = _sceneAudioBinding ?? throw new InvalidDataException("story.audio.restore_scene_owner");
                await audio.PlayStoryMusicAsync(music, _ui.SourceLocale, _preferences.MusicSelection, binding.Cancellation.Token);
            }
            if (IsStoryCurrent(binding))
            {
                if (StoryPlayback(binding.Session)?.Phase == NormalTurnStoryResultPhase.PostResult)
                {
                    //Defer until this task has actually completed and has been
                    //assigned to MediaRestoration, including synchronous loads.
                    Callable.From(() => RunUiAction(() =>
                    {
                        if (!IsStoryCurrent(binding) ||
                            StoryPlayback(binding.Session) is not { Phase: NormalTurnStoryResultPhase.PostResult } actual ||
                            actual.Story.Source != binding.Source || actual.Story.Sequence != binding.Sequence ||
                            actual.Story.ContentRevision != binding.ContentRevision ||
                            !binding.MediaRestoration.IsCompletedSuccessfully) return;
                        TryOpenPersonnelPresentation();
                    })).CallDeferred();
                }
                else RunUiAction(() => PumpStoryStep(binding));
            }
        }
        catch (OperationCanceledException) when (binding.Cancellation.IsCancellationRequested) { }
        catch (Exception error)
        {
            if (IsStoryCurrent(binding)) RunUiAction(() => ReportPlayFailure(error));
            throw;
        }
    }

    private async Task ExecuteStoryEffectsAsync(StoryPresentationBinding binding, CampaignStoryRequest story)
    {
        try
        {
            var result = await binding.Map.ExecuteStoryEffectAsync(binding,binding.View.OwnedEffectOutput,story,binding.Cancellation.Token);
            if (result.Receipt is null) throw new InvalidDataException(result.UnresolvedOwner ?? "story.effects.execute_owner");
            if (IsStoryCurrent(binding)) RunUiAction(() => CompleteStoryStep(binding,story.Sequence,story.NextStep,result.Receipt));
        }
        catch (OperationCanceledException) when (binding.Cancellation.IsCancellationRequested) { }
        catch (Exception error)
        { if (IsStoryCurrent(binding)) RunUiAction(() => ReportPlayFailure(error)); }
    }

    private async Task RetireStoryPresentationAsync(StoryPresentationBinding binding)
    {
        try
        { if (GodotObject.IsInstanceValid(binding.Map)) await binding.Map.RetireStoryEffectsAsync(binding); }
        catch (Exception error) { GD.PushError(error.ToString()); }
        finally
        {
            if (GodotObject.IsInstanceValid(binding.View))
            { binding.View.GetParent()?.RemoveChild(binding.View); binding.View.QueueFree(); }
        }
    }

    private void CloseStoryPresentation()
    {
        var binding = _storyPresentation; _storyPresentation = null;
        if (binding is null) return;
        CloseStoryLog(binding, resume: false);
        CloseStoryPoem(binding,restore:false);
        binding.Cancellation.Cancel(); binding.Cancellation.Dispose();
        if (binding.OwnsVoice) _sceneAudioBinding?.StopNarrationVoice();
        if (_sceneAudioBinding is { } audio) audio.StorySoundSuppressed = null;
        binding.View.Abandon(); _ = RetireStoryPresentationAsync(binding);
        if (GodotObject.IsInstanceValid(binding.Map) && ReferenceEquals(_residentMap, binding.Map))
        {
            binding.Map.EndStoryPresentation(binding);
            if (!_failed && !_shuttingDown) SetMapFocus(binding.ReturnMapFocus);
        }
    }

    private void OpenStoryLog(StoryPresentationBinding binding)
    {
        if (!StoryInputAdmitted(binding) || binding.View.Message is not { Visible: true } ||
            PendingCampaignStory(binding.Session) is not { } story) return;
        var messages = story.TextLog.AsEnumerable();
        if (story.PreparedStep?.Text is { } current) messages = messages.Append(current);
        var sprites = new LocalizedUiSpriteSource(_repository!);
        var entries = messages.Select(message =>
        {
            LoadedSprite? face = null;
            if (message.Speaker is { } speaker)
            {
                int appearance = speaker.PortraitAppearanceIndex ??
                    throw new InvalidDataException("story.log.source_appearance");
                var route = _repository!.ResolveFacePortrait(appearance);
                face = sprites.LoadTexturePackSprite(route.ResourceName, route.SpriteName);
            }
            return new StoryLogVisual(message.SpeakerName, message.Text, face);
        }).ToArray();
        var log = new StoryLogView { Name = "StoryLog" }; binding.Log = log; binding.View.AddChild(log);
        try
        {
            log.Configure(_repository!, _ui, entries,
                () => IsStoryCurrent(binding) && CanAdvanceNarration && !_advancingPeriod && binding.Log == log,
                () => RunUiAction(() => CloseStoryLog(binding)), CurrentListScrollSpeed);
        }
        catch { CloseStoryLog(binding); throw; }
    }

    private void CloseStoryLog(StoryPresentationBinding binding, bool resume = true)
    {
        var log = binding.Log; binding.Log = null;
        if (log is not null) { log.Abandon(); log.GetParent()?.RemoveChild(log); log.QueueFree(); }
        var after = binding.AfterLog; binding.AfterLog = null;
        if (resume && IsStoryCurrent(binding)) after?.Invoke();
    }
}
