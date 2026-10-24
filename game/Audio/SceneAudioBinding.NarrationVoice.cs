using Godot;
using San14.Data;

internal sealed partial class SceneAudioBinding
{
    private CancellationTokenSource? _narrationVoice;
    private const string NarrationVoiceChannel = "narration-voice";

    internal void PlayNarrationVoice(NarrationVoiceRequest request)
    {
        StopNarrationVoice();
        var lifetime = new CancellationTokenSource();
        _narrationVoice = lifetime;
        _ = PlayNarrationVoiceAsync(request, lifetime);
    }

    private async Task PlayNarrationVoiceAsync(NarrationVoiceRequest request, CancellationTokenSource lifetime)
    {
        try { await _session.PlayNarrationVoiceAsync(NarrationVoiceChannel, request, lifetime.Token); }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            if (ReferenceEquals(_narrationVoice, lifetime)) GD.PushWarning("Narration voice: " + error.Message);
        }
    }

    internal void StopNarrationVoice()
    {
        _narrationVoice?.Cancel(); _narrationVoice?.Dispose(); _narrationVoice = null;
        if (GodotObject.IsInstanceValid(_session) && _session.IsInsideTree())
            _session.StopChannel(NarrationVoiceChannel);
    }
}
