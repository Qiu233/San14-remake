using System.Collections.Concurrent;
using Godot;
using San14.Data;

/// <summary>Independent selected-resource playback service. A game sound resolver
/// chooses the exact file and stream; this service does not infer UI/BGM/voice roles.
/// Loading alone never starts playback. Godot resources are installed on the node's
/// engine thread after background range reading and decoding finish.</summary>
public partial class OriginalAudioPlaybackService : Node
{
    private readonly ConcurrentQueue<Action> _engineActions = new();
    private readonly Dictionary<string, AudioStreamPlayer> _players = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly object _postGate = new();
    private OriginalAudioRepository? _repository;
    private IOriginalAudioDecoder? _decoder;
    private int _engineThread;

    public void Configure(OriginalAudioRepository repository, IOriginalAudioDecoder decoder)
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
    }

    public Task<OriginalAudioClip> PrepareAsync(uint fileId, int streamIndex,
        bool enableOriginalLoop = false, CancellationToken cancellationToken = default)
    {
        OriginalAudioRepository repository = _repository ?? throw new InvalidOperationException("Configure the attached audio service first.");
        return PrepareSelectedAsync(() => repository.LoadStream(fileId, streamIndex), enableOriginalLoop, cancellationToken);
    }

    public Task<OriginalAudioClip> PrepareBySectionIdAsync(uint fileId, uint sectionId,
        bool enableOriginalLoop = false, CancellationToken cancellationToken = default)
    {
        OriginalAudioRepository repository = _repository ?? throw new InvalidOperationException("Configure the attached audio service first.");
        return PrepareSelectedAsync(() => repository.LoadStreamBySectionId(fileId, sectionId), enableOriginalLoop, cancellationToken);
    }

    public Task<OriginalAudioClip> PrepareInputAsync(OriginalAudioDecodeInput input,
        bool enableOriginalLoop = false, CancellationToken cancellationToken = default)
    {
        IOriginalAudioDecoder decoder = _decoder ?? throw new InvalidOperationException("Configure the attached audio service first.");
        return PrepareDecodedAsync(token => decoder.DecodeInputAsync(input, token), enableOriginalLoop, cancellationToken);
    }

    public Task<OriginalAudioClip> PrepareLoadedStreamAsync(LoadedOriginalAudioStream source,
        bool enableOriginalLoop = false, CancellationToken cancellationToken = default)
    {
        IOriginalAudioDecoder decoder = _decoder ?? throw new InvalidOperationException("Configure the attached audio service first.");
        return PrepareDecodedAsync(token => decoder.DecodeAsync(source, token), enableOriginalLoop, cancellationToken);
    }

    public Task<OriginalAudioClip> PrepareInputAsync(OriginalAudioDecodeInput input,
        OriginalAudioLoopPolicy loopPolicy, CancellationToken cancellationToken = default)
    {
        IOriginalAudioDecoder decoder = _decoder ?? throw new InvalidOperationException("Configure the attached audio service first.");
        return PrepareDecodedAsync(token => decoder.DecodeInputAsync(input, token), loopPolicy, cancellationToken);
    }

    public Task<OriginalAudioClip> PrepareLoadedStreamAsync(LoadedOriginalAudioStream source,
        OriginalAudioLoopPolicy loopPolicy, CancellationToken cancellationToken = default)
    {
        IOriginalAudioDecoder decoder = _decoder ?? throw new InvalidOperationException("Configure the attached audio service first.");
        return PrepareDecodedAsync(token => decoder.DecodeAsync(source, token), loopPolicy, cancellationToken);
    }

    private Task<OriginalAudioClip> PrepareSelectedAsync(Func<LoadedOriginalAudioStream> loadSelected,
        bool enableOriginalLoop, CancellationToken cancellationToken)
    {
        IOriginalAudioDecoder decoder = _decoder!;
        return PrepareDecodedAsync(async token =>
        {
            LoadedOriginalAudioStream source = await Task.Run(loadSelected, token).ConfigureAwait(false);
            return await decoder.DecodeAsync(source, token).ConfigureAwait(false);
        }, enableOriginalLoop, cancellationToken);
    }

    private async Task<OriginalAudioClip> PrepareDecodedAsync(Func<CancellationToken, Task<DecodedOriginalAudio>> decode,
        bool enableOriginalLoop, CancellationToken cancellationToken)
        => await PrepareDecodedAsync(decode, enableOriginalLoop ? OriginalAudioLoopPolicy.RequireSource :
            OriginalAudioLoopPolicy.Disabled, cancellationToken).ConfigureAwait(false);

    private async Task<OriginalAudioClip> PrepareDecodedAsync(Func<CancellationToken, Task<DecodedOriginalAudio>> decode,
        OriginalAudioLoopPolicy loopPolicy, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        DecodedOriginalAudio audio = await decode(linked.Token).ConfigureAwait(false);
        return await InvokeOnEngineThreadAsync(() => OriginalAudioStreamFactory.Create(audio, loopPolicy),
            linked.Token, clip => clip.Stream.Dispose()).ConfigureAwait(false);
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
    public void Play(OriginalAudioClip clip, string channel, float volumeDb = 0, string bus = "Master")
    {
        RequireEngineThread();
        ArgumentNullException.ThrowIfNull(clip);
        if (string.IsNullOrWhiteSpace(channel) || channel.Length > 64) throw new ArgumentException("Audio channel needs a short nonempty key.", nameof(channel));
        if (!float.IsFinite(volumeDb) || volumeDb is < -80 or > 24) throw new ArgumentOutOfRangeException(nameof(volumeDb));
        if (AudioServer.GetBusIndex(bus) < 0) throw new ArgumentException("Selected audio bus does not exist.", nameof(bus));
        if (!_players.TryGetValue(channel, out AudioStreamPlayer? player))
        {
            player = new AudioStreamPlayer { Name = "OriginalAudioVoice" + _players.Count, Autoplay = false };
            AddChild(player); _players.Add(channel, player);
        }
        player.Stop(); player.Stream = clip.Stream; player.Bus = bus; player.VolumeDb = volumeDb;
        player.StreamPaused = false; player.Play();
    }

    public void Stop(string channel)
    {
        RequireEngineThread();
        if (_players.TryGetValue(channel, out AudioStreamPlayer? player)) { player.Stop(); player.Stream = null; }
    }

    public void Pause(string channel, bool paused)
    {
        RequireEngineThread();
        if (_players.TryGetValue(channel, out AudioStreamPlayer? player)) player.StreamPaused = paused;
    }

    public override void _ExitTree()
    {
        _lifetime.Cancel();
        foreach (AudioStreamPlayer player in _players.Values) { player.Stop(); player.Stream = null; }
        _players.Clear();
        lock (_postGate) while (_engineActions.TryDequeue(out _)) { }
        _engineThread = 0;
    }

    private void RequireEngineThread()
    {
        if (_engineThread == 0 || System.Environment.CurrentManagedThreadId != _engineThread || _lifetime.IsCancellationRequested)
            throw new InvalidOperationException("Audio player commands must run on the attached service's Godot engine thread.");
    }
}
