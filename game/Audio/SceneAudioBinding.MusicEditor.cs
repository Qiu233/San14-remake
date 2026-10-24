using San14.Data;

internal sealed partial class SceneAudioBinding
{
    private bool _musicEditActive, _musicEditTemporaryScene;
    private MapMusicEntrySelection? _musicReturnSelection;

    internal void BeginMusicEdit()
    {
        if (_musicEditActive) return;
        _musicReturnSelection = MapMusicChannel?.Status is "playing" or "paused" ? MapMusicSelection : null;
        _musicEditTemporaryScene = _session.SceneKey is null;
        _musicEditActive = true;
        StopMusicPreview();
        if (_musicEditTemporaryScene) _session.BeginScene("music-editor");
    }

    internal void PreviewMusic(MapMusicEntrySelection entry)
    {
        if (!_musicEditActive) throw new InvalidOperationException("Music preview requires its editor lifetime.");
        StopMusicPreview();
        var lifetime = new CancellationTokenSource(); _mapEntry = lifetime;
        MapMusicSelection = entry;
        _ = PlayEntryAsync(entry, lifetime);
        MusicChanged?.Invoke();
    }

    internal void StopMusicPreview()
    {
        _mapEntry?.Cancel(); _mapEntry?.Dispose(); _mapEntry = null;
        if (_session.SceneKey is not null) _session.StopChannel("map-music");
        MapMusicSelection = null; MapMusicError = null;
        MusicChanged?.Invoke();
    }

    internal void EndMusicEdit(bool restore)
    {
        if (!_musicEditActive) return;
        var previous = _musicReturnSelection;
        bool temporary = _musicEditTemporaryScene;
        _musicEditActive = false; _musicReturnSelection = null; _musicEditTemporaryScene = false;
        StopMusicPreview();
        if (temporary) _session.LeaveScene();
        if (restore && previous is not null)
        {
            var lifetime = new CancellationTokenSource(); _mapEntry = lifetime;
            MapMusicSelection = previous;
            _ = PlayEntryAsync(previous, lifetime);
        }
        MusicChanged?.Invoke();
    }
}
