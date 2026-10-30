using Godot;
using San14.Core;
using San14.Client.Gameplay;

public partial class Main
{
    private sealed record NormalStoryAudioOwner(EventSourceReference Source, long Sequence);
    private sealed class NormalStoryExitWorkspace(GameplaySession session, CampaignComposition composition,
        MapSceneView map, GameState world, NormalTurnStoryPlayback playback, StoryPresentationBinding scene)
    {
        internal readonly GameplaySession Session = session;
        internal readonly CampaignComposition Composition = composition;
        internal readonly MapSceneView Map = map;
        internal readonly GameState World = world;
        internal readonly NormalTurnStoryPlayback Playback = playback;
        internal readonly StoryPresentationBinding Scene = scene;
    }
    private NormalStoryAudioOwner? _normalStoryAudio;
    private NormalStoryExitWorkspace? _normalStoryExit;
    private bool HasNormalStoryAudioExit => _normalStoryExit is not null;

    private bool IsNormalStoryExitCurrent(NormalStoryExitWorkspace owner) =>
        ReferenceEquals(_normalStoryExit, owner) && ReferenceEquals(_session, owner.Session) &&
        ReferenceEquals(_campaignComposition, owner.Composition) && ReferenceEquals(_residentMap, owner.Map) &&
        ReferenceEquals(StoryOwner(owner.Session), owner.World) &&
        ReferenceEquals(StoryPlayback(owner.Session), owner.Playback) &&
        IsStoryCurrent(owner.Scene);

    private bool TryHandleNormalStoryAudio(GameplaySession session, NormalPeriodOperation? pending,
        CampaignComposition composition, MapSceneView map)
    {
        if (StoryPlayback(session) is not { IsReturningCamera: false } playback)
            return false;
        var story = playback.Story;
        var audio = _sceneAudioBinding ?? throw new InvalidDataException("story.audio.scene_owner");
        bool sameOwner = _normalStoryAudio is { } current && current.Source == story.Source && current.Sequence == story.Sequence;
        if (!sameOwner)
        {
            CloseNormalStoryAudio();
            if (playback.Phase == NormalTurnStoryResultPhase.Entry)
            {
                var snapshot = audio.BeginNormalStory(story.Source, story.Sequence, _ui.SourceLocale,
                    _preferences.MusicSelection, map.CaptureStoryCamera());
                _normalStoryAudio = new(story.Source, story.Sequence);
                if (!session.TryBeginCampaignStoryAudio(StoryOwner(session), story.Sequence, snapshot))
                {
                    _normalStoryAudio = null;
                    _ = RecoverNormalStoryAudioAsync(audio, story.Source, story.Sequence);
                    throw new InvalidDataException("story.audio.entry_receipt_owner");
                }
                ContinueNormalStory(session, composition);
                return true;
            }
            //Legacy snapshots remain unknown. They may display retained content,
            //but the worker reports their missing return facts before actual exit.
            if (playback.AudioReturn is not { } saved) return false;
            audio.ResumeNormalStory(story.Source, story.Sequence, saved, _ui.SourceLocale, _preferences.MusicSelection);
            _normalStoryAudio = new(story.Source, story.Sequence);
        }
        if (playback.Phase != NormalTurnStoryResultPhase.Exiting) return false;
        if (_storyPresentation is null && !TryOpenStoryPresentation(session, pending, composition, map,
                story, restoreMedia: false)) return false;
        var scene = _storyPresentation ?? throw new InvalidDataException("story.audio.exit_scene_owner");
        var owner = new NormalStoryExitWorkspace(session, composition, map, StoryOwner(session), playback, scene);
        _normalStoryExit = owner;
        CloseStoryLog(scene, resume: false);
        _ = CompleteNormalStoryExitAsync(owner, audio);
        return true;
    }

    private void ContinueNormalStory(GameplaySession session, CampaignComposition composition)
    {
        _advancingPeriod = true;
        _periodAdvanceTask = ContinueCampaignStoryAsync(session, composition);
    }

    private async Task CompleteNormalStoryExitAsync(NormalStoryExitWorkspace owner, SceneAudioBinding audio)
    {
        var scene = owner.Scene;
        var story = owner.Playback.Story;
        var cancellation = scene.Cancellation.Token;
        try
        {
            //A loaded result cannot retire while its real media is still restoring.
            await scene.MediaRestoration.WaitAsync(cancellation);
            if (!IsNormalStoryExitCurrent(owner)) return;
            //8345D0 waits for black before removing standing portraits/talk.
            await WaitForView(done => scene.View.BeginNormalExitFade(false, 500, done));
            if (!IsNormalStoryExitCurrent(owner)) return;
            scene.View.RetireNormalTalk();
            await audio.RestoreNormalStoryMusicAsync(story.Source, story.Sequence, cancellation);
            if (!IsNormalStoryExitCurrent(owner)) return;
            await scene.Map.RetireStoryEffectsAsync(scene);
            if (!IsNormalStoryExitCurrent(owner)) return;
            scene.View.RetireAttachments();
            var snapshot = owner.Playback.AudioReturn ?? throw new InvalidDataException("story.audio.exit_return_owner");
            if (snapshot.InvocationCamera is { } camera)
            {
                if (!scene.Map.RestoreStoryCamera(scene, camera))
                    throw new InvalidDataException("story.camera.invocation_return_owner");
                await WaitForView(done => scene.View.BeginWait(0, done, () => scene.Map.IsStoryCameraIdle(scene)));
            }
            if (!IsNormalStoryExitCurrent(owner)) return;
            bool returning = scene.Rules.ReadStoryInputWaitBypassCurrent() ??
                throw new InvalidDataException("story.audio.exit_application_owner");
            if (!returning) await WaitForView(done => scene.View.BeginSourceWait(1000, done));
            if (!IsNormalStoryExitCurrent(owner)) return;
            //This host is the ordinary playable-map route. Special ending-family
            //events bypass its final reveal, as in the source caller's mask.
            if (story.Source.EventId is not (900 or 950 or >= 953 and <= 959))
                await WaitForView(done => scene.View.BeginNormalExitFade(true, 500, done));
            if (!IsNormalStoryExitCurrent(owner)) return;
            await audio.EndNormalStory(story.Source, story.Sequence, restore: true, cancellation);
            if (!IsNormalStoryExitCurrent(owner)) return;
            if (!owner.Session.TryCompleteCampaignStoryAudio(owner.World, story.Sequence, snapshot))
                throw new InvalidDataException("story.audio.exit_receipt_owner");
            _normalStoryExit = null; _normalStoryAudio = null;
            CloseStoryPresentation();
            ContinueNormalStory(owner.Session, owner.Composition);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception error)
        {
            if (IsNormalStoryExitCurrent(owner)) RunUiAction(() =>
            {
                //Retain the failed pending owner: a refresh must not loop the
                //same restoration automatically. Saving/loading retains Exiting.
                ReportPlayFailure(error);
            });
        }

        Task WaitForView(Action<Action> begin)
        {
            var completion = new TaskCompletionSource();
            begin(() => { if (IsNormalStoryExitCurrent(owner)) completion.TrySetResult(); });
            return completion.Task.WaitAsync(cancellation);
        }
    }

    private async Task RecoverNormalStoryAudioAsync(SceneAudioBinding audio, EventSourceReference source, long sequence)
    {
        try { await audio.EndNormalStory(source, sequence, restore: !_failed && !_shuttingDown); }
        catch (Exception error) { if (!_failed && !_shuttingDown) RunUiAction(() => ReportPlayFailure(error)); }
    }

    private void CloseNormalStoryAudio()
    {
        var exit = _normalStoryExit; _normalStoryExit = null;
        if (exit is not null && ReferenceEquals(_storyPresentation, exit.Scene)) CloseStoryPresentation();
        var owner = _normalStoryAudio; _normalStoryAudio = null;
        if (owner is not null && _sceneAudioBinding is { } audio)
            _ = audio.EndNormalStory(owner.Source, owner.Sequence, restore: false);
    }
}
