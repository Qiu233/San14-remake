using Godot;
using San14.Data;

internal sealed partial class SceneAudioBinding
{
    private sealed record PoemEventPlayback(string Locale, MapMusicPreferences Preferences,
        int? ReturnMusicIndex, CancellationTokenSource Preparation);
    private PoemEventPlayback? _poemEvent;
    internal bool PoemEventActive => _poemEvent is not null;

    internal void BeginPoemEvent(string locale, MapMusicPreferences preferences)
    {
        if (_poemEvent is not null) return;
        ArgumentNullException.ThrowIfNull(preferences);
        var entry = preferences.SelectMusicIndex(locale, 116);
        var owner = new PoemEventPlayback(locale, preferences, MapMusicChannel?.MusicCommit?.MusicIndex, new());
        _poemEvent = owner;
        _mapEntry?.Cancel(); _mapEntry?.Dispose(); _mapEntry = null;
        // The poem presentation deliberately restarts its own music when the
        // saved manager index already equals 116. Other songs use the normal
        // manager transition, which records their outgoing cursor once.
        if (owner.ReturnMusicIndex == 116) _musicSession.FadeOutMapMusic(1000);
        MapMusicSelection = entry; MapMusicError = null;
        _ = PlayPoemEventMusicAsync(entry, owner);
        MusicChanged?.Invoke();
    }

    private async Task PlayPoemEventMusicAsync(MapMusicEntrySelection entry, PoemEventPlayback owner)
    {
        try
        {
            await _musicSession.PlayMapMusicCommitAsync("map-music", entry,
                [new(AudioSelectionResources.InstalledMusicBankId, 0x272C6EFB)], owner.Preparation.Token,
                useStoredPosition: false, fadeInMilliseconds: 1000, fadeOutMilliseconds: 1000);
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            if (!ReferenceEquals(_poemEvent, owner)) return;
            MapMusicError = error.Message; MusicChanged?.Invoke();
        }
    }

    internal void EndPoemEvent(bool restore)
    {
        var owner = _poemEvent;
        if (owner is null) return;
        _poemEvent = null;
        owner.Preparation.Cancel(); owner.Preparation.Dispose();
        if (!restore)
        {
            if (GodotObject.IsInstanceValid(_musicSession) && _musicSession.IsInsideTree()) _musicSession.StopMapMusic();
            MapMusicSelection = null; MapMusicError = null;
        }
        else if (owner.ReturnMusicIndex is not int index)
        {
            // Unlike Collection row preview, this presentation explicitly
            // stops on return when its original manager index was invalid.
            _musicSession.FadeOutMapMusic(1000);
        }
        else
        {
            bool resumePosition = index != 116;
            if (!resumePosition) _musicSession.FadeOutMapMusic(1000);
            var entry = owner.Preferences.SelectMusicIndex(owner.Locale, index);
            _mapEntry?.Cancel(); _mapEntry?.Dispose();
            var lifetime = new CancellationTokenSource(); _mapEntry = lifetime;
            MapMusicSelection = entry; MapMusicError = null;
            _ = RestorePoemEventMusicAsync(entry, lifetime, resumePosition);
        }
        MusicChanged?.Invoke();
    }

    private async Task RestorePoemEventMusicAsync(MapMusicEntrySelection entry,
        CancellationTokenSource lifetime, bool resumePosition)
    {
        try
        {
            await _musicSession.PlayMapMusicCommitAsync("map-music", entry,
                [new(AudioSelectionResources.InstalledMusicBankId, 0x272C6EFB)], lifetime.Token,
                useStoredPosition: resumePosition, fadeInMilliseconds: 1000, fadeOutMilliseconds: 1000);
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            if (!ReferenceEquals(_mapEntry, lifetime)) return;
            MapMusicError = error.Message; MusicChanged?.Invoke();
        }
    }
}
