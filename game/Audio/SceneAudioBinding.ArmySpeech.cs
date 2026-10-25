using San14.Data;

internal sealed partial class SceneAudioBinding
{
    internal bool PrepareArmySpeechBank(NarrationVoiceLanguage language) => _session.PrepareArmySpeechBank(language);
    internal SceneAudioSession.ArmySpeechAudioBatch BeginArmySpeechAudio(ArmySpeechAudioContext? context, bool enabled)
        => _session.BeginArmySpeechAudio(context, enabled);
    internal void StopArmySpeechVoices() => _session.StopArmySpeechVoices();
}
