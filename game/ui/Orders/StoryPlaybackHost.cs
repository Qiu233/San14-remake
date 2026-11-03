using Godot;
using San14.Core;
using San14.Data;

/// <summary>The actual story owner supplies state transitions. This canvas
/// consumes media and input without assuming a normal-period continuation.</summary>
internal sealed record StoryPlaybackAccess(Func<CampaignStoryRequest?> ReadStory,
    Action PrepareText, Func<CampaignStoryStepReceipt,bool> CompleteStep,
    Func<CampaignStoryRequest,AreaAdministrationQuery<StoryPresentationPublication>> QueryPresentation)
{
    internal Func<GameState>? ReadWorld { get; init; }
    internal Func<bool?>? ReadInputWaitBypass { get; init; }
    internal Func<int,AreaAdministrationQuery<StoryCameraTarget>>? QueryCameraTarget { get; init; }

    internal StoryPlaybackAccess(ArmyCampaignRuleAdapter rules,Func<GameState> readWorld,
        Func<CampaignStoryRequest?> readStory,Action prepareText,Func<CampaignStoryStepReceipt,bool> completeStep)
        : this(readStory,prepareText,completeStep,
            story => rules.QueryStoryPresentation(readWorld(),story))
    {
        ReadWorld=readWorld;
        ReadInputWaitBypass=rules.ReadStoryInputWaitBypassCurrent;
        QueryCameraTarget=hex => rules.QueryStoryCameraTarget(readWorld(),hex);
    }
}

internal partial class StoryPlaybackHost : Control
{
    private GameResourceRepository repository = null!;
    private MapSystemMenuPreparedResources preparedUiResources = null!;
    private PreviewText text = null!;
    private StoryPlaybackAccess access = null!;
    private MapSceneView? map;
    private SceneAudioBinding audio = null!;
    private StoryPresentationView scene = null!;
    private Func<bool> admitted = null!;
    private Func<AutomaticMessageAdvanceMode> automaticAdvance = null!;
    private Func<int> scrollSpeed = null!;
    private Action? completed;
    private Action<Exception>? failed;
    private Action<GameState>? prepared;
    private readonly CancellationTokenSource lifetime = new();
    private StoryLogView? log;
    private Action? afterLog;
    private long startedSequence = -1;
    private int startedStep = -1;
    private bool abandoned, ownsVoice, skipped, accelerated, returningToEditor;
    private Task? editorReturn;
    private readonly TaskCompletionSource mediaRestoration = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal Task MediaRestoration => mediaRestoration.Task;
    internal DeploymentNarrationView? Message => scene?.Message;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        StoryPlaybackAccess access, MapSceneView? map, SceneAudioBinding audio, MapMusicPreferences musicPreferences,
        Func<bool> admitted, Func<AutomaticMessageAdvanceMode> automaticAdvance,
        Func<int> scrollSpeed, Func<InputEvent, DeploymentNarrationView?, bool> keyboardInput,
        Action<GameState>? prepared, Action completed, Action<Exception> failed,
        MapSystemMenuPreparedResources preparedUiResources,
        Func<InputEvent, Func<int, bool>, bool>? inputWaitKeyboard = null)
    {
        this.repository = repository; this.text = text; this.access = access; this.map = map;
        this.preparedUiResources = preparedUiResources;
        this.audio = audio; this.admitted = admitted; this.automaticAdvance = automaticAdvance;
        this.musicPreferences = musicPreferences;
        // This playback manager is freshly constructed; source 1B0 starts
        // clear. The distinct skip latch is consumed by its own audio step.
        audio.StorySoundSuppressed = () => abandoned ? null : false;
        this.scrollSpeed = scrollSpeed; this.prepared = prepared; this.completed = completed; this.failed = failed;
        Name = "StoryPlaybackHost"; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        scene = new(); AddChild(scene);
        scene.KeyboardInput = input => keyboardInput(input, scene.Message);
        scene.MessageModeRequested = RequestMessageMode;
        Func<int, bool> requestMode = scene.RequestInputWaitAction;
        scene.InputWaitKeyboard = input => inputWaitKeyboard?.Invoke(input, requestMode) ?? false;
        scene.ReadAccelerated = () => skipped || accelerated;
        scene.OpenLog = OpenLog;
        scene.Configure(repository, text, CanUse, preparedUiResources.CreateChromeResources());
        map?.BeginStoryPresentation(this);
        RestoreScene();
        if (access.ReadStory() is { } restore && restore.Scene.OwnedEffects is { Length: > 0 })
            _ = RestoreEffectsAndStart(restore);
        else StartRestored();
    }

    private bool CanUse() => !abandoned && IsInsideTree() && IsVisibleInTree() && admitted() && log is null && poem is null;

    private MapSceneView RequireMap() => map ?? throw new InvalidDataException("story.presentation.map_owner");

    private void RestoreScene()
    {
        if (access.ReadStory() is not { } story) return;
        scene.SetBackground(story.Scene.Background?.IllustrationId ?? 0, story.Scene.Background?.ResourceKind ?? 15);
        scene.SetIllustration(story.Scene.Illustration?.IllustrationId ?? 0);
        scene.SetSmallIllustration(story.Scene.SmallIllustration?.IllustrationId ?? 0);
        scene.SetTutorialIllustration(story.Scene.TutorialIllustration?.IllustrationId ?? 0);
        scene.SetItemIllustration(story.Scene.ItemIllustration?.IllustrationId);
        scene.RestoreBlackout(story.Scene.Blackout is { FadeIn: false });
        ApplyActors(story);
        if (story.Scene.PreparedText is { } current) scene.RestoreMessage(current.SpeakerName, current.Text, current.SpeechOnly);
        if (story.Scene.RestoredCamera is { } restored)
        {
            if (!RequireMap().RestoreStoryCamera(this, restored, immediate: true))
                throw new InvalidDataException("story.camera.restore_scene_owner");
        }
        else if (story.Scene.Camera is { HexId: int hex })
        {
            var target = access.QueryCameraTarget?.Invoke(hex) ?? throw new InvalidDataException("story.camera.query_owner");
            if (!target.Available || !RequireMap().MoveStoryCamera(this, target.Value!, 0))
                throw new InvalidDataException(target.UnresolvedInput ?? "story.camera.restore_scene_request");
        }
    }

    private void ApplyActors(CampaignStoryRequest story)
    {
        var query = access.QueryPresentation(story);
        var publication = query.Available ? query.Value! :
            throw new InvalidDataException(query.UnresolvedInput ?? "story.presentation.publication");
        var portraits = preparedUiResources.Assets.Portraits;
        var actors = publication.Actors.Select(actor =>
        {
            if (actor.UnresolvedOwner is not null || !actor.StandingPortrait.Available ||
                actor.StandingPortrait.Value is not { } route)
                throw new InvalidDataException(actor.UnresolvedOwner ?? actor.StandingPortrait.UnresolvedInput ?? "story.presentation.actor");
            var layers = route.LoadSources(portraits.RequireSource);
            return new StoryActorVisual(actor.Slot, actor.Speaker.OfficerId, layers) { Dark = actor.Dark };
        }).ToArray();
        scene.ApplyActors(actors, publication.HighlightedSlot, publication.PortraitDarkFlags);
    }

    private void Pump()
    {
        if (!CanUse() || returningToEditor) return;
        try
        {
            var story = access.ReadStory();
            if (story is null || story.NextStep == story.Steps.Length) { completed?.Invoke(); return; }
            int stepIndex = story.NextStep;
            if (startedSequence == story.Sequence && startedStep == stepIndex) return;
            startedSequence = story.Sequence; startedStep = stepIndex;
            void Complete() => CompleteStep(story.Sequence, stepIndex);
            switch (story.Steps[stepIndex])
            {
                case StoryStanding:
                    ApplyActors(story); Complete(); break;
                case StoryPeople or StoryNames or StoryResetStandingRegistration:
                    Complete(); break;
                case StoryHideMessage:
                    scene.HideMessage(); scene.BeginWait(0, Complete); break;
                case CampaignStoryInputWait:
                    bool bypassInput = access.ReadInputWaitBypass?.Invoke() ??
                        throw new InvalidDataException("story.input_wait.application_owner");
                    if (bypassInput) Complete();
                    else scene.BeginInputWait(automaticAdvance, Complete);
                    break;
                case StoryWait wait:
                    scene.BeginSourceWait(skipped || accelerated ? 0 : wait.Milliseconds, Complete); break;
                case StoryHideCharacters hide:
                    scene.HideCharacters(hide.Characters); scene.BeginWait(0, Complete); break;
                case StoryMessage or StoryNarration or StoryFormatOnlyMessage:
                    if (story.Steps[stepIndex] is StoryFormatOnlyMessage) scene.HideMessage();
                    access.PrepareText();
                    var preparedStory = access.ReadStory()!;
                    var current = preparedStory.PreparedStep?.Text ?? throw new InvalidDataException("story.text.prepare");
                    if (prepared is { } notify)
                        notify((access.ReadWorld ?? throw new InvalidDataException("story.presentation.prepared_world_owner"))());
                    if (current.FormatOnly)
                    {
                        foreach (var voice in current.Voices) audio.PlayPersonnelNarrationVoice(voice);
                        Complete(); break;
                    }
                    ApplyActors(preparedStory);
                    if (current.Voices.Length != 0)
                    {
                        ownsVoice = true;
                        foreach (var voice in current.Voices) audio.PlayPersonnelNarrationVoice(voice);
                    }
                    scene.PresentMessage(current.SpeakerName, current.Text, Complete, automaticAdvance, current.SpeechOnly);
                    break;
                case StoryPoemEvent poemRequest:
                    OpenPoem(poemRequest,story.Sequence,stepIndex);break;
                case StoryBackground background:
                    scene.SetBackground(background.IllustrationId, background.ResourceKind);
                    if (background.FadeIn) scene.BeginTransition(true, Duration(background.TransitionSetting, 500), Complete);
                    else scene.BeginWait(0, Complete);
                    break;
                case StoryIllustration illustration:
                    scene.SetIllustration(illustration.IllustrationId); scene.BeginWait(0, Complete); break;
                case StorySmallIllustration small:
                    scene.SetSmallIllustration(small.IllustrationId); scene.BeginWait(0, Complete); break;
                case StoryItemIllustration item:
                    scene.SetItemIllustration(item.IllustrationId, audio.EventPlaybackAudioSuppressed);
                    scene.BeginWait(0, Complete); break;
                case StoryOwnedEffect:
                    _ = ExecuteEffects(story); break;
                case StoryTutorialIllustration tutorial:
                    scene.SetTutorialIllustration(tutorial.IllustrationId); scene.BeginWait(0, Complete); break;
                case StoryTransition transition:
                    scene.BeginTransition(transition.FadeIn, Duration(transition.Setting, 500), Complete); break;
                case StoryCamera camera:
                    BeginCamera(camera, Complete); break;
                case StoryRestoreCampaignCamera restore:
                    if (!RequireMap().RestoreStoryCamera(this, restore.Snapshot))
                        throw new InvalidDataException("story.camera.restore_request_owner");
                    scene.BeginWait(0, Complete, () => RequireMap().IsStoryCameraIdle(this)); break;
                case StoryAudio sound:
                    _ = PresentAudio(sound, story.Sequence, stepIndex); break;
                default:
                    throw new InvalidDataException("story.presentation.unsupported_step." + story.Steps[stepIndex].GetType().Name);
            }
        }
        catch (Exception error) { Fail(error); }
    }

    private static int Duration(int setting, int fallback) => setting < 0 ? fallback : setting;

    private void BeginCamera(StoryCamera camera, Action completed)
    {
        void Blackout() => scene.BeginTransition(false, Duration(camera.BlackoutSetting, 500),
            () => scene.BeginWait(camera.WaitMilliseconds, completed));
        if (camera.HexId is not int hex) { Blackout(); return; }
        var target = access.QueryCameraTarget?.Invoke(hex) ?? throw new InvalidDataException("story.camera.query_owner");
        int milliseconds = Duration(camera.CameraSetting, 1500);
        if (!target.Available || !RequireMap().MoveStoryCamera(this, target.Value!, milliseconds))
            throw new InvalidDataException(target.UnresolvedInput ?? "story.camera.request_owner");
        scene.BeginWait(milliseconds, Blackout, () => RequireMap().IsStoryCameraIdle(this));
    }

    private async Task PresentAudio(StoryAudio sound, long sequence, int step)
    {
        try
        {
            if (sound.AudioKind is not (StoryAudioKind.SoundIfNotSkipped or StoryAudioKind.FadeInSoundIfNotSkipped) || !skipped)
            {
                if (sound.AudioKind is StoryAudioKind.Music or StoryAudioKind.DirectMusic or StoryAudioKind.FadeOutMusic)
                    await audio.PlayStoryMusicAsync(sound, text.SourceLocale, musicPreferences!, lifetime.Token);
                else await audio.PlayStorySoundAsync(sound, lifetime.Token);
            }
            if (!abandoned) CompleteStep(sequence, step);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception error) { Fail(error); }
    }

    private void StartRestored()
    {
        if (abandoned) return;
        if (log is not null) { afterLog = StartRestored; return; }
        if (access.ReadStory()?.Scene.Music is { } music) _ = RestoreMusic(music);
        else { mediaRestoration.TrySetResult(); Pump(); }
    }

    private async Task RestoreEffectsAndStart(CampaignStoryRequest story)
    {
        try
        {
            var result=await RequireMap().RestoreStoryEffectsAsync(this,scene.OwnedEffectOutput,story,lifetime.Token);
            if (result.Receipt is null) throw new InvalidDataException(result.UnresolvedOwner ?? "story.effects.restore_owner");
            if (!abandoned) StartRestored();
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception error) { mediaRestoration.TrySetException(error); if (!abandoned) Fail(error); }
    }

    private async Task ExecuteEffects(CampaignStoryRequest story)
    {
        try
        {
            var result=await RequireMap().ExecuteStoryEffectAsync(this,scene.OwnedEffectOutput,story,lifetime.Token);
            if (result.Receipt is null) throw new InvalidDataException(result.UnresolvedOwner ?? "story.effects.execute_owner");
            if (!abandoned) CompleteStep(story.Sequence,story.NextStep,result.Receipt);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception error) { if (!abandoned) Fail(error); }
    }

    private async Task RetireEffects()
    {
        try { if (map is { } owner && GodotObject.IsInstanceValid(owner)) await owner.RetireStoryEffectsAsync(this); }
        catch (Exception error) { GD.PushError(error.ToString()); }
    }

    private MapMusicPreferences musicPreferences = null!;
    internal void ContinuePlayback() => Pump();

    private async Task RestoreMusic(StoryAudio music)
    {
        try
        {
            await audio.PlayStoryMusicAsync(music, text.SourceLocale, musicPreferences!, lifetime.Token);
            if (!abandoned) { mediaRestoration.TrySetResult(); Pump(); }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception error) { mediaRestoration.TrySetException(error); Fail(error); }
    }

    private bool RequestMessageMode(int action)
    {
        if (!CanUse() || returningToEditor) return false;
        if (action == 23) skipped = true;
        else if (action == 24) accelerated = !accelerated;
        else return false;
        SetMeta("story_skip_requested", skipped); SetMeta("story_fast_requested", accelerated);
        return true;
    }
    internal void RequestSkip()
    {
        if(poem is{} page)page.RequestBack();
        else RequestMessageMode(23);
    }

    internal Task ReturnToEditor(Func<Task> restoreMusic, Func<bool> bypassWait)
    {
        ArgumentNullException.ThrowIfNull(restoreMusic);
        ArgumentNullException.ThrowIfNull(bypassWait);
        if (abandoned) return Task.FromCanceled(new CancellationToken(true));
        return editorReturn ??= ReturnToEditorCoreAsync(restoreMusic, bypassWait);
    }

    private async Task ReturnToEditorCoreAsync(Func<Task> restoreMusic, Func<bool> bypassWait)
    {
        returningToEditor = true;
        CloseLog();
        //83C100's test invocation still performs8345D0: black reaches255
        //before standing portraits and the message pane are retired.
        await WaitForView(done => scene.BeginNormalExitFade(false, 500, done));
        lifetime.Token.ThrowIfCancellationRequested();
        scene.RetireNormalTalk();
        await restoreMusic().WaitAsync(lifetime.Token);
        lifetime.Token.ThrowIfCancellationRequested();
        if (map is { } owner && GodotObject.IsInstanceValid(owner))
            await owner.RetireStoryEffectsAsync(this).WaitAsync(lifetime.Token);
        lifetime.Token.ThrowIfCancellationRequested();
        scene.RetireAttachments();
        //The editor test has no map/camera owner. Its outer39C900 callback
        //clears117 music cursors and restores settings only after this return.
        if (!bypassWait()) await WaitForView(done => scene.BeginSourceWait(1000, done));
        lifetime.Token.ThrowIfCancellationRequested();
        await WaitForView(done => scene.BeginNormalExitFade(true, 500, done));

        Task WaitForView(Action<Action> begin)
        {
            var completion = new TaskCompletionSource();
            begin(() => { if (!abandoned) completion.TrySetResult(); });
            return completion.Task.WaitAsync(lifetime.Token);
        }
    }

    private void CompleteStep(long sequence, int step, StoryOwnedEffectReceipt? effects = null)
    {
        if (abandoned) return;
        if (log is not null) { afterLog = () => CompleteStep(sequence, step, effects); return; }
        try
        {
            if (!access.CompleteStep(new(sequence, step) { OwnedEffectResult = effects })) return;
            if (ownsVoice) { audio.StopNarrationVoice(); ownsVoice = false; }
            Callable.From(Pump).CallDeferred();
        }
        catch (Exception error) { Fail(error); }
    }

    private void OpenLog()
    {
        if (!CanUse() || scene.Message is not { Visible: true } || access.ReadStory() is not { } story) return;
        try
        {
            var texts = story.TextLog.AsEnumerable();
            if (story.PreparedStep?.Text is { } current) texts = texts.Append(current);
            var portraits = preparedUiResources.Assets.Portraits;
            var entries = texts.Select(message =>
            {
                LoadedUiSpriteSource? face = null;
                if (message.Speaker is { } speaker)
                {
                    int appearance = speaker.PortraitAppearanceIndex ??
                        throw new InvalidDataException("story.log.source_appearance");
                    var route = repository.ResolveFacePortrait(appearance);
                    face = portraits.RequireSource(route.ResourceName, route.SpriteName);
                }
                return new StoryLogVisual(message.SpeakerName, message.Text, face);
            }).ToArray();
            var view = new StoryLogView(); log = view; AddChild(view);
            view.Configure(repository, text, entries, () => !abandoned && admitted() && log == view,
                CloseLog, scrollSpeed, preparedUiResources.CreateChromeResources());
        }
        catch (Exception error) { CloseLog(); Fail(error); }
    }
    private void CloseLog()
    {
        var previous = log; log = null;
        if (previous is not null) { previous.Abandon(); previous.QueueFree(); }
        var next = afterLog; afterLog = null;
        if (!abandoned) next?.Invoke();
    }
    private void Fail(Exception error) { if (!abandoned) failed?.Invoke(error); }
    internal void Abandon()
    {
        if (abandoned) return;
        abandoned = true;
        mediaRestoration.TrySetCanceled();
        lifetime.Cancel(); _ = RetireEffects(); completed = null; failed = null; prepared = null;
        ClosePoem(restore:false);CloseLog(); scene?.Abandon();
        if (audio is not null) audio.StorySoundSuppressed = null;
        if (ownsVoice) { audio?.StopNarrationVoice(); ownsVoice = false; }
        if (map is not null && GodotObject.IsInstanceValid(map)) map.EndStoryPresentation(this);
        Hide();
    }
    public override void _ExitTree() { Abandon(); lifetime.Dispose(); }
}
