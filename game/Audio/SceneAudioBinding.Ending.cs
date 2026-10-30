using San14.Core;

internal sealed partial class SceneAudioBinding
{
    internal void BeginEndingAudio()
    {
        _musicSession.ResetAudioSpecialMode();
        StopCollectionMovieOrdinarySounds();
    }

    //The opening message is a real wait between these two manager requests.
    //Each task completes on request adoption, rather than at the end of a song.
    internal Task PlayEndingOpeningMusicAsync(string locale, MapMusicPreferences preferences,
        CancellationToken cancellation) =>
        PlayStoryMusicAsync(new(StoryAudioKind.Music, 24, 0, 1000, 1000), locale, preferences, cancellation);

    internal Task CompleteEndingOpeningMusicAsync(string locale, MapMusicPreferences preferences,
        CancellationToken cancellation) =>
        PlayStoryMusicAsync(new(StoryAudioKind.Music, 22, 0, 1000, 1000), locale, preferences, cancellation);

    internal void PrepareEndingMovieAudio()
    {
        StopCollectionMovieOrdinarySounds();
        _musicSession.FadeOutMapMusic(1000);
    }
}
