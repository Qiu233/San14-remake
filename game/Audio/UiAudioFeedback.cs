using Godot;
using San14.Data;

internal enum UiAudioAvailability { Loading, Ready, Unavailable }
internal enum UiAudioCue { ButtonSelect, Tab, Cancel, None }
internal sealed record UiAudioSource(string BankUri, string BankHash, int SelectFrames, int CancelFrames, int TabFrames);
internal sealed record UiAudioFeedbackStatus(UiAudioAvailability Availability = UiAudioAvailability.Loading,
    string? Reason = null, UiAudioSource? Source = null, int? SelectCount = null, int? CancelCount = null, int? TabCount = null)
{
    internal string? SpeakerLayoutError { get; init; }
    internal GameAudioSpeakerLayout? RequestedSpeakerLayout { get; init; }
    internal GameAudioSpeakerLayout? ActualSpeakerLayout { get; init; }
}

/// <summary>Owns one UI feedback lifetime: source preparation, a shared playback
/// service, attached button subscriptions and the prepared clips. Scene cues
/// remain a separate caller-owned session using the configured playback service.</summary>
internal sealed class UiAudioFeedback : IDisposable
{
    private readonly Node _host;
    private readonly SceneTree _tree;
    private readonly GameResourceRepository _resourceSources;
    private Task? _preparation;
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
    private bool _windowFocused = true;

    internal void SetWindowFocused(bool focused)
    {
        _windowFocused = focused;
        if (!_token.IsCancellationRequested && _service is { } service &&
            GodotObject.IsInstanceValid(service) && service.IsInsideTree())
            service.SetWindowFocused(focused);
    }
    private GameAudioSpeakerLayout _initialSpeakerLayout = GameAudioSpeakerLayout.Stereo;

    internal void SetInitialSpeakerLayout(GameAudioSpeakerLayout layout)
    {
        if (_subscribed || !Enum.IsDefined(layout)) throw new InvalidOperationException("audio.output.initial_speaker_layout");
        _initialSpeakerLayout = layout;
    }

    //Called only after the real preference write succeeds. A saved51 choice
    //that cannot run on a changed device is never silently overwritten here.
    internal void SpeakerLayoutPreferenceCommitted(GameAudioSpeakerLayout layout)
    {
        if (_service is null || _service.CurrentSpeakerLayout != layout)
            throw new InvalidOperationException("audio.output.preference_not_applied");
        Publish(Status with { RequestedSpeakerLayout = layout, ActualSpeakerLayout = layout, SpeakerLayoutError = null });
    }

    public void SetVolumes(AudioVolumeOptions volumes)
    {
        ArgumentNullException.ThrowIfNull(volumes); volumes.Validate();
        _volumes = volumes;
        if (!_token.IsCancellationRequested) _service?.SetVolumes(volumes);
    }

    public UiAudioFeedback(Node host, GameResourceRepository resourceSources, string? overridesDirectory,
        Func<bool> acceptPreparedClips)
    {
        _resourceSources = resourceSources ?? throw new ArgumentNullException(nameof(resourceSources));
        (_host, _tree, _sourceDirectory, _overridesDirectory, _acceptPreparedClips) =
            (host, host.GetTree(), resourceSources.SourceDirectory, overridesDirectory, acceptPreparedClips);
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
        _preparation = PrepareAsync();
    }

    // Startup joins actual cue preparation and Godot resource installation.
    internal async Task WaitUntilPreparedAsync()
    {
        await (_preparation ?? throw new InvalidOperationException("UI audio feedback has not started."));
        _token.ThrowIfCancellationRequested();
        if (Status.Availability != UiAudioAvailability.Ready)
            throw new InvalidOperationException(Status.Reason ?? "Original UI audio preparation did not complete.");
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
            service.SetWindowFocused(_windowFocused);
            // This reader owns its ledger; immutable data belongs to the
            // application generation shared by the UI and map readers.
            var source = _resourceSources.CreateReadScope();
            var graphs = await Task.Run(() =>
            {
                _token.ThrowIfCancellationRequested();
                var category = source.LoadRdbAsset("RRPreview", 0x1CC2C167);
                return (Music: AudioMusicOutputResources.Read(category, 0x2E7FE88E),
                    Effects: AudioMusicOutputResources.Read(category, 0x2CB51744));
            }, _token);
            _token.ThrowIfCancellationRequested();
            if (!_acceptPreparedClips()) return;
            var actualLayout = _initialSpeakerLayout;
            string? speakerError = null;
            if (actualLayout == GameAudioSpeakerLayout.Surround51 &&
                OriginalAudioOutputs.QuerySurroundAvailability() is { Available: false } unsupported)
            {
                speakerError = unsupported.UnresolvedOwner;
                actualLayout = GameAudioSpeakerLayout.Stereo;
                GD.PushWarning("Saved5.1 output cannot be applied: " + speakerError +
                    ". Stereo output is active; the saved preference is retained for explicit recovery in Environment.");
            }
            service.ConfigureOriginalOutputs(graphs.Music, graphs.Effects,
                OriginalAudioDeviceProfile.WindowsStereo with { Layout = actualLayout });
            Publish(Status with { RequestedSpeakerLayout = _initialSpeakerLayout,
                ActualSpeakerLayout = actualLayout, SpeakerLayoutError = speakerError });
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
                clips.Cancel.SampleFrames, clips.Tab.SampleFrames);
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

    public void Play(bool cancel) => Play(cancel ? UiAudioCue.Cancel : UiAudioCue.ButtonSelect);

    public void Play(UiAudioCue cue)
    {
        if (cue == UiAudioCue.None || _token.IsCancellationRequested || _clips is null || _service is null) return;
        try
        {
            // These UI samples belong to the original Effects
            // category. General cue selection/arbitration remains separate.
            var clip = cue switch
            {
                UiAudioCue.ButtonSelect => _clips.ButtonSelect,
                UiAudioCue.Tab => _clips.Tab,
                UiAudioCue.Cancel => _clips.Cancel,
                _ => throw new ArgumentOutOfRangeException(nameof(cue))
            };
            if (!_service.TryPlayEffect(clip)) return;
            Publish(cue switch
            {
                UiAudioCue.Cancel => Status with { Availability = UiAudioAvailability.Ready, Reason = null,
                    CancelCount = (Status.CancelCount ?? 0) + 1 },
                UiAudioCue.Tab => Status with { Availability = UiAudioAvailability.Ready, Reason = null,
                    TabCount = (Status.TabCount ?? 0) + 1 },
                _ => Status with { Availability = UiAudioAvailability.Ready, Reason = null,
                    SelectCount = (Status.SelectCount ?? 0) + 1 }
            });
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
        catch
        {
            clip?.Dispose();
            // This cue belongs to the declared startup closure. The outer
            // preparation owner publishes its real failure as Unavailable.
            throw;
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
        Action handler = () =>
        {
            if (button.Disabled) return;
            Play(button is UiSourceButton source ? source.PressedCue :
                button.Name == "CancelPreparedDeployment" || button.Name == "DiplomacyCancelAlliance" ||
                button.Name == "TacticCancel" ? UiAudioCue.Cancel : UiAudioCue.ButtonSelect);
        };
        _buttons.Add(button.GetInstanceId(), (button, handler));
        if (button is UiSourceButton sourceButton) sourceButton.FeedbackRequested += handler;
        else button.Pressed += handler;
    }

    private void ReleaseButton(Node node)
    {
        if (_buttons.Remove(node.GetInstanceId(), out var subscription))
            DetachButton(subscription);
    }

    private static void DetachButton((BaseButton Button, Action Handler) subscription)
    {
        if (subscription.Button is UiSourceButton source) source.FeedbackRequested -= subscription.Handler;
        else subscription.Button.Pressed -= subscription.Handler;
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
            DetachButton(subscription);
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
