using Godot;
using San14.Data;

internal sealed partial class SceneAudioBinding
{
    private sealed record CollectionMusicPlayback(string Locale, MapMusicPreferences Preferences, int? ReturnMusicIndex);
    private CollectionMusicPlayback? _collectionMusic;
    private CancellationTokenSource? _collectionMusicRequest;
    internal bool CollectionMusicActive => _collectionMusic is not null;

    internal void BeginCollectionMusic(string locale, MapMusicPreferences preferences)
    {
        if (_collectionMusic is not null) return;
        ArgumentNullException.ThrowIfNull(preferences);
        if (locale is not ("TC" or "SC")) throw new ArgumentOutOfRangeException(nameof(locale));
        // 4EDAD0 saves the actual current manager index, including a stopped
        // current entry. The page starts with no selected preview row.
        _collectionMusic = new(locale, preferences, MapMusicChannel?.MusicCommit?.MusicIndex);
        _mapEntry?.Cancel(); _mapEntry?.Dispose(); _mapEntry = null;
        _musicSession.FadeOutMapMusic(500);
        MapMusicError = null;
        MusicChanged?.Invoke();
    }

    internal void PlayCollectionMusic(int? musicIndex)
    {
        var owner = _collectionMusic ?? throw new InvalidOperationException("Collection music needs its active page lifetime.");
        CancelCollectionMusicRequest();
        MapMusicError = null;
        if (musicIndex is not int index)
        {
            _musicSession.FadeOutMapMusic(250);
            MusicChanged?.Invoke();
            return;
        }
        var entry = owner.Preferences.SelectMusicIndex(owner.Locale, index);
        // The row-play callback explicitly stops first with 1000ms, then sends
        // index/from-zero/125ms attack. Its 125ms outgoing request cannot
        // restart the stop that the same manager has already accepted.
        _musicSession.FadeOutMapMusic(1000);
        var lifetime = new CancellationTokenSource(); _collectionMusicRequest = lifetime;
        MapMusicSelection = entry;
        _ = PlayCollectionEntryAsync(entry, owner, lifetime);
        MusicChanged?.Invoke();
    }

    private async Task PlayCollectionEntryAsync(MapMusicEntrySelection entry, CollectionMusicPlayback owner,
        CancellationTokenSource lifetime)
    {
        try
        {
            await _musicSession.PlayMapMusicCommitAsync("map-music", entry,
                [new(AudioSelectionResources.InstalledMusicBankId, 0x272C6EFB)], lifetime.Token,
                useStoredPosition: false, fadeInMilliseconds: 125, fadeOutMilliseconds: 125);
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            if (!ReferenceEquals(_collectionMusic, owner) || !ReferenceEquals(_collectionMusicRequest, lifetime)) return;
            MapMusicError = error.Message; MusicChanged?.Invoke();
        }
    }

    internal void EndCollectionMusic(bool restore)
    {
        var owner = _collectionMusic;
        if (owner is null) return;
        _collectionMusic = null;
        CancelCollectionMusicRequest();
        if (!restore)
        {
            if (GodotObject.IsInstanceValid(_musicSession) && _musicSession.IsInsideTree()) _musicSession.StopMapMusic();
            MapMusicSelection = null; MapMusicError = null;
        }
        else if (owner.ReturnMusicIndex is int index)
        {
            var entry = owner.Preferences.SelectMusicIndex(owner.Locale, index);
            _mapEntry?.Cancel(); _mapEntry?.Dispose();
            var lifetime = new CancellationTokenSource(); _mapEntry = lifetime;
            MapMusicSelection = entry; MapMusicError = null;
            _ = RestoreCollectionMusicAsync(entry, lifetime);
        }
        // An absent previous index is the manager's invalid fresh index. The
        // source return request rejects it; it does not select a default song.
        MusicChanged?.Invoke();
    }

    private async Task RestoreCollectionMusicAsync(MapMusicEntrySelection entry, CancellationTokenSource lifetime)
    {
        try
        {
            await _musicSession.PlayMapMusicCommitAsync("map-music", entry,
                [new(AudioSelectionResources.InstalledMusicBankId, 0x272C6EFB)], lifetime.Token,
                useStoredPosition: true, fadeInMilliseconds: 1000, fadeOutMilliseconds: 1000);
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            if (!ReferenceEquals(_mapEntry, lifetime)) return;
            MapMusicError = error.Message; MusicChanged?.Invoke();
        }
    }

    private void CancelCollectionMusicRequest()
    {
        _collectionMusicRequest?.Cancel(); _collectionMusicRequest?.Dispose(); _collectionMusicRequest = null;
    }
}
