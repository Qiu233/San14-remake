using San14.Core;
using San14.Data;

internal sealed partial class SceneAudioBinding
{
    internal async Task PlayStoryMusicAsync(StoryAudio command, string locale,
        MapMusicPreferences preferences, CancellationToken cancellation)
    {
        // An explicit source fade-out is a music-manager operation. The
        // ordinary channel owner schedules and performs the fade; a stopped
        // channel remains a legitimate no-op, never an attempted music ID.
        if (command.AudioKind == StoryAudioKind.FadeOutMusic)
        {
            if (command.Id != 0 || command.Flags != 0 || command.ScalarB != 0 ||
                !float.IsFinite(command.ScalarA) || command.ScalarA < 0 ||
                (double)command.ScalarA > int.MaxValue || MathF.Truncate(command.ScalarA) != command.ScalarA)
                throw new InvalidDataException("story.audio.fade_out_manager_parameters");
            if (!HasStoryAudioOwner) throw new InvalidOperationException("story.audio.scene_owner");
            cancellation.ThrowIfCancellationRequested();
            _musicSession.FadeOutMapMusic(checked((int)command.ScalarA));
            return;
        }
        if (command.AudioKind is not (StoryAudioKind.Music or StoryAudioKind.DirectMusic) || command.Flags != 0 ||
            command.ScalarA != 1000f || command.ScalarB != 1000f)
            throw new InvalidDataException("story.audio.music_manager_parameters");
        if (!HasStoryAudioOwner)
            throw new InvalidOperationException("story.audio.scene_owner");
        var entry = command.AudioKind == StoryAudioKind.DirectMusic
            ? preferences.SelectMusicIndex(locale, command.Id)
            : preferences.SelectSourceSlot(locale, command.Id);
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
                [new(AudioSelectionResources.InstalledMusicBankId, 0x272C6EFB)], lifetime.Token,
                fadeInMilliseconds: 1000, fadeOutMilliseconds: 1000).WaitAsync(cancellation);
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
