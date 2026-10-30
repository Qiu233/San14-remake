using Godot;

public partial class AudioPlaybackService
{
    internal sealed record MapMusicPauseHandle(string Channel, object Voice, bool WasPaused);

    internal MapMusicPauseHandle? BeginCollectionMovieMusicPause(string channel, int milliseconds)
    {
        RequireEngineThread();
        AudioServer.Lock();
        try
        {
            if (!_mapMusic.TryGetValue(channel, out var voice) || voice.Releasing) return null;
            ulong frames = ReadMapMusicFrames();
            DrainMapMusic(channel, voice, frames);
            if (!_mapMusic.TryGetValue(channel, out var current) || !ReferenceEquals(current, voice)) return null;
            var owner = new MapMusicPauseHandle(channel, voice, voice.RequestedPaused);
            voice.MoviePauseOwner = owner;
            if (!owner.WasPaused) SetCollectionMovieMusicFade(channel, voice, true, milliseconds, frames);
            return owner;
        }
        finally { AudioServer.Unlock(); }
    }

    internal void EndCollectionMovieMusicPause(MapMusicPauseHandle owner, bool restore, int milliseconds)
    {
        RequireEngineThread();
        AudioServer.Lock();
        try
        {
            if (!_mapMusic.TryGetValue(owner.Channel, out var voice) || !ReferenceEquals(voice, owner.Voice) ||
                !ReferenceEquals(voice.MoviePauseOwner, owner)) return;
            ulong frames = ReadMapMusicFrames();
            DrainMapMusic(owner.Channel, voice, frames);
            if (!_mapMusic.TryGetValue(owner.Channel, out var current) || !ReferenceEquals(current, voice)) return;
            voice.MoviePauseOwner = null;
            if (!restore) { Stop(owner.Channel); return; }
            if (!owner.WasPaused) SetCollectionMovieMusicFade(owner.Channel, voice, false, milliseconds, frames);
        }
        finally { AudioServer.Unlock(); }
    }

    private void SetCollectionMovieMusicFade(string channel, MapMusicVoice voice, bool paused, int milliseconds, ulong frames)
    {
        voice.RequestedPaused = paused;
        voice.PauseStartGain = voice.PauseGain;
        voice.PauseElapsed = 0;
        voice.PauseTicks = checked((ulong)Math.Clamp(milliseconds, 0, 30000) * 48);
        voice.PauseFading = voice.PauseTicks != 0;
        if (!voice.PauseFading) voice.PauseGain = paused ? 0 : 1;
        //Resume the same player at its original cursor before fading in.
        //No source index, seek, remembered position, or new clip is involved.
        voice.Player.StreamPaused = paused && !voice.PauseFading || !CanProcess();
        voice.Interval.SetRunning(!voice.Player.StreamPaused, frames);
        ApplyVolume(_players[channel]);
    }
}
