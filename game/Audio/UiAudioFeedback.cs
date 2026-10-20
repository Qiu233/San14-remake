using Godot;
using San14.Data;

internal enum UiAudioAvailability { Loading, Ready, Unavailable }
internal sealed record UiAudioSource(string BankUri, string BankHash, int SelectFrames, int CancelFrames);
internal sealed record UiAudioFeedbackStatus(UiAudioAvailability Availability = UiAudioAvailability.Loading,
    string? Reason = null, UiAudioSource? Source = null, int? SelectCount = null, int? CancelCount = null);

/// <summary>Owns one UI feedback lifetime: source preparation, a shared playback
/// service, attached button subscriptions and the two prepared clips. Scene cues
/// remain a separate caller-owned session using the configured playback service.</summary>
internal sealed class UiAudioFeedback : IDisposable
{
    private readonly Node _host;
    private readonly SceneTree _tree;
    private readonly string _sourceDirectory;
    private readonly string? _overridesDirectory;
    private readonly Func<bool> _acceptPreparedClips;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly CancellationToken _token;
    private readonly Dictionary<ulong, (BaseButton Button, Action Handler)> _buttons = [];
    private bool _subscribed;
    private AudioPlaybackService? _service;
    private UiAudioClips? _clips;
    private AudioClip? _effectsPreview;
    private AudioVolumeOptions _volumes = new();

    public void SetVolumes(AudioVolumeOptions volumes)
    {
        ArgumentNullException.ThrowIfNull(volumes); volumes.Validate();
        _volumes = volumes;
        if (!_token.IsCancellationRequested) _service?.SetVolumes(volumes);
    }

    public UiAudioFeedback(Node host, string sourceDirectory, string? overridesDirectory,
        Func<bool> acceptPreparedClips)
    {
        (_host, _tree, _sourceDirectory, _overridesDirectory, _acceptPreparedClips) =
            (host, host.GetTree(), sourceDirectory, overridesDirectory, acceptPreparedClips);
        _token = _lifetime.Token;
    }

    public UiAudioFeedbackStatus Status { get; private set; } = new();
    public event Action<UiAudioFeedbackStatus>? Changed;
    public event Action<AudioPlaybackService>? ServiceReady;

    public void Start()
    {
        if (_subscribed) throw new InvalidOperationException("UI audio feedback is already started.");
        _tree.NodeAdded += HookButton;
        _tree.NodeRemoved += ReleaseButton;
        _subscribed = true;
        HookSubtree(_host);
        _ = PrepareAsync();
    }

    private async Task PrepareAsync()
    {
        try
        {
            if (!AudioDecoderRuntime.TryCreate(out var decoder, out string? reason))
            {
                Publish(Status with { Availability = UiAudioAvailability.Unavailable, Reason = reason });
                GD.PushWarning(reason ?? "Original UI audio decoder is unavailable.");
                return;
            }
            var service = new AudioPlaybackService { Name = "OriginalUiAudioPlayback" };
            _host.AddChild(service);
            _service = service;
            var audioRepository = new AudioRepository(_sourceDirectory, _overridesDirectory);
            service.Configure(audioRepository, decoder!);
            service.SetVolumes(_volumes);
            // Separate source cache from map and scenario loading.
            var source = new GameResourceRepository(_sourceDirectory, _overridesDirectory);
            var graphs = await Task.Run(() =>
            {
                _token.ThrowIfCancellationRequested();
                var category = source.LoadRdbAsset("RRPreview", 0x1CC2C167);
                return (Music: AudioMusicOutputResources.Read(category, 0x2E7FE88E),
                    Effects: AudioMusicOutputResources.Read(category, 0x2CB51744));
            }, _token);
            _token.ThrowIfCancellationRequested();
            if (!_acceptPreparedClips()) return;
            service.ConfigureOriginalOutputs(graphs.Music, graphs.Effects, OriginalAudioDeviceProfile.WindowsStereo);
            ServiceReady?.Invoke(service);
            UiAudioClips clips = await UiAudioClips.LoadAsync(service, source, audioRepository, _token);
            if (_token.IsCancellationRequested || !_acceptPreparedClips())
            {
                clips.Dispose();
                return;
            }
            _clips = clips;
            var provenance = new UiAudioSource(clips.SourceBankUri, clips.SourceBankSha256,
                clips.ButtonSelect.SampleFrames,
                clips.Cancel.SampleFrames);
            Publish(Status with { Availability = UiAudioAvailability.Ready, Source = provenance });
            GD.Print($"ORIGINAL_UI_AUDIO_READY bank={provenance.BankUri} " +
                $"selectFrames={provenance.SelectFrames} cancelFrames={provenance.CancelFrames} preloadOnly=true");
            await PrepareEffectsPreviewAsync(service, source, audioRepository);
        }
        catch (OperationCanceledException) when (_token.IsCancellationRequested) { }
        catch (Exception error)
        {
            if (_token.IsCancellationRequested) return;
            Publish(Status with { Availability = UiAudioAvailability.Unavailable, Reason = error.Message });
            GD.PushWarning("Original UI audio unavailable: " + error.Message);
        }
    }

    public void Play(bool cancel)
    {
        if (_token.IsCancellationRequested || _clips is null || _service is null) return;
        try
        {
            // Both selected UI samples belong to the original Effects
            // category. General cue selection/arbitration remains separate.
            if (!_service.TryPlayEffect(cancel ? _clips.Cancel : _clips.ButtonSelect)) return;
            Publish(cancel ? Status with { Availability = UiAudioAvailability.Ready, Reason = null,
                    CancelCount = (Status.CancelCount ?? 0) + 1 }
                : Status with { Availability = UiAudioAvailability.Ready, Reason = null,
                    SelectCount = (Status.SelectCount ?? 0) + 1 });
        }
        catch (Exception error)
        {
            Publish(Status with { Availability = UiAudioAvailability.Unavailable, Reason = error.Message });
            GD.PushWarning("Original UI sound playback unavailable: " + error.Message);
        }
    }

    private async Task PrepareEffectsPreviewAsync(AudioPlaybackService service, GameResourceRepository source,
        AudioRepository audioRepository)
    {
        AudioClip? clip = null;
        try
        {
            clip = await CueAudioClips.PrepareStaticNonSpatialEffectAsync(service, source,
                audioRepository,
                new(AudioSelectionResources.InstalledUiBankId, null, "SE_400_DAMEGE_VERTICAL_MAN", AudioLoopPolicy.Disabled), _token);
            if (_token.IsCancellationRequested || !_acceptPreparedClips()) { clip.Dispose(); return; }
            _effectsPreview = clip;
        }
        catch (OperationCanceledException) when (_token.IsCancellationRequested) { clip?.Dispose(); }
        catch (Exception error)
        {
            clip?.Dispose();
            if (!_token.IsCancellationRequested) GD.PushWarning("Original Effects preview unavailable: " + error.Message);
        }
    }

    public void PlayEffectsPreview()
    {
        if (_token.IsCancellationRequested || _effectsPreview is null || _service is null) return;
        try { _service.TryPlayEffect(_effectsPreview); }
        catch (Exception error) { GD.PushWarning("Original Effects preview unavailable: " + error.Message); }
    }

    private void HookSubtree(Node node)
    {
        HookButton(node);
        foreach (Node child in node.GetChildren()) HookSubtree(child);
    }

    private void HookButton(Node node)
    {
        if (node is not BaseButton button || !_host.IsAncestorOf(button) || _buttons.ContainsKey(button.GetInstanceId())) return;
        Action handler = () => Play(button.Name == "CancelPreparedDeployment" ||
            button.Name == "DiplomacyCancelAlliance" || button.Name == "TacticCancel");
        _buttons.Add(button.GetInstanceId(), (button, handler));
        button.Pressed += handler;
    }

    private void ReleaseButton(Node node)
    {
        if (_buttons.Remove(node.GetInstanceId(), out var subscription))
            subscription.Button.Pressed -= subscription.Handler;
    }

    private void Publish(UiAudioFeedbackStatus status)
    {
        Status = status;
        Changed?.Invoke(status);
    }

    // The host cancels pending UI work before leaving its scene-audio session.
    public void CancelPreparation()
    {
        if (!_token.IsCancellationRequested) _lifetime.Cancel();
    }

    public void Dispose()
    {
        CancelPreparation();
        if (_subscribed)
        {
            _tree.NodeAdded -= HookButton;
            _tree.NodeRemoved -= ReleaseButton;
            _subscribed = false;
        }
        foreach (var subscription in _buttons.Values)
            subscription.Button.Pressed -= subscription.Handler;
        _buttons.Clear();
        if (_service is { } service && GodotObject.IsInstanceValid(service) && service.IsInsideTree()) service.StopEffects();
        _effectsPreview?.Dispose(); _effectsPreview = null;
        _clips?.Dispose();
        _clips = null;
        Changed = null;
        ServiceReady = null;
        _lifetime.Dispose();
    }
}
