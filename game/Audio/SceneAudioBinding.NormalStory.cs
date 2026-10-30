using San14.Core;
using San14.Data;

internal sealed partial class SceneAudioBinding
{
    private sealed class NormalStoryAudioPlayback(EventSourceReference source, long sequence,
        NormalTurnStoryAudioReturn audioReturn, string locale, MapMusicPreferences preferences)
    {
        internal readonly EventSourceReference Source = source;
        internal readonly long Sequence = sequence;
        internal readonly NormalTurnStoryAudioReturn Return = audioReturn;
        internal readonly string Locale = locale;
        internal readonly MapMusicPreferences Preferences = preferences;
        internal CancellationTokenSource? RestoreLifetime;
        internal Task? Restoration;
        internal Task? Completion;
    }

    private NormalStoryAudioPlayback? _normalStoryAudio;

    // Called at the actual main-thread Entry barrier, before any event business.
    // The requested selection (+18), not the active player's index (+14), returns.
    internal NormalTurnStoryAudioReturn BeginNormalStory(EventSourceReference source, long sequence,
        string locale, MapMusicPreferences preferences, CampaignCameraSnapshot? invocationCamera = null)
    {
        RequireNormalStoryEntry(source, sequence, locale, preferences);
        if (_normalStoryAudio is not null)
            return RequireNormalStoryOwner(source, sequence).Return;
        var audioReturn = new NormalTurnStoryAudioReturn(MapMusicSelection?.Commit.MusicIndex,
            _musicSession.CaptureAudioSpecialMode()) { InvocationCamera = invocationCamera };
        var owner = new NormalStoryAudioPlayback(source, sequence, audioReturn, locale, preferences);
        _normalStoryAudio = owner;
        _mapEntry?.Cancel(); _mapEntry?.Dispose(); _mapEntry = null;
        //83C100 flags=3:4056F0 stores the active player's cursor and fades1000ms.
        _musicSession.FadeOutMapMusic(1000);
        _musicSession.ResetAudioSpecialMode();
        StopCollectionMovieOrdinarySounds();
        return audioReturn;
    }

    // A loaded invocation already entered. Attach its saved return facts without
    // capturing the map's newly selected music or replaying the entry fade.
    internal void ResumeNormalStory(EventSourceReference source, long sequence,
        NormalTurnStoryAudioReturn audioReturn, string locale, MapMusicPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(audioReturn);
        RequireNormalStoryEntry(source, sequence, locale, preferences);
        if (audioReturn.RequestedMusicIndex is < 0 or >= AudioMapMusicSelector.MusicIndexCount)
            throw new InvalidDataException("story.audio.return_music_index");
        if (_normalStoryAudio is not null)
        {
            if (RequireNormalStoryOwner(source, sequence).Return != audioReturn)
                throw new InvalidOperationException("story.audio.return_identity");
            return;
        }
        _normalStoryAudio = new(source, sequence, audioReturn, locale, preferences);
    }

    private void RequireNormalStoryEntry(EventSourceReference source, long sequence,
        string locale, MapMusicPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(preferences);
        if (sequence < 1) throw new ArgumentOutOfRangeException(nameof(sequence));
        if (locale is not ("TC" or "SC")) throw new ArgumentOutOfRangeException(nameof(locale));
        if (_playableSession is null || _session.SceneKey is null || _collectionEventAudio is not null || _eventTestAudio is not null)
            throw new InvalidOperationException("story.audio.campaign_owner");
    }

    private NormalStoryAudioPlayback RequireNormalStoryOwner(EventSourceReference source, long sequence)
    {
        if (_normalStoryAudio is not { } owner || owner.Source != source || owner.Sequence != sequence)
            throw new InvalidOperationException("story.audio.invocation_identity");
        return owner;
    }

    //83C100 runs this after8345D0's visual retirement, before the camera return,
    //1000ms wait and conditional screen fade. Those visual operations stay in Main.
    internal Task RestoreNormalStoryMusicAsync(EventSourceReference source, long sequence,
        CancellationToken cancellation = default)
    {
        var owner = RequireNormalStoryOwner(source, sequence);
        var restoration = owner.Restoration ??= RestoreNormalStoryMusicCoreAsync(owner);
        return cancellation.CanBeCanceled ? restoration.WaitAsync(cancellation) : restoration;
    }

    private async Task RestoreNormalStoryMusicCoreAsync(NormalStoryAudioPlayback owner)
    {
        StopNarrationVoice();
        _mapEntry?.Cancel(); _mapEntry?.Dispose(); _mapEntry = null;
        try
        {
            //900 deliberately retains the ending selection instead of restoring
            //the interrupted map track. No replacement cue is invented here.
            if (owner.Source.EventId == 900)
            {
                _musicSession.ResetAudioSpecialMode();
                StopCollectionMovieOrdinarySounds();
                return;
            }
            if (owner.Return.RequestedMusicIndex is not int index) return;
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
            if (ReferenceEquals(_normalStoryAudio, owner))
            { MapMusicError = error.Message; MusicChanged?.Invoke(); }
            throw;
        }
        finally
        {
            if (ReferenceEquals(_normalStoryAudio, owner) && owner.Source.EventId != 900)
            {
                StopCollectionMovieOrdinarySounds();
                _musicSession.RestoreAudioSpecialMode(owner.Return.SpecialMode);
            }
        }
    }

    // End(true) is called after Main's real wait/fade; it also supports recovery
    // before those operations. Cancellation of a view wait never repeats playback.
    internal Task EndNormalStory(EventSourceReference source, long sequence, bool restore,
        CancellationToken cancellation = default)
    {
        if (_normalStoryAudio is null) return Task.CompletedTask;
        var owner = RequireNormalStoryOwner(source, sequence);
        if (!restore)
        {
            CloseNormalStoryAudio();
            return Task.CompletedTask;
        }
        var completion = owner.Completion ??= EndNormalStoryCoreAsync(owner);
        return cancellation.CanBeCanceled ? completion.WaitAsync(cancellation) : completion;
    }

    private async Task EndNormalStoryCoreAsync(NormalStoryAudioPlayback owner)
    {
        await RestoreNormalStoryMusicAsync(owner.Source, owner.Sequence);
        if (!ReferenceEquals(_normalStoryAudio, owner)) return;
        _normalStoryAudio = null;
        owner.RestoreLifetime = null; //The accepted BGM belongs to the application.
        StopNarrationVoice(); StopCollectionMovieOrdinarySounds();
        //Ordinary playback never performs editor-test494180's cursor reset.
    }

    private void CloseNormalStoryAudio()
    {
        var owner = _normalStoryAudio;
        if (owner is null) return;
        _normalStoryAudio = null;
        var pending = _mapEntry; _mapEntry = null;
        pending?.Cancel(); pending?.Dispose();
        if (owner.RestoreLifetime is { } restoration && !ReferenceEquals(restoration, pending))
        { restoration.Cancel(); restoration.Dispose(); }
        owner.RestoreLifetime = null;
        StopNarrationVoice(); StopCollectionMovieOrdinarySounds();
    }
}
