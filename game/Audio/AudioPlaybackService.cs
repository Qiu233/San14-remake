using System.Collections.Concurrent;
using Godot;
using San14.Data;

/// <summary>Independent selected-resource playback service. A game sound resolver
/// chooses the exact file and stream; this service does not infer UI/BGM/voice roles.
/// Loading alone never starts playback. Godot resources are installed on the node's
/// engine thread after background range reading and decoding finish.</summary>
public partial class AudioPlaybackService : Node
{
    private readonly ConcurrentQueue<Action> _engineActions = new();
    private sealed record Voice(OriginalAudioPlayer Player, float BaseVolumeDb, AudioVolumeGroup? Group,
        float SourceGain, bool StaticCue, long Sequence = 0, EffectVoice? Effect = null);
    private readonly Dictionary<string, Voice> _players = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly object _postGate = new();
    private AudioRepository? _repository;
    private IAudioDecoder? _decoder;
    private int _engineThread;
    private long _voiceSequence;
    internal event Action? Retiring;
    internal event Action<string, long, string?>? ChannelStopped;
    internal long ReadVoiceSequence(string channel) => _players[channel].Sequence;
    public AudioVolumeOptions Volumes { get; private set; } = new();
    private bool _voiceEnabled = true;
    private bool _windowFocused = true;

    // Window focus controls music output only. Keep source cursors, mixer
    // intervals, scene ownership and explicit pause/fade state advancing.
    internal void SetWindowFocused(bool focused)
    {
        RequireEngineThread();
        if (_windowFocused == focused) return;
        _windowFocused = focused;
        foreach (var voice in _players.Values)
            if (voice.Group == AudioVolumeGroup.Music) ApplyVolume(voice);
    }

    internal void SetVoiceEnabled(bool enabled)
    {
        RequireEngineThread();
        if (_voiceEnabled == enabled) return;
        _voiceEnabled = enabled;
        foreach (var voice in _players.Values)
            if (voice.Group == AudioVolumeGroup.Voice) ApplyVolume(voice);
        ApplySpatialVolumes();
    }

    /// <summary>Changes live voices without restarting or resuming them. New
    /// voices receive the same current gains at their actual playback call.</summary>
    public void SetVolumes(AudioVolumeOptions volumes)
    {
        RequireEngineThread();
        ArgumentNullException.ThrowIfNull(volumes); volumes.Validate();
        Volumes = volumes;
        foreach (var voice in _players.Values) ApplyVolume(voice);
        ApplySpatialVolumes();
    }

    private void ApplyVolume(Voice voice)
    {
        if (voice.StaticCue)
        {
            // Native direct amplitude is capped after the current category
            // gain. Caller-selected base volume is a separate project gain.
            float category = voice.Group is { } categoryGroup ? Volumes.Percent(categoryGroup) / 100f : 1;
            voice.Player.VolumeLinear = MathF.Min(voice.SourceGain * category, 4) * Mathf.DbToLinear(voice.BaseVolumeDb);
        }
        else if (voice.Group is { } group)
            voice.Player.VolumeLinear = voice.SourceGain * Mathf.DbToLinear(voice.BaseVolumeDb) * (Volumes.Percent(group) / 100f);
        else if (voice.SourceGain != 1)
            voice.Player.VolumeLinear = voice.SourceGain * Mathf.DbToLinear(voice.BaseVolumeDb);
        else voice.Player.VolumeDb = voice.BaseVolumeDb;
        if (_mapMusic.Values.FirstOrDefault(music => music.Player == voice.Player) is { } music)
            // The source attack/release is applied once by the player's mixer DSP.
            voice.Player.VolumeLinear *= music.PauseGain;
        if (voice.Effect is { } effect) voice.Player.VolumeLinear *=
            voice.Player.HasSourceEnvelope ? effect.ScriptVolume : effect.ControlGain;
        if (voice.Group == AudioVolumeGroup.Voice && !_voiceEnabled) voice.Player.VolumeLinear = 0;
        if (voice.Group == AudioVolumeGroup.Music && !_windowFocused) voice.Player.VolumeLinear = 0;
    }

    public void Configure(AudioRepository repository, IAudioDecoder decoder)
    {
        RequireEngineThread();
        if (_repository is not null) throw new InvalidOperationException("Audio service is already configured.");
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(decoder);
        _repository = repository; _decoder = decoder;
    }

    public override void _EnterTree()
    {
        if (_lifetime.IsCancellationRequested) throw new InvalidOperationException("A retired audio service cannot be reattached.");
        _engineThread = System.Environment.CurrentManagedThreadId;
        ProcessMode = ProcessModeEnum.Always;
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        while (_engineActions.TryDequeue(out Action? action)) action();
        UpdateEffects();
        UpdateSourceEffectEnvelopes();
        UpdateSpatialVoices();
        UpdateMapMusic();
        UpdateSpeakerLayoutChange();
    }

    public Task<AudioClip> PrepareAsync(uint fileId, int streamIndex,
        bool enableSourceLoop = false, CancellationToken cancellationToken = default)
    {
        AudioRepository repository = _repository ?? throw new InvalidOperationException("Configure the attached audio service first.");
        return PrepareSelectedAsync(() => repository.LoadStream(fileId, streamIndex), enableSourceLoop, cancellationToken);
    }

    public Task<AudioClip> PrepareBySectionIdAsync(uint fileId, uint sectionId,
        bool enableSourceLoop = false, CancellationToken cancellationToken = default)
    {
        AudioRepository repository = _repository ?? throw new InvalidOperationException("Configure the attached audio service first.");
        return PrepareSelectedAsync(() => repository.LoadStreamBySectionId(fileId, sectionId), enableSourceLoop, cancellationToken);
    }

    public Task<AudioClip> PrepareInputAsync(AudioDecodeInput input,
        bool enableSourceLoop = false, CancellationToken cancellationToken = default)
    {
        if (_decoder is null) throw new InvalidOperationException("Configure the attached audio service first.");
        return PrepareDecodedAsync(token => DecodeResidentAsync(input, token), enableSourceLoop, cancellationToken);
    }

    public Task<AudioClip> PrepareLoadedStreamAsync(LoadedAudioStream source,
        bool enableSourceLoop = false, CancellationToken cancellationToken = default)
    {
        if (_decoder is null) throw new InvalidOperationException("Configure the attached audio service first.");
        return PrepareDecodedAsync(token => DecodeResidentAsync(source, token), enableSourceLoop, cancellationToken);
    }

    public Task<AudioClip> PrepareInputAsync(AudioDecodeInput input,
        AudioLoopPolicy loopPolicy, CancellationToken cancellationToken = default)
    {
        if (_decoder is null) throw new InvalidOperationException("Configure the attached audio service first.");
        return PrepareDecodedAsync(token => DecodeResidentAsync(input, token), loopPolicy, cancellationToken);
    }

    public Task<AudioClip> PrepareLoadedStreamAsync(LoadedAudioStream source,
        AudioLoopPolicy loopPolicy, CancellationToken cancellationToken = default)
    {
        if (_decoder is null) throw new InvalidOperationException("Configure the attached audio service first.");
        return PrepareDecodedAsync(token => DecodeResidentAsync(source, token), loopPolicy, cancellationToken);
    }

    private Task<AudioClip> PrepareSelectedAsync(Func<LoadedAudioStream> loadSelected,
        bool enableSourceLoop, CancellationToken cancellationToken)
    {
        return PrepareDecodedAsync(async token =>
        {
            LoadedAudioStream source = await Task.Run(loadSelected, token).ConfigureAwait(false);
            return await DecodeResidentAsync(source, token).ConfigureAwait(false);
        }, enableSourceLoop, cancellationToken);
    }

    private async Task<AudioClip> PrepareDecodedAsync(Func<CancellationToken, Task<ResidentDecodedAudio>> decode,
        bool enableSourceLoop, CancellationToken cancellationToken)
        => await PrepareDecodedAsync(decode, enableSourceLoop ? AudioLoopPolicy.RequireSource :
            AudioLoopPolicy.Disabled, cancellationToken).ConfigureAwait(false);

    private async Task<AudioClip> PrepareDecodedAsync(Func<CancellationToken, Task<ResidentDecodedAudio>> decode,
        AudioLoopPolicy loopPolicy, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        ResidentDecodedAudio decoded = await decode(linked.Token).ConfigureAwait(false);
        return await InvokeOnEngineThreadAsync(() => BorrowResidentPlainStream(decoded, loopPolicy),
            linked.Token, clip => clip.Dispose()).ConfigureAwait(false);
    }

    /// <summary>Completes on this service's engine queue, cancelling on retirement.
    /// The optional release handles a result whose caller cancels during its creation.</summary>
    public async Task<T> InvokeOnEngineThreadAsync<T>(Func<T> action, CancellationToken cancellationToken = default,
        Action<T>? releaseCancelledResult = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (_engineThread == 0 && !_lifetime.IsCancellationRequested)
            throw new InvalidOperationException("Engine actions require an attached audio service.");
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        linked.Token.ThrowIfCancellationRequested();
        var ready = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration cancelled = linked.Token.Register(() => ready.TrySetCanceled(linked.Token));
        lock (_postGate)
        {
            if (!_lifetime.IsCancellationRequested)
                _engineActions.Enqueue(() =>
                {
                    if (ready.Task.IsCompleted) return;
                    try
                    {
                        T value = action();
                        if (!ready.TrySetResult(value)) releaseCancelledResult?.Invoke(value);
                    }
                    catch (Exception error) { ready.TrySetException(error); }
                });
        }
        return await ready.Task.ConfigureAwait(false);
    }

    /// <summary>Explicit caller-selected channel; each channel replaces its preceding voice.</summary>
    public void Play(AudioClip clip, string channel, float volumeDb = 0, string bus = "Master",
        AudioVolumeGroup? volumeGroup = null, float startSeconds = 0)
        => PlayVoice(clip,channel,volumeDb,bus,volumeGroup,startSeconds);

    private void PlayVoice(AudioClip clip,string channel,float volumeDb,string bus,
        AudioVolumeGroup? volumeGroup,float startSeconds,EffectVoice? effect=null, SpatialSoundSpeakerMatrix? controlMatrix=null)
    {
        RequireEngineThread();
        ArgumentNullException.ThrowIfNull(clip);
        if (!float.IsFinite(startSeconds) || startSeconds < 0) throw new ArgumentOutOfRangeException(nameof(startSeconds));
        if (string.IsNullOrWhiteSpace(channel) || channel.Length > 64) throw new ArgumentException("Audio channel needs a short nonempty key.", nameof(channel));
        if (!float.IsFinite(volumeDb) || volumeDb is < -80 or > 24) throw new ArgumentOutOfRangeException(nameof(volumeDb));
        if (volumeGroup is { } group && !Enum.IsDefined(group)) throw new ArgumentOutOfRangeException(nameof(volumeGroup));
        float sourceGain = clip.StaticPlayback?.LinearGain ?? clip.MapMusic?.RootGain ?? 1;
        if (!float.IsFinite(sourceGain) || sourceGain < 0) throw new InvalidDataException("Audio source gain must be finite and nonnegative.");
        float playbackRate = clip.StaticPlayback?.PlaybackRate ?? 1;
        if (!float.IsFinite(playbackRate) || playbackRate <= 0) throw new InvalidDataException("Audio playback rate must be finite and positive.");
        if (AudioServer.GetBusIndex(bus) < 0) throw new ArgumentException("Selected audio bus does not exist.", nameof(bus));
        StopUniqueTacticVoiceRoutes(channel);
        _mapMusic.Remove(channel);
        if (!_players.TryGetValue(channel, out var previous))
        {
            previous = new(new OriginalAudioPlayer { Name = "OriginalAudioVoice" + _players.Count },
                volumeDb, volumeGroup, sourceGain, clip.StaticPlayback is not null);
            AddChild(previous.Player);
        }
        var voice = previous with { BaseVolumeDb = volumeDb, Group = volumeGroup, SourceGain = sourceGain,
            StaticCue = clip.StaticPlayback is not null, Sequence = checked(++_voiceSequence), Effect = effect };
        _players[channel] = voice;
        var player = voice.Player;
        player.ProcessMode = ProcessModeEnum.Inherit;
        AudioServer.Lock();
        try
        {
            player.Stop(); player.Bus = bus;
            if (controlMatrix is null) player.SetClip(clip);
            else player.SetSpatialMatrixClip(clip, controlMatrix, GameAudioSpeakerLayout.Stereo);
            if (effect is { NativeEnvelopeActive: true })
                player.BeginSourceEnvelope(effect.NativeEnvelopeFrom, effect.NativeEnvelopeTarget,
                    effect.NativeEnvelopeTicks, effect.NativeEnvelopeCurve);
            player.PitchScale = playbackRate; ApplyVolume(voice);
            player.StreamPaused = false; player.Play(startSeconds);
        }
        finally { AudioServer.Unlock(); }
    }

    public void Stop(string channel, string? reason = null)
    {
        RequireEngineThread();
        StopUniqueTacticVoiceRoutes(channel);
        _mapMusic.Remove(channel);
        if (_players.TryGetValue(channel, out var voice) && voice.Player.Stream is not null)
        {
            voice.Player.Stop(); voice.Player.Stream = null;
            // Music may stop under the audio-driver lock. Deliver presentation
            // callbacks on the existing engine queue, with the actual voice ID.
            _engineActions.Enqueue(() => ChannelStopped?.Invoke(channel, voice.Sequence, reason));
        }
    }

    public void Pause(string channel, bool paused)
    {
        RequireEngineThread();
        if (_mapMusic.ContainsKey(channel)) { PauseMapMusic(channel, paused); return; }
        if (_players.TryGetValue(channel, out var voice) && (voice.Player.Playing || voice.Player.StreamPaused))
            voice.Player.StreamPaused = paused;
    }

    public override void _ExitTree()
    {
        //Borrowing scene sessions must release their owned sounds and pending
        //requests while this service can still execute engine-thread commands.
        //Sibling removal order (including failed-interface cleanup) is arbitrary.
        Retiring?.Invoke();
        Retiring = null;
        StopUniqueTacticVoices();
        ForceStopEffects();
        _lifetime.Cancel();
        if (_speakerLayoutChange is { } changing)
        { _speakerLayoutChange = null; changing.Completion.TrySetCanceled(_lifetime.Token); }
        StopAllSpatial();
        foreach (var voice in _players.Values) { voice.Player.Stop(); voice.Player.Stream = null; }
        _players.Clear();
        ChannelStopped = null;
        _mapMusic.Clear();
        AudioServer.Lock();
        try { _musicClock?.Dispose(); _musicClock = null; _originalOutputs?.Dispose(); _originalOutputs = null; }
        finally { AudioServer.Unlock(); }
        RetireResidentAudio();
        lock (_postGate) while (_engineActions.TryDequeue(out _)) { }
        _engineThread = 0;
    }

    private void RequireEngineThread()
    {
        if (_engineThread == 0 || System.Environment.CurrentManagedThreadId != _engineThread || _lifetime.IsCancellationRequested)
            throw new InvalidOperationException("Audio player commands must run on the attached service's Godot engine thread.");
    }
}
