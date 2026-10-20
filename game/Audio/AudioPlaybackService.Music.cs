using Godot;

public partial class AudioPlaybackService
{
    private sealed class MapMusicVoice(AudioStreamPlayer player, MusicMixerInterval interval, ulong releaseTicks)
    {
        internal readonly AudioStreamPlayer Player = player;
        internal readonly MusicMixerInterval Interval = interval;
        internal bool RequestedPaused;
        internal bool Releasing;
        internal ulong Elapsed;
        internal float StartGain;
        internal float Gain;
        internal ulong Duration => Releasing ? releaseTicks : 48000ul;
        internal bool Advance(ulong ticks)
        {
            Elapsed = Math.Min(Duration, checked(Elapsed + Math.Min(ticks, Duration)));
            if (Elapsed == Duration) { Gain = Releasing ? 0 : 1; return Releasing; }
            float u = (float)Elapsed / (float)Duration;
            if (!Releasing) Gain = u;
            else
            {
                float easing = (MathF.Sin((u - .5f) * 3.1415927f) + 1) * .5f;
                Gain = (0 - StartGain) * easing + StartGain;
            }
            return false;
        }
    }
    private readonly Dictionary<string, MapMusicVoice> _mapMusic = new(StringComparer.Ordinal);
    private MusicMixerClock? _musicClock;

    /// <summary>The proved first-map attack on an already prepared looping clip.
    /// Host route selection and native bus DSP belong to the caller.</summary>
    internal void PlayMapMusic(AudioClip clip, string channel, float volumeDb = 0, string bus = "Master")
    {
        RequireEngineThread();
        ArgumentNullException.ThrowIfNull(clip);
        if (clip.MapMusic is { } profile)
        {
            if (!ReferenceEquals(profile.Outputs, _originalOutputs))
                throw new InvalidOperationException("Prepared map music belongs to a replaced output resource.");
            profile.Outputs.ValidateHost();
        }
        if (!clip.UsesSourceLoop || clip.SampleRate != 48000 || clip.StaticPlayback is not null)
            throw new InvalidDataException("Map music requires its original 48000 Hz loop and separate root gain.");
        float releaseSeconds = clip.MapMusic?.ReleaseSeconds ?? 3.5f;
        if (!float.IsFinite(releaseSeconds) || releaseSeconds < 0 || releaseSeconds * 48000f >= int.MaxValue)
            throw new InvalidDataException("Map music source release duration is outside its supported tick range.");
        ulong releaseTicks = (uint)(releaseSeconds * 48000f);
        AudioServer.Lock();
        try
        {
            _musicClock ??= new(GetInstanceId());
            ulong frames = ReadMapMusicFrames();
            Play(clip, channel, volumeDb, bus, AudioVolumeGroup.Music);
            var player = _players[channel].Player;
            // Parent lifecycle owns this voice's pause gate, including explicit
            // user pause. Inherited child notifications must not resume it.
            player.ProcessMode = ProcessModeEnum.Always;
            var voice = new MapMusicVoice(player, new(_musicClock.MixRate, frames), releaseTicks);
            _mapMusic[channel] = voice;
            player.StreamPaused = !CanProcess();
            voice.Interval.SetRunning(!player.StreamPaused, frames);
            ApplyVolume(_players[channel]);
        }
        finally { AudioServer.Unlock(); }
    }

    /// <summary>Only the proved mode0/6 resource stop. Generic Stop remains immediate.</summary>
    internal void ReleaseMapMusic(string channel)
    {
        RequireEngineThread();
        AudioServer.Lock();
        try
        {
            if (!_mapMusic.TryGetValue(channel, out var voice)) return;
            DrainMapMusic(channel, voice, ReadMapMusicFrames());
            if (!voice.Releasing)
            { voice.Releasing = true; voice.StartGain = voice.Gain; voice.Elapsed = 0; }
        }
        finally { AudioServer.Unlock(); }
    }

    private void DrainMapMusic(string channel, MapMusicVoice voice, ulong frames)
    {
        if (voice.Player.Stream is null || (!voice.Player.Playing && !voice.Player.StreamPaused))
        { Stop(channel); return; }
        if (voice.Advance(voice.Interval.Drain(frames))) { Stop(channel); return; }
        ApplyVolume(_players[channel]);
    }

    private void UpdateMapMusic()
    {
        if (_mapMusic.Count == 0) return;
        AudioServer.Lock();
        try
        {
            ulong frames = _musicClock!.ReadFrames();
            foreach (var pair in _mapMusic.ToArray()) DrainMapMusic(pair.Key, pair.Value, frames);
        }
        catch (InvalidOperationException error)
        {
            InvalidateMapMusicEpoch(error.Message);
            GD.PushWarning(error.Message);
        }
        finally { AudioServer.Unlock(); }
    }

    private void PauseMapMusic(string channel, bool requested)
    {
        AudioServer.Lock();
        try
        {
            var voice = _mapMusic[channel];
            ulong frames = ReadMapMusicFrames();
            DrainMapMusic(channel, voice, frames);
            if (!_mapMusic.ContainsKey(channel)) return;
            voice.RequestedPaused = requested;
            bool paused = requested || !CanProcess();
            voice.Player.StreamPaused = paused;
            voice.Interval.SetRunning(!paused, frames);
        }
        finally { AudioServer.Unlock(); }
    }

    public override void _Notification(int what)
    {
        if (_musicClock is null || (what != NotificationPaused && what != NotificationUnpaused &&
            what != NotificationDisabled && what != NotificationEnabled)) return;
        try
        {
            foreach (var pair in _mapMusic.ToArray()) PauseMapMusic(pair.Key, pair.Value.RequestedPaused);
        }
        catch (InvalidOperationException error) { GD.PushWarning(error.Message); }
    }

    // A control transition can precede the next process poll (or disable it).
    // Invalid host state must retire Always children before leaving that path.
    private ulong ReadMapMusicFrames()
    {
        try { return _musicClock!.ReadFrames(); }
        catch (InvalidOperationException error) { InvalidateMapMusicEpoch(error.Message); throw; }
    }
    private void InvalidateMapMusicEpoch(string? reason = null)
    {
        foreach (string channel in _mapMusic.Keys.ToArray()) Stop(channel, reason);
        _musicClock?.Dispose(); _musicClock = null;
    }
}
