using San14.Data;

internal sealed partial class SceneAudioBinding
{
    /// <summary>Direction and narration share the source's ordinary Voice
    /// channel1. This task completes after installation, not at the voice tail.</summary>
    internal Task<SceneAudioChannel> PlayUniqueTacticVoiceAsync(UniqueTacticSelectedVoice selection,
        CancellationToken cancellationToken = default)
        => PlayPersonVoiceAsync(selection, cancellationToken);

    /// <summary>Gallery and Direction replace the same source channel1 and its
    /// real output routes. The returned task reports installation, not the tail.</summary>
    internal Task<SceneAudioChannel> PlayPersonVoiceAsync(UniqueTacticSelectedVoice selection,
        CancellationToken cancellationToken = default)
    {
        StopNarrationVoice();
        var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _narrationVoice = lifetime;
        return _session.PlayUniqueTacticVoiceAsync(NarrationVoiceChannel, selection, lifetime.Token);
    }

    //Scene cutin completion has no404680 stop call. Only an actual explicit
    //stop, next channel1 request, or enclosing scene/map exit retires its voice.
    internal void StopUniqueTacticVoice() => StopNarrationVoice();
}
