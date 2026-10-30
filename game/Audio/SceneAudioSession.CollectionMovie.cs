public partial class SceneAudioSession
{
    internal AudioPlaybackService.MapMusicPauseHandle? BeginCollectionMovieMusicPause(int milliseconds)
    {
        RequireEngineThread();
        return _currentMusicChannel is { } channel
            ? _service?.BeginCollectionMovieMusicPause(channel, milliseconds) : null;
    }

    internal void EndCollectionMovieMusicPause(AudioPlaybackService.MapMusicPauseHandle owner, bool restore, int milliseconds)
    {
        RequireEngineThread();
        _service?.EndCollectionMovieMusicPause(owner, restore, milliseconds);
    }
}
