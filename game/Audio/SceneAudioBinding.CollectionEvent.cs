using Godot;
using San14.Data;

internal sealed partial class SceneAudioBinding
{
    private sealed record CollectionEventAudioPlayback(string SceneKey, string? ReturnSceneKey,
        string Locale, MapMusicPreferences Preferences, int? ReturnMusicIndex, int ReturnSpecialMode,
        AudioPlaybackService.MapMusicPauseHandle? PausedMusic, CancellationTokenSource Lifetime);
    private CollectionEventAudioPlayback? _collectionEventAudio;
    private bool HasStoryAudioOwner => _session.SceneKey is not null &&
        (_playableSession is not null || _collectionEventAudio is { } owner && _session.SceneKey == owner.SceneKey ||
            _eventTestAudio is { } test && _session.SceneKey == test.SceneKey);

    internal async Task BeginCollectionEvent(string actualSceneKey, int eventId, string locale,
        MapMusicPreferences preferences, bool pauseMusic, CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actualSceneKey);
        ArgumentNullException.ThrowIfNull(preferences);
        if (locale is not ("TC" or "SC")) throw new ArgumentOutOfRangeException(nameof(locale));
        if (_playableSession is not null || _collectionEventAudio is not null || _eventTestAudio is not null)
            throw new InvalidOperationException("A title event needs its independent actual audio lifetime.");
        cancellation.ThrowIfCancellationRequested();
        string? returnScene = _session.SceneKey;
        int? returnMusic = MapMusicChannel?.MusicCommit?.MusicIndex;
        int returnMode = _musicSession.CaptureAudioSpecialMode();
        var pausedMusic = pauseMusic ? _musicSession.BeginCollectionMovieMusicPause(1000) : null;
        var owner = new CollectionEventAudioPlayback(actualSceneKey, returnScene, locale, preferences,
            returnMusic, returnMode, pausedMusic, CancellationTokenSource.CreateLinkedTokenSource(cancellation));
        _collectionEventAudio = owner;
        _mapEntry?.Cancel(); _mapEntry?.Dispose(); _mapEntry = null;
        _session.BeginScene(actualSceneKey);
        _musicSession.ResetAudioSpecialMode();
        StopCollectionMovieOrdinarySounds();
        try
        {
            //60A6A0's ending-family preview starts source slot22 before VM.
            if (eventId > 949)
                await PlayStoryMusicAsync(new(San14.Core.StoryAudioKind.Music, 22, 0, 1000, 1000),
                    locale, preferences, owner.Lifetime.Token);
        }
        catch
        {
            if (ReferenceEquals(_collectionEventAudio, owner)) await EndCollectionEvent(restore: true);
            throw;
        }
    }

    internal async Task EndCollectionEvent(bool restore)
    {
        var owner = CloseCollectionEventAudio();
        if (owner is null) return;
        if (!restore)
        {
            _musicSession.StopMapMusic();
            MapMusicSelection = null; MapMusicError = null;
            MusicChanged?.Invoke();
            return;
        }
        //The source restores the saved manager selection with its own cursor.
        //Release this event's pause latch before that same manager request;
        //a replaced player makes the old latch an identity-checked no-op.
        if (owner.PausedMusic is { } paused) _musicSession.EndCollectionMovieMusicPause(paused, true, 1000);
        if (owner.ReturnMusicIndex is not int index)
        {
            StopCollectionMovieOrdinarySounds();
            _musicSession.RestoreAudioSpecialMode(owner.ReturnSpecialMode);
            return;
        }
        var entry = owner.Preferences.SelectMusicIndex(owner.Locale, index);
        _mapEntry?.Cancel(); _mapEntry?.Dispose();
        var lifetime = new CancellationTokenSource(); _mapEntry = lifetime;
        MapMusicSelection = entry; MapMusicError = null; MusicChanged?.Invoke();
        try
        {
            await _musicSession.PlayMapMusicCommitAsync("map-music", entry,
                [new(AudioSelectionResources.InstalledMusicBankId, 0x272C6EFB)], lifetime.Token,
                useStoredPosition: true, fadeInMilliseconds: 1000, fadeOutMilliseconds: 1000);
        }
        finally
        {
            if (ReferenceEquals(_mapEntry, lifetime))
            {
                StopCollectionMovieOrdinarySounds();
                _musicSession.RestoreAudioSpecialMode(owner.ReturnSpecialMode);
            }
        }
    }

    private CollectionEventAudioPlayback? CloseCollectionEventAudio()
    {
        var owner = _collectionEventAudio;
        if (owner is null) return null;
        _collectionEventAudio = null;
        owner.Lifetime.Cancel(); owner.Lifetime.Dispose();
        _mapEntry?.Cancel(); _mapEntry?.Dispose(); _mapEntry = null;
        StopNarrationVoice();
        StopCollectionMovieOrdinarySounds();
        if (GodotObject.IsInstanceValid(_session) && _session.IsInsideTree())
        {
            if (owner.ReturnSceneKey is { } scene) _session.BeginScene(scene);
            else _session.LeaveScene();
        }
        return owner;
    }
}
