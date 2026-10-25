using Godot;
using San14.Data;

public sealed record SceneAudioChannel(string SceneKey, long SceneRevision, string Channel,
    AudioCueRequest? Request, string Status, AudioClipSelection? Selection = null,
    AudioClipCue? Cue = null, AudioDecodeProvenance? Provenance = null,
    int SampleRate = 0, int Channels = 0, int SampleFrames = 0,
    int? LoopBegin = null, int? LoopEnd = null, string? Error = null)
{
    public AudioSampleRequest? SampleRequest { get; init; }
    public AudioPreparedMusicSelection? MusicSelection { get; init; }
    public AudioPreparedMusicCommit? MusicCommit { get; init; }
    public int OutputChannels { get; init; }
}

/// <summary>A caller-owned scene session. Beginning/leaving a scene stops its
/// channels, cancels preparation and releases its resources. Music queries,
/// manager IDs, bus/gain and speaker routing remain caller inputs.</summary>
public partial class SceneAudioSession : Node
{
    private sealed class ChannelState(SceneAudioChannel snapshot, CancellationTokenSource cancellation,
        float volumeDb, string bus, AudioVolumeGroup? volumeGroup)
    {
        public SceneAudioChannel Snapshot = snapshot;
        public readonly CancellationTokenSource Cancellation = cancellation;
        public readonly float VolumeDb = volumeDb;
        public readonly string Bus = bus;
        public readonly AudioVolumeGroup? VolumeGroup = volumeGroup;
        public AudioClip? Clip;
        public MapMusicEntrySelection? MapMusic;
        public long VoiceSequence;
        public bool StaticNonSpatialEffect;
    }

    private readonly Dictionary<string, ChannelState> _channels = new(StringComparer.Ordinal);
    private AudioPlaybackService? _service;
    private GameResourceRepository? _repository;
    private AudioRepository? _streams;
    private CancellationTokenSource? _sceneCancellation;
    private int _engineThread;
    private bool _retired;
    public string? SceneKey { get; private set; }
    public long SceneRevision { get; private set; }
    // Includes preparing, loaded, playing, paused and failed retained channels.
    public int ChannelCount => _channels.Count;
    public event Action<SceneAudioChannel>? ChannelChanged;

    public override void _EnterTree()
    {
        if (_retired) throw new InvalidOperationException("A retired audio session cannot be reattached.");
        _engineThread = System.Environment.CurrentManagedThreadId;
    }

    public void Configure(AudioPlaybackService service, GameResourceRepository repository,
        AudioRepository streamRepository)
    {
        RequireEngineThread();
        if (_service is not null) throw new InvalidOperationException("Scene audio is already configured.");
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _streams = streamRepository ?? throw new ArgumentNullException(nameof(streamRepository));
        _service.ChannelStopped += VoiceStopped;
    }

    public void BeginScene(string sceneKey)
    {
        RequireEngineThread();
        ArgumentException.ThrowIfNullOrWhiteSpace(sceneKey);
        LeaveScene();
        SceneKey = sceneKey;
        _sceneCancellation = new();
    }

    public void LeaveScene()
    {
        RequireEngineThread();
        StopStorySounds();
        StopMonthlyTerritorySounds();
        _sceneCancellation?.Cancel();
        _sceneCancellation?.Dispose();
        _sceneCancellation = null;
        foreach (string channel in _channels.Keys.ToArray()) StopChannel(channel);
        SceneKey = null;
        SceneRevision++;
    }

    public SceneAudioChannel? GetChannel(string channel)
    {
        RequireEngineThread();
        return _channels.TryGetValue(channel, out ChannelState? state) ? state.Snapshot : null;
    }

    public Task<SceneAudioChannel> PrepareCueAsync(string channel, AudioCueRequest request,
        float volumeDb = 0, string bus = "Master", CancellationToken cancellationToken = default,
        AudioVolumeGroup? volumeGroup = null) =>
        ChangeSelectionAsync(channel, request, null, false, volumeDb, bus, cancellationToken, volumeGroup);

    public Task<SceneAudioChannel> PlayCueAsync(string channel, AudioCueRequest request,
        float volumeDb = 0, string bus = "Master", CancellationToken cancellationToken = default,
        AudioVolumeGroup? volumeGroup = null) =>
        ChangeSelectionAsync(channel, request, null, true, volumeDb, bus, cancellationToken, volumeGroup);

    public Task<SceneAudioChannel> PrepareSampleAsync(string channel, AudioSampleRequest request,
        float volumeDb = 0, string bus = "Master", CancellationToken cancellationToken = default,
        AudioVolumeGroup? volumeGroup = null) =>
        ChangeSelectionAsync(channel, null, request, false, volumeDb, bus, cancellationToken, volumeGroup);

    public Task<SceneAudioChannel> PlaySampleAsync(string channel, AudioSampleRequest request,
        float volumeDb = 0, string bus = "Master", CancellationToken cancellationToken = default,
        AudioVolumeGroup? volumeGroup = null) =>
        ChangeSelectionAsync(channel, null, request, true, volumeDb, bus, cancellationToken, volumeGroup);

    private Task<SceneAudioChannel> ChangeSelectionAsync(string channel, AudioCueRequest? request,
        AudioSampleRequest? sampleRequest,
        bool play, float volumeDb, string bus, CancellationToken cancellationToken, AudioVolumeGroup? volumeGroup,
        MapMusicEntrySelection? mapMusic = null, bool staticNonSpatialEffect = false)
    {
        RequireEngineThread();
        if (_service is null || _repository is null || _streams is null || SceneKey is null || _sceneCancellation is null)
            throw new InvalidOperationException("Configure scene audio and begin its explicit scene first.");
        if ((request is null) == (sampleRequest is null)) throw new ArgumentException("Select exactly one source cue or named sample.");
        if (string.IsNullOrWhiteSpace(channel) || channel.Length > 64) throw new ArgumentException("Audio channel needs a short nonempty key.", nameof(channel));
        if (!float.IsFinite(volumeDb) || volumeDb is < -80 or > 24) throw new ArgumentOutOfRangeException(nameof(volumeDb));
        if (volumeGroup is { } group && !Enum.IsDefined(group)) throw new ArgumentOutOfRangeException(nameof(volumeGroup));
        ArgumentException.ThrowIfNullOrWhiteSpace(bus);
        StopChannel(channel);
        var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _sceneCancellation.Token);
        var state = new ChannelState(new SceneAudioChannel(SceneKey, SceneRevision, channel, request, "preparing")
            { SampleRequest = sampleRequest, MusicSelection = mapMusic?.Selection, MusicCommit = mapMusic?.Commit },
            linked, volumeDb, bus, volumeGroup) { MapMusic = mapMusic, StaticNonSpatialEffect = staticNonSpatialEffect };
        _channels.Add(channel, state);
        ChannelChanged?.Invoke(state.Snapshot);
        return LoadAndInstallAsync(state, play, linked.Token);
    }

    private async Task<SceneAudioChannel> LoadAndInstallAsync(ChannelState state, bool play, CancellationToken token)
    {
        AudioClip? clip = null;
        try
        {
            clip = state.MapMusic is not null
                ? await MapMusicClips.PrepareAsync(_service!, _repository!, _streams!, state.Snapshot.Request!, token).ConfigureAwait(false)
                : state.Snapshot.Request is not null
                ? state.StaticNonSpatialEffect
                    ? await CueAudioClips.PrepareStaticNonSpatialEffectAsync(_service!, _repository!, _streams!,
                        state.Snapshot.Request, token).ConfigureAwait(false)
                    : await CueAudioClips.PrepareSingleSampleAsync(_service!, _repository!, _streams!,
                        state.Snapshot.Request, token).ConfigureAwait(false)
                : await NamedAudioClips.PrepareAsync(_service!, _repository!, _streams!,
                    state.Snapshot.SampleRequest!, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            SceneAudioChannel result = await _service!.InvokeOnEngineThreadAsync(() =>
            {
                if (!IsCurrent(state) || token.IsCancellationRequested) throw new OperationCanceledException(token);
                if (play)
                {
                    if (state.MapMusic is not null)
                    {
                        _service.OriginalOutputs.ValidateHost();
                        _service.PlayMapMusic(clip, state.Snapshot.Channel, state.VolumeDb, _service.OriginalOutputs.MusicBus);
                    }
                    else _service.Play(clip, state.Snapshot.Channel, state.VolumeDb, state.Bus, state.VolumeGroup);
                    state.VoiceSequence = _service.ReadVoiceSequence(state.Snapshot.Channel);
                }
                state.Clip = clip;
                state.Snapshot = state.Snapshot with
                {
                    Status = play ? "playing" : "prepared", Selection = clip.Selection, Cue = clip.Cue,
                    Provenance = clip.Provenance, SampleRate = clip.SampleRate, Channels = clip.Channels,
                    SampleFrames = clip.SampleFrames, OutputChannels = clip.OutputChannels,
                    LoopBegin = clip.UsesSourceLoop ? clip.LoopBegin : null,
                    LoopEnd = clip.UsesSourceLoop ? clip.LoopEnd : null
                };
                clip = null; // Ownership moves to this channel only after installation.
                ChannelChanged?.Invoke(state.Snapshot);
                return state.Snapshot;
            }).ConfigureAwait(false);
            return result;
        }
        catch (Exception error)
        {
            clip?.Dispose();
            try
            {
                await _service!.InvokeOnEngineThreadAsync(() =>
                {
                    if (IsCurrent(state))
                    {
                        state.Snapshot = state.Snapshot with { Status = error is OperationCanceledException ? "cancelled" : "unavailable", Error = error.Message };
                        ChannelChanged?.Invoke(state.Snapshot);
                    }
                    return true;
                }).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            throw;
        }
        finally { state.Cancellation.Dispose(); }
    }

    public void StartPrepared(string channel)
    {
        RequireEngineThread();
        if (!_channels.TryGetValue(channel, out ChannelState? state) || state.Clip is null)
            throw new InvalidOperationException("Selected channel has no prepared source cue.");
        if (state.MapMusic is not null)
        {
            _service!.OriginalOutputs.ValidateHost();
            _service.PlayMapMusic(state.Clip, channel, state.VolumeDb, _service.OriginalOutputs.MusicBus);
        }
        else _service!.Play(state.Clip, channel, state.VolumeDb, state.Bus, state.VolumeGroup);
        state.VoiceSequence = _service.ReadVoiceSequence(channel);
        state.Snapshot = state.Snapshot with { Status = "playing" };
        ChannelChanged?.Invoke(state.Snapshot);
    }

    public void PauseChannel(string channel, bool paused)
    {
        RequireEngineThread();
        if (!_channels.TryGetValue(channel, out ChannelState? state) || state.Clip is null ||
            state.Snapshot.Status is not ("playing" or "paused")) return;
        _service!.Pause(channel, paused);
        state.Snapshot = state.Snapshot with { Status = paused ? "paused" : "playing" };
        ChannelChanged?.Invoke(state.Snapshot);
    }

    public void StopChannel(string channel)
    {
        RequireEngineThread();
        if (!_channels.Remove(channel, out ChannelState? state)) return;
        // A completed load already disposed its request CTS; cancel only pending work.
        if (state.Snapshot.Status == "preparing") state.Cancellation.Cancel();
        if (_service is not null && GodotObject.IsInstanceValid(_service) && _service.IsInsideTree()) _service.Stop(channel);
        state.Clip?.Dispose();
        state.Clip = null;
        ChannelChanged?.Invoke(state.Snapshot with { Status = "stopped" });
    }

    /// <summary>Resolves a supplied selector commit against supplied bank links.
    /// This method does not run a default selection or synthesize query success.</summary>
    public async Task<AudioMusicCueSelection> ResolveMusicCommitAsync(AudioPreparedMusicCommit commit,
        IReadOnlyList<AudioCueBank> banks, CancellationToken cancellationToken = default)
    {
        RequireEngineThread();
        ArgumentNullException.ThrowIfNull(commit);
        ArgumentNullException.ThrowIfNull(banks);
        if (_repository is null || _sceneCancellation is null) throw new InvalidOperationException("Begin configured scene audio first.");
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _sceneCancellation.Token);
        AudioMusicCueSelection selected = await Task.Run(() =>
        {
            lock (_repository)
            {
                linked.Token.ThrowIfCancellationRequested();
                var loaded = banks.Select(binding => (binding,
                    AudioSelectionResources.Load(_repository, binding.SelectionBankId))).ToArray();
                linked.Token.ThrowIfCancellationRequested();
                return AudioCueSelection.ResolveMusicCommit(commit, loaded);
            }
        }, linked.Token);
        linked.Token.ThrowIfCancellationRequested();
        return selected;
    }

    public async Task<SceneAudioChannel> PrepareMusicCommitAsync(string channel,
        AudioPreparedMusicCommit commit, IReadOnlyList<AudioCueBank> banks,
        float volumeDb = 0, string bus = "Master", CancellationToken cancellationToken = default)
    {
        AudioMusicCueSelection selected = await ResolveMusicCommitAsync(commit, banks, cancellationToken);
        return await PrepareCueAsync(channel, selected.Request, volumeDb, bus, cancellationToken, AudioVolumeGroup.Music);
    }

    public async Task<SceneAudioChannel> PlayMusicCommitAsync(string channel,
        AudioPreparedMusicCommit commit, IReadOnlyList<AudioCueBank> banks,
        float volumeDb = 0, string bus = "Master", CancellationToken cancellationToken = default)
    {
        AudioMusicCueSelection selected = await ResolveMusicCommitAsync(commit, banks, cancellationToken);
        return await PlayCueAsync(channel, selected.Request, volumeDb, bus, cancellationToken, AudioVolumeGroup.Music);
    }

    private bool IsCurrent(ChannelState state) => !_retired && SceneRevision == state.Snapshot.SceneRevision &&
        _channels.TryGetValue(state.Snapshot.Channel, out ChannelState? current) && ReferenceEquals(state, current);

    public override void _ExitTree()
    {
        LeaveScene();
        if (_service is not null) _service.ChannelStopped -= VoiceStopped;
        _retired = true;
        _engineThread = 0;
    }

    internal async Task<SceneAudioChannel> PlayMapMusicCommitAsync(string channel, MapMusicEntrySelection entry,
        IReadOnlyList<AudioCueBank> banks, CancellationToken token = default)
    {
        RequireEngineThread();
        long revision = SceneRevision;
        var selected = await ResolveMusicCommitAsync(entry.Commit, banks, token);
        if (SceneRevision != revision || token.IsCancellationRequested)
            throw new OperationCanceledException("Map music selection belongs to a retired scene.", token);
        return await ChangeSelectionAsync(channel, selected.Request with { LoopPolicy = AudioLoopPolicy.RequireSource },
            null, true, 0, _service!.OriginalOutputs.MusicBus, token, AudioVolumeGroup.Music, entry);
    }

    private void VoiceStopped(string channel, long sequence, string? reason)
    {
        if (!_channels.TryGetValue(channel, out var state) || state.VoiceSequence != sequence || state.Clip is null) return;
        state.Clip.Dispose(); state.Clip = null;
        state.Snapshot = state.Snapshot with { Status = reason is null ? "stopped" : "unavailable", Error = reason };
        ChannelChanged?.Invoke(state.Snapshot);
    }

    private void RequireEngineThread()
    {
        if (_retired || _engineThread == 0 || System.Environment.CurrentManagedThreadId != _engineThread)
            throw new InvalidOperationException("Scene audio commands must run on its attached Godot engine thread.");
    }
}
