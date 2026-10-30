using San14.Data;

public partial class SceneAudioSession
{
    internal GameAudioSpeakerLayout CurrentSpeakerLayout
    { get { RequireEngineThread(); return (_service ?? throw new InvalidOperationException("audio.output.service_owner")).CurrentSpeakerLayout; } }
    internal SpeakerLayoutAvailability QuerySpeakerLayoutAvailability(GameAudioSpeakerLayout layout)
    { RequireEngineThread(); return (_service ?? throw new InvalidOperationException("audio.output.service_owner")).QuerySpeakerLayoutAvailability(layout); }
    internal Task ApplySpeakerLayoutAsync(GameAudioSpeakerLayout layout, CancellationToken cancellation)
    { RequireEngineThread(); return (_service ?? throw new InvalidOperationException("audio.output.service_owner")).ApplySpeakerLayoutAsync(layout, cancellation); }
}
