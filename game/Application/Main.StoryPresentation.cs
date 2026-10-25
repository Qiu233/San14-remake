using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private sealed class StoryPresentationBinding(GameplaySession session, GameState world,
        MapSceneView map, ArmyCampaignRuleAdapter rules, StoryPresentationView view)
    {
        internal readonly GameplaySession Session = session;
        internal GameState World = world;
        internal readonly MapSceneView Map = map;
        internal readonly ArmyCampaignRuleAdapter Rules = rules;
        internal readonly StoryPresentationView View = view;
        internal readonly CancellationTokenSource Cancellation = new();
        internal int StartedStep = -1;
        internal bool OwnsVoice;
        internal bool SkipRequested, FastRequested;
        internal StoryLogView? Log;
        internal Action? AfterLog;
    }
    private StoryPresentationBinding? _storyPresentation;
    private bool HasStoryPresentation => _storyPresentation is not null;
    private bool IsStoryPresentationCurrent => _storyPresentation is { } binding && IsStoryCurrent(binding);
    private bool IsStoryCurrent(StoryPresentationBinding binding) =>
        ReferenceEquals(_storyPresentation, binding) && ReferenceEquals(_session, binding.Session) &&
        ReferenceEquals(_residentMap, binding.Map) &&
        ReferenceEquals(binding.Session.PendingNormalPeriod?.Candidate, binding.World);
    private bool StoryInputAdmitted(StoryPresentationBinding binding) => IsStoryCurrent(binding) &&
        CanAdvanceNarration && !_advancingPeriod && binding.Log is null;

    private bool TryOpenStoryPresentation(GameplaySession session, NormalPeriodOperation pending,
        CampaignComposition composition, MapSceneView map)
    {
        if (pending.PendingStory is null) return false;
        //An unhandled earlier source effect is still a queue head. Do not
        //skip it merely because a later event has already been prepared.
        if (PersonnelPresentationQueue.Next(pending.Candidate) is not null || HasForcePresentation) return false;
        var rules = composition.PrepareArmyQueries(pending.Candidate);
        rules.ConfigurePersonnelNarrationVoice(_preferences.Voice.Capture());
        if (map.SourceMainCameraTerrain is { } terrain) rules.ConfigureStoryCameraTerrain(terrain);
        var view = new StoryPresentationView { KeyboardInput = HandleGameplayNarrationKeyboard };
        var binding = new StoryPresentationBinding(session, pending.Candidate, map, rules, view);
        view.MessageModeRequested = action => RequestStoryMessageMode(binding, action);
        view.ReadAccelerated = () => binding.SkipRequested || binding.FastRequested;
        view.OpenLog = () => RunUiAction(() => OpenStoryLog(binding));
        _storyPresentation = binding; AddChild(view);
        if (_sceneAudioBinding is { } audio)
            //The current Story set contains presentation steps only. Its
            //actual manager constructs nested-Exec suppression at zero;
            //future business Exec scopes must supply their own enter/leave.
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
            if (pending.PendingStory.Scene.Music is { } music) _ = RestoreStoryMusicAsync(binding, music);
            else PumpStoryStep(binding);
            return true;
        }
        catch { CloseStoryPresentation(); throw; }
    }

    private void RestoreStoryScene(StoryPresentationBinding binding)
    {
        var story = binding.Session.PendingNormalPeriod!.PendingStory!;
        var scene = story.Scene;
        binding.View.SetBackground(scene.Background?.IllustrationId ?? 0);
        binding.View.SetIllustration(scene.Illustration?.IllustrationId ?? 0);
        binding.View.RestoreBlackout(scene.Blackout is { FadeIn: false });
        ApplyStoryActors(binding, story);
        if (scene.PreparedText is { } text) binding.View.RestoreMessage(text.SpeakerName, text.Text);
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

    private void ApplyStoryActors(StoryPresentationBinding binding, CampaignStoryRequest story)
    {
        var query = binding.Rules.QueryStoryPresentation(binding.World, story);
        var publication = query.Available ? query.Value! :
            throw new InvalidDataException(query.UnresolvedInput ?? "story.presentation.publication");
        var sprites = new LocalizedUiSpriteSource(_repository!);
        var actors = publication.Actors.Select(actor =>
        {
            if (actor.UnresolvedOwner is not null || !actor.StandingPortrait.Available || actor.StandingPortrait.Value is not { } route)
                throw new InvalidDataException(actor.UnresolvedOwner ?? actor.StandingPortrait.UnresolvedInput ?? "story.presentation.actor");
            var layers = new[] { route.SpriteB, route.SpriteA, route.SpriteC }
                .Select(name => sprites.LoadTexturePackSprite(route.ResourceName, name)).ToArray();
            return new StoryActorVisual(actor.Slot, actor.Speaker.OfficerId, layers);
        }).ToArray();
        binding.View.ApplyActors(actors, publication.HighlightedSlot);
    }

    private void PumpStoryStep(StoryPresentationBinding binding)
    {
        if (!IsStoryCurrent(binding)) return;
        var story = binding.Session.PendingNormalPeriod?.PendingStory;
        if (story is null)
        {
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
            case StoryPeople or StoryNames or StoryStanding:
                //These source steps register parameters and actors. They do
                //not reveal all registered characters before they speak.
                Complete(); break;
            case StoryMessage or StoryNarration:
                if (!binding.Session.TryPrepareNormalTurnStoryStep(binding.World, binding.Rules, out var prepared) ||
                    prepared.Prepared?.Text is not { } text)
                    throw new InvalidDataException(prepared.UnresolvedOwner ?? "story.text.prepare");
                binding.World = binding.Session.PendingNormalPeriod!.Candidate;
                if (binding.World.CampaignAppRandom32 is { } app) _presentationRandom.InstallCommittedState(app.State);
                SynchronizeApplicationCrtRandom(binding.World);
                ApplyStoryActors(binding, binding.Session.PendingNormalPeriod.PendingStory!);
                if (text.Voices.Length != 0)
                {
                    if (_sceneAudioBinding is null) throw new InvalidDataException("story.voice.scene_owner");
                    binding.OwnsVoice = true;
                    foreach (var voice in text.Voices) _sceneAudioBinding.PlayPersonnelNarrationVoice(voice);
                }
                binding.View.PresentMessage(text.SpeakerName, text.Text, Complete, () => _preferences.AutomaticMessageAdvance);
                break;
            case StoryBackground background:
                binding.View.SetBackground(background.IllustrationId);
                binding.View.BeginTransition(true, SourceStoryDuration(background.TransitionSetting, 500), Complete); break;
            case StoryIllustration illustration:
                binding.View.SetIllustration(illustration.IllustrationId);
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
            if (audio.AudioKind == StoryAudioKind.SoundIfNotSkipped && binding.SkipRequested)
            {
                //The first source manager call is conditional on this actual
                //playback manager's skip latch at arrival, not at staging.
                CompleteStoryStep(binding, sequence, step); return;
            }
            if (_sceneAudioBinding is not { } owner) throw new InvalidDataException("story.audio.scene_owner");
            if (audio.AudioKind == StoryAudioKind.Music)
                await owner.PlayStoryMusicAsync(audio, _ui.SourceLocale, _preferences.MusicSelection, binding.Cancellation.Token);
            else await owner.PlayStorySoundAsync(audio, binding.Cancellation.Token);
            if (IsStoryCurrent(binding)) RunUiAction(() => CompleteStoryStep(binding, sequence, step));
        }
        catch (OperationCanceledException) when (binding.Cancellation.IsCancellationRequested) { }
        catch (Exception error)
        { if (IsStoryCurrent(binding)) RunUiAction(() => ReportPlayFailure(error)); }
    }

    private async Task RestoreStoryMusicAsync(StoryPresentationBinding binding, StoryAudio music)
    {
        try
        {
            if (_sceneAudioBinding is not { } owner) throw new InvalidDataException("story.audio.restore_scene_owner");
            await owner.PlayStoryMusicAsync(music, _ui.SourceLocale, _preferences.MusicSelection, binding.Cancellation.Token);
            //Restoring retained media is not another receipt for its old step.
            if (IsStoryCurrent(binding)) RunUiAction(() => PumpStoryStep(binding));
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

    private void CompleteStoryStep(StoryPresentationBinding binding, long sequence, int step)
    {
        if (!IsStoryCurrent(binding)) return;
        if (binding.Log is not null) { binding.AfterLog = () => CompleteStoryStep(binding, sequence, step); return; }
        if (!binding.Session.TryCompleteNormalTurnStoryStep(binding.World, sequence, step)) return;
        if (binding.OwnsVoice) { _sceneAudioBinding?.StopNarrationVoice(); binding.OwnsVoice = false; }
        binding.World = binding.Session.PendingNormalPeriod!.Candidate;
        //Do not recurse through a long parameter-only sequence, and do not
        //let the input that completed a page reach its successor's wait.
        Callable.From(() => RunUiAction(() => PumpStoryStep(binding))).CallDeferred();
    }

    private void CloseStoryPresentation()
    {
        var binding = _storyPresentation; _storyPresentation = null;
        if (binding is null) return;
        CloseStoryLog(binding, resume: false);
        binding.Cancellation.Cancel(); binding.Cancellation.Dispose();
        if (binding.OwnsVoice) _sceneAudioBinding?.StopNarrationVoice();
        if (_sceneAudioBinding is { } audio) audio.StorySoundSuppressed = null;
        binding.View.Abandon(); binding.View.GetParent()?.RemoveChild(binding.View); binding.View.QueueFree();
        if (GodotObject.IsInstanceValid(binding.Map) && ReferenceEquals(_residentMap, binding.Map))
        {
            binding.Map.EndStoryPresentation(binding);
            if (!_failed && !_shuttingDown) SetMapFocus(_mapFocused);
        }
    }

    private void OpenStoryLog(StoryPresentationBinding binding)
    {
        if (!StoryInputAdmitted(binding) || binding.View.Message is not { Visible: true } ||
            binding.Session.PendingNormalPeriod?.PendingStory is not { } story) return;
        var messages = story.TextLog.AsEnumerable();
        if (story.PreparedStep?.Text is { } current) messages = messages.Append(current);
        var sprites = new LocalizedUiSpriteSource(_repository!);
        var entries = messages.Select(message =>
        {
            LoadedSprite? face = null;
            if (message.Speaker is { } speaker)
            {
                var query = binding.Rules.QueryPersonnelPresentationSpeaker(binding.World, speaker);
                if (query.Value?.Face2Portrait is { Kind: OfficerPortraitRouteKind.StockNamed, ResourceName: not null, SpriteName: not null } route)
                    face = sprites.LoadTexturePackSprite(route.ResourceName, route.SpriteName);
                else throw new InvalidDataException(query.Value?.PortraitUnresolvedInput ?? query.UnresolvedInput ?? "story.log.face");
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
