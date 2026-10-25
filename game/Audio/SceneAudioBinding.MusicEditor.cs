using San14.Data;

internal sealed partial class SceneAudioBinding
{
    private bool _musicEditActive;
    private MapMusicEntrySelection? _musicReturnSelection;

    internal void BeginMusicEdit()
    {
        if (_musicEditActive) return;
        _musicReturnSelection = MapMusicChannel?.Status is "playing" or "paused" ? MapMusicSelection : null;
        _musicEditActive = true;
        StopMusicPreview();
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
        _musicSession.StopMapMusic();
        MapMusicSelection = null; MapMusicError = null;
        MusicChanged?.Invoke();
    }

    internal void EndMusicEdit(bool restore)
    {
        if (!_musicEditActive) return;
        var previous = _musicReturnSelection;
        _musicEditActive = false; _musicReturnSelection = null;
        StopMusicPreview();
        if (restore && previous is not null)
        {
            var lifetime = new CancellationTokenSource(); _mapEntry = lifetime;
            MapMusicSelection = previous;
            _ = PlayEntryAsync(previous, lifetime);
        }
        MusicChanged?.Invoke();
    }
}
