using Godot;
using San14.Core;
using San14.Client.Gameplay;

public partial class Main
{
    private sealed class PeriodMapMusicWorkspace(CampaignPeriodPlayback playback, GameplaySession session,
        MapSceneView map, GameState world, MapMusicIntent intent, CancellationToken cancellation)
    {
        internal readonly CampaignPeriodPlayback Playback = playback;
        internal readonly GameplaySession Session = session;
        internal readonly MapSceneView Map = map;
        internal readonly GameState World = world;
        internal readonly MapMusicIntent Intent = intent;
        internal readonly CancellationTokenSource Lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        internal bool Running = true;
    }

    private PeriodMapMusicWorkspace? _periodMapMusic;

    private bool CurrentPeriodMapMusic(PeriodMapMusicWorkspace owner) =>
        !_failed && !_shuttingDown && ReferenceEquals(_periodMapMusic, owner) &&
        ReferenceEquals(_periodPlayback, owner.Playback) && ReferenceEquals(_session, owner.Session) &&
        ReferenceEquals(_residentMap, owner.Map) && ReferenceEquals(owner.Playback.Candidate, owner.World);

    private bool BeginPeriodMapMusic(CampaignPeriodPlayback playback, MapSceneView? map, GameState candidate)
    {
        if (candidate.PendingMapMusicIntent is not { } intent) return false;
        if (playback.MapMusicPresentationOpen) return true;
        if (_periodMapMusic is not null) throw new InvalidOperationException("period.music.concurrent_host");
        if (!ReferenceEquals(_periodPlayback, playback) || !ReferenceEquals(playback.Candidate, candidate) ||
            _session is not { } session || map is null || !ReferenceEquals(_residentMap, map))
            throw new InvalidOperationException("period.music.borrow_owner");
        var audio = _sceneAudioBinding ?? throw new InvalidOperationException("period.music.audio_owner");
        var token = _periodAdvanceCancellation?.Token ?? throw new InvalidOperationException("period.music.operation_owner");
        var owner = new PeriodMapMusicWorkspace(playback, session, map, candidate, intent, token);
        _periodMapMusic = owner;
        playback.MapMusicPresentationOpen = true;
        // This is an actual audio acceptance borrow, not a terminal modal.
        // The map's source clock, focus and existing camera stay admitted.
        _ = PresentAsync();
        return true;

        async Task PresentAsync()
        {
            try
            {
                await audio.PlayMapMusicIntentAsync(owner.Intent, _ui.SourceLocale,
                    _preferences.MusicSelection, owner.Lifetime.Token);
                if (!ReferenceEquals(_periodMapMusic, owner)) return;
                if (!CurrentPeriodMapMusic(owner)) throw new InvalidOperationException("period.music.completion_owner");
                if (!playback.TryCompletePendingMapMusicIntent(owner.World, owner.Intent))
                    throw new InvalidOperationException("period.music.completion_intent");
                RetirePeriodMapMusic(playback);
                if (ReferenceEquals(_periodPlayback, playback) && playback.Candidate is { } completed)
                    PublishPeriodPlayback(playback, owner.Map, completed);
            }
            catch (OperationCanceledException) when (owner.Lifetime.IsCancellationRequested) { }
            catch (Exception error)
            {
                if (ReferenceEquals(_periodPlayback, playback)) playback.FailPresentation(error);
            }
            finally
            {
                if (ReferenceEquals(_periodMapMusic, owner)) RetirePeriodMapMusic(playback);
                owner.Running = false;
                owner.Lifetime.Dispose();
            }
        }
    }

    private void RetirePeriodMapMusic(CampaignPeriodPlayback playback)
    {
        playback.MapMusicPresentationOpen = false;
        if (_periodMapMusic is not { } owner || !ReferenceEquals(owner.Playback, playback)) return;
        _periodMapMusic = null;
        owner.Lifetime.Cancel();
        if (!owner.Running) owner.Lifetime.Dispose();
    }
}
