using Godot;
using San14.Data;

internal sealed partial class SceneAudioBinding
{
    private sealed class EventTestAudioPlayback(string sceneKey, string? returnSceneKey,
        string locale, MapMusicPreferences preferences, int? returnMusicIndex, int returnSpecialMode)
    {
        internal readonly string SceneKey = sceneKey;
        internal readonly string? ReturnSceneKey = returnSceneKey;
        internal readonly string Locale = locale;
        internal readonly MapMusicPreferences Preferences = preferences;
        internal readonly int? ReturnMusicIndex = returnMusicIndex;
        internal readonly int ReturnSpecialMode = returnSpecialMode;
        internal CancellationTokenSource? RestoreLifetime;
        internal Task? Restoration;
        internal Task? Completion;
    }

    private EventTestAudioPlayback? _eventTestAudio;

    /// <summary>510A70's actual title test entry. The source's requested music
    /// selection survives the fade; the active player supplies its real cursor.</summary>
    internal void BeginEventTest(string actualSceneKey, string locale,
        MapMusicPreferences preferences, CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actualSceneKey);
        ArgumentNullException.ThrowIfNull(preferences);
        if (locale is not ("TC" or "SC")) throw new ArgumentOutOfRangeException(nameof(locale));
        if (_playableSession is not null || _collectionEventAudio is not null || _eventTestAudio is not null)
            throw new InvalidOperationException("An editor test needs its independent actual audio lifetime.");
        cancellation.ThrowIfCancellationRequested();
        var owner = new EventTestAudioPlayback(actualSceneKey, _session.SceneKey, locale, preferences,
            MapMusicSelection?.Commit.MusicIndex, _musicSession.CaptureAudioSpecialMode());
        _eventTestAudio = owner;
        _mapEntry?.Cancel(); _mapEntry?.Dispose(); _mapEntry = null;
        //4056F0 saves active manager+14's position before its500ms fade.
        //The return selection is manager+18, captured above before cancellation.
        _musicSession.FadeOutMapMusic(500);
        _session.BeginScene(actualSceneKey);
        _musicSession.ResetAudioSpecialMode();
        StopCollectionMovieOrdinarySounds();
    }

    /// <summary>The playback exit restores requested BGM before the source's
    ///1000ms wait and500ms screen fade, which belong to the view owner.</summary>
    internal Task RestoreEventTestMusicAsync()
    {
        var owner = _eventTestAudio;
        if (owner is null) return Task.CompletedTask;
        return owner.Restoration ??= RestoreEventTestMusicCoreAsync(owner);
    }

    private async Task RestoreEventTestMusicCoreAsync(EventTestAudioPlayback owner)
    {
        StopNarrationVoice(); StopCollectionMovieOrdinarySounds();
        _mapEntry?.Cancel(); _mapEntry?.Dispose(); _mapEntry = null;
        try
        {
            //83C100 skips404570 when the saved requested index was invalid.
            //It does not replace that branch with an invented stop or title cue.
            if (owner.ReturnMusicIndex is not int index) return;
            var entry = owner.Preferences.SelectMusicIndex(owner.Locale, index);
            var lifetime = new CancellationTokenSource();
            owner.RestoreLifetime = lifetime; _mapEntry = lifetime;
            MapMusicSelection = entry; MapMusicError = null; MusicChanged?.Invoke();
            await _musicSession.PlayMapMusicCommitAsync("map-music", entry,
                [new(AudioSelectionResources.InstalledMusicBankId, 0x272C6EFB)], lifetime.Token,
                useStoredPosition: true, fadeInMilliseconds: 1000, fadeOutMilliseconds: 1000);
        }
        catch (Exception error)
        {
            if (ReferenceEquals(_eventTestAudio, owner))
            { MapMusicError = error.Message; MusicChanged?.Invoke(); }
            throw;
        }
        finally
        {
            if (ReferenceEquals(_eventTestAudio, owner))
            {
                StopCollectionMovieOrdinarySounds();
                _musicSession.RestoreAudioSpecialMode(owner.ReturnSpecialMode);
            }
        }
    }

    /// <summary>Called after the actual view exit. An interrupted test can call
    ///this directly to restore audio first; shutdown can retire without restoration.</summary>
    internal Task EndEventTest(bool restore)
    {
        var owner = _eventTestAudio;
        if (owner is null) return Task.CompletedTask;
        if (!restore)
        {
            CloseEventTestAudio();
            if (GodotObject.IsInstanceValid(_musicSession) && _musicSession.IsInsideTree())
            {
                _musicSession.StopMapMusic();
                _musicSession.ResetMapMusicPositions();
            }
            MapMusicSelection = null; MapMusicError = null; MusicChanged?.Invoke();
            return Task.CompletedTask;
        }
        return owner.Completion ??= EndEventTestCoreAsync(owner);
    }

    private async Task EndEventTestCoreAsync(EventTestAudioPlayback owner)
    {
        try { await RestoreEventTestMusicAsync(); }
        finally
        {
            if (ReferenceEquals(_eventTestAudio, owner))
            {
                CloseEventTestAudio();
                //494180 clears117 saved BGM positions only after playback's
                //music restoration, source wait and view fade have completed.
                if (GodotObject.IsInstanceValid(_musicSession) && _musicSession.IsInsideTree())
                    _musicSession.ResetMapMusicPositions();
            }
        }
    }

    private void CloseEventTestAudio()
    {
        var owner = _eventTestAudio;
        if (owner is null) return;
        _eventTestAudio = null;
        var pending = _mapEntry; _mapEntry = null;
        pending?.Cancel(); pending?.Dispose();
        if (owner.RestoreLifetime is { } restoration && !ReferenceEquals(restoration, pending))
        { restoration.Cancel(); restoration.Dispose(); }
        owner.RestoreLifetime = null;
        StopNarrationVoice(); StopCollectionMovieOrdinarySounds();
        if (GodotObject.IsInstanceValid(_session) && _session.IsInsideTree())
        {
            if (owner.ReturnSceneKey is { } scene) _session.BeginScene(scene);
            else _session.LeaveScene();
        }
    }
}
