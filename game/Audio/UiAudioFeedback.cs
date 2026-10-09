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
    private AudioPlaybackService? _service;
    private UiAudioClips? _clips;

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
        _tree.NodeAdded += HookButton;
        _tree.NodeRemoved += ReleaseButton;
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
            service.Configure(new AudioRepository(_sourceDirectory, _overridesDirectory), decoder!);
            ServiceReady?.Invoke(service);
            // Separate source cache from map and scenario loading.
            var source = new GameResourceRepository(_sourceDirectory, _overridesDirectory);
            UiAudioClips clips = await UiAudioClips.LoadAsync(service, source, _token);
            if (_token.IsCancellationRequested || !_acceptPreparedClips())
            {
                clips.Dispose();
                return;
            }
            _clips = clips;
            var provenance = new UiAudioSource(clips.SourceBankUri, clips.SourceBankSha256,
                clips.ButtonSelect.Stream.Data.Length / (clips.ButtonSelect.Stream.Stereo ? 4 : 2),
                clips.Cancel.Stream.Data.Length / (clips.Cancel.Stream.Stereo ? 4 : 2));
            Publish(Status with { Availability = UiAudioAvailability.Ready, Source = provenance });
            GD.Print($"ORIGINAL_UI_AUDIO_READY bank={provenance.BankUri} " +
                $"selectFrames={provenance.SelectFrames} cancelFrames={provenance.CancelFrames} preloadOnly=true");
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
            // Existing feedback policy: one UI voice, Master at 0 dB.
            // The original cue graph and mixer are separate unresolved work.
            _service.Play(cancel ? _clips.Cancel : _clips.ButtonSelect, "ui");
            Publish(cancel ? Status with { CancelCount = (Status.CancelCount ?? 0) + 1 }
                : Status with { SelectCount = (Status.SelectCount ?? 0) + 1 });
        }
        catch (Exception error)
        {
            Publish(Status with { Availability = UiAudioAvailability.Unavailable, Reason = error.Message });
            GD.PushWarning("Original UI sound playback unavailable: " + error.Message);
        }
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
        _tree.NodeAdded -= HookButton;
        _tree.NodeRemoved -= ReleaseButton;
        foreach (var subscription in _buttons.Values)
            subscription.Button.Pressed -= subscription.Handler;
        _buttons.Clear();
        if (_service is { } service && GodotObject.IsInstanceValid(service) && service.IsInsideTree()) service.Stop("ui");
        _clips?.Dispose();
        _clips = null;
        Changed = null;
        ServiceReady = null;
        _lifetime.Dispose();
    }
}
