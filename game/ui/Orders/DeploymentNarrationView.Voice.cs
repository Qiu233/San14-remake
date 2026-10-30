using San14.Data;

internal partial class DeploymentNarrationView
{
    private bool preparedVoicesBound;

    /// <summary>Flush the prepared formatter's real requests once for this
    /// narration. Pages retain the same voice selection and never replay it.</summary>
    internal void BindPreparedVoices(IReadOnlyList<NarrationVoiceRequest> voices, SceneAudioBinding? binding)
    {
        ArgumentNullException.ThrowIfNull(voices);
        if (finished || preparedVoicesBound) throw new InvalidOperationException("Narration voice owner has retired or was already bound.");
        if (voices.Count != 0 && binding is null) throw new InvalidOperationException("Prepared narration voice requires the active scene audio owner.");
        preparedVoicesBound = true;
        //8644D0/83B4F0 close only the narration UI. Once flushed, playback
        //belongs to the existing audio manager: natural end, replacement or
        //scene retirement, independently of page advance/ordinary close.
        foreach (var voice in voices) binding!.PlayNarrationVoice(voice);
    }
}
