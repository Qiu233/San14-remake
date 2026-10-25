using San14.Core;
using San14.Data;

internal sealed partial class SceneAudioBinding
{
    internal async Task PlayStoryMusicAsync(StoryAudio command, string locale,
        MapMusicPreferences preferences, CancellationToken cancellation)
    {
        if (command.AudioKind != StoryAudioKind.Music || command.Flags != 0 ||
            command.ScalarA != 1000f || command.ScalarB != 1000f)
            throw new InvalidDataException("story.audio.music_manager_parameters");
        if (_playableSession is null || _session.SceneKey is null)
            throw new InvalidOperationException("story.audio.scene_owner");
        var entry = preferences.SelectSourceSlot(locale, command.Id);
        //404570 retains a currently playing identical selection. A stopped or
        //failed channel still needs a new request, even when its index matches.
        if (MapMusicChannel is { Status: "playing", MusicCommit: { } playing } &&
            playing.MusicIndex == entry.Commit.MusicIndex) return;
        _mapEntry?.Cancel(); _mapEntry?.Dispose();
        var lifetime = new CancellationTokenSource(); _mapEntry = lifetime;
        MapMusicSelection = entry; MapMusicError = null; MusicChanged?.Invoke();
        try
        {
            //The map owns BGM beyond this event's lifetime. Cancelling a story
            //wait suppresses its receipt; leaving the map cancels the channel.
            await _musicSession.PlayMapMusicCommitAsync("map-music", entry,
                [new(AudioSelectionResources.InstalledMusicBankId, 0x272C6EFB)], lifetime.Token).WaitAsync(cancellation);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error)
        {
            if (ReferenceEquals(_mapEntry, lifetime))
            { MapMusicError = error.Message; MusicChanged?.Invoke(); }
            throw;
        }
    }
}
