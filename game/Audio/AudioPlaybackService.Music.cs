using Godot;

public partial class AudioPlaybackService
{
    private sealed class MapMusicVoice(AudioStreamPlayer player, MusicMixerInterval interval, ulong releaseTicks,
        ulong attackTicks, int? musicIndex)
    {
        internal readonly AudioStreamPlayer Player = player;
        internal readonly MusicMixerInterval Interval = interval;
        internal bool RequestedPaused;
        internal bool Releasing;
        internal ulong Elapsed;
        internal float StartGain;
        internal float Gain;
        internal int? MusicIndex = musicIndex;
        internal ulong ReleaseTicks = releaseTicks;
        internal bool LinearRelease;
        internal ulong Duration => Releasing ? ReleaseTicks : attackTicks;
        internal bool Advance(ulong ticks)
        {
            Elapsed = Math.Min(Duration, checked(Elapsed + Math.Min(ticks, Duration)));
            if (Elapsed == Duration) { Gain = Releasing ? 0 : 1; return Releasing; }
            float u = (float)Elapsed / (float)Duration;
            if (!Releasing) Gain = u;
            else
            {
                float easing = LinearRelease ? u : (MathF.Sin((u - .5f) * 3.1415927f) + 1) * .5f;
                Gain = (0 - StartGain) * easing + StartGain;
            }
            return false;
        }
    }
    private readonly Dictionary<string, MapMusicVoice> _mapMusic = new(StringComparer.Ordinal);
    private MusicMixerClock? _musicClock;
    private readonly Dictionary<int, float> _musicPositions = [];

    internal void ResetMapMusicPositions()
    {
        RequireEngineThread();
        // The map's initialization writes all 117 remembered positions to zero.
        // An absent dictionary entry represents that explicit initialized zero.
        _musicPositions.Clear();
    }

    internal bool IsMapMusicPlaying(string channel, int index) =>
        _mapMusic.TryGetValue(channel, out var music) && !music.Releasing && music.MusicIndex == index &&
        music.Player.Stream is not null && music.Player.Playing && !music.Player.StreamPaused;

    // The source manager writes the outgoing cursor before fading it and reads
    // the target index's own cursor. Old and new players share one mixer epoch.
    internal void TransitionMapMusic(AudioClip clip, string channel, string? outgoingChannel,
        int musicIndex, bool useStoredPosition, int fadeInMilliseconds, int fadeOutMilliseconds, string bus)
    {
        RequireEngineThread();
        ulong releaseTicks = ValidateMapMusicClip(clip);
        AudioServer.Lock();
        try
        {
            if (outgoingChannel is not null && _mapMusic.TryGetValue(outgoingChannel, out var outgoing))
            {
                RememberMapMusicPosition(outgoing);
                ReleaseMapMusicLocked(outgoingChannel, fadeOutMilliseconds);
            }
            float start = useStoredPosition && _musicPositions.TryGetValue(musicIndex, out var saved) ? saved : 0;
            StartMapMusicLocked(clip, channel, 0, bus, musicIndex, start, fadeInMilliseconds, releaseTicks);
        }
        finally { AudioServer.Unlock(); }
    }

    private void RememberMapMusicPosition(MapMusicVoice voice)
    {
        if (voice.Releasing || voice.MusicIndex is not int index || voice.Player.Stream is null) return;
        float position = voice.Player.GetPlaybackPosition();
        if (float.IsFinite(position) && position >= 0) _musicPositions[index] = position;
    }

    // The manager's explicit stop saves the current cursor once and fades the
    // actual voice. Repeated stops while already fading do not restart it.
    internal bool FadeOutMapMusic(string channel, int milliseconds)
    {
        RequireEngineThread();
        AudioServer.Lock();
        try
        {
            if (!_mapMusic.TryGetValue(channel, out var voice) || voice.Releasing) return false;
            RememberMapMusicPosition(voice);
            ReleaseMapMusicLocked(channel, milliseconds);
            return true;
        }
        finally { AudioServer.Unlock(); }
    }

    /// <summary>The first-map attack on an already prepared music clip.
    /// Host route selection and native bus DSP belong to the caller.</summary>
    internal void PlayMapMusic(AudioClip clip, string channel, float volumeDb = 0, string bus = "Master",
        int? musicIndex = null, float startSeconds = 0, int attackMilliseconds = 1000)
    {
        RequireEngineThread();
        ulong releaseTicks = ValidateMapMusicClip(clip);
        AudioServer.Lock();
        try { StartMapMusicLocked(clip, channel, volumeDb, bus, musicIndex, startSeconds, attackMilliseconds, releaseTicks); }
        finally { AudioServer.Unlock(); }
    }

    private ulong ValidateMapMusicClip(AudioClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        if (clip.MapMusic is { } profile)
        {
            if (!ReferenceEquals(profile.Outputs, _originalOutputs))
                throw new InvalidOperationException("Prepared map music belongs to a replaced output resource.");
            profile.Outputs.ValidateHost();
        }
        if (clip.SampleRate != 48000 || clip.StaticPlayback is not null)
            throw new InvalidDataException("Map music requires its original 48000 Hz sample format and separate root gain.");
        float releaseSeconds = clip.MapMusic?.ReleaseSeconds ?? 3.5f;
        if (!float.IsFinite(releaseSeconds) || releaseSeconds < 0 || releaseSeconds * 48000f >= int.MaxValue)
            throw new InvalidDataException("Map music source release duration is outside its supported tick range.");
        return (uint)(releaseSeconds * 48000f);
    }

    private void StartMapMusicLocked(AudioClip clip, string channel, float volumeDb, string bus,
        int? musicIndex, float startSeconds, int attackMilliseconds, ulong releaseTicks)
    {
        _musicClock ??= new(GetInstanceId());
        ulong frames = ReadMapMusicFrames();
        Play(clip, channel, volumeDb, bus, AudioVolumeGroup.Music, startSeconds);
        var player = _players[channel].Player;
        // Parent lifecycle owns this voice's pause gate, including explicit
        // user pause. Inherited child notifications must not resume it.
        player.ProcessMode = ProcessModeEnum.Always;
        var voice = new MapMusicVoice(player, new(_musicClock.MixRate, frames), releaseTicks,
            checked((ulong)Math.Clamp(attackMilliseconds, 0, 30000) * 48), musicIndex);
        if (voice.Duration == 0) voice.Gain = 1;
        _mapMusic[channel] = voice;
        player.StreamPaused = !CanProcess();
        voice.Interval.SetRunning(!player.StreamPaused, frames);
        ApplyVolume(_players[channel]);
    }

    /// <summary>Only the proved mode0/6 resource stop. Generic Stop remains immediate.</summary>
    internal void ReleaseMapMusic(string channel)
        => ReleaseMapMusic(channel, null);

    private void ReleaseMapMusic(string channel, int? explicitMilliseconds)
    {
        RequireEngineThread();
        AudioServer.Lock();
        try { ReleaseMapMusicLocked(channel, explicitMilliseconds); }
        finally { AudioServer.Unlock(); }
    }

    private void ReleaseMapMusicLocked(string channel, int? explicitMilliseconds)
    {
        if (!_mapMusic.TryGetValue(channel, out var voice)) return;
        DrainMapMusic(channel, voice, ReadMapMusicFrames());
        if (!_mapMusic.ContainsKey(channel) || voice.Releasing) return;
        voice.Releasing = true; voice.StartGain = voice.Gain; voice.Elapsed = 0;
        if (explicitMilliseconds is int milliseconds)
        { voice.ReleaseTicks = checked((ulong)Math.Clamp(milliseconds, 0, 30000) * 48); voice.LinearRelease = true; }
        if (voice.Duration == 0) Stop(channel);
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
