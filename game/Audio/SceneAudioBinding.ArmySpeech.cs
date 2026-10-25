using San14.Data;

internal sealed partial class SceneAudioBinding
{
    internal bool PrepareArmySpeechBank(NarrationVoiceLanguage language) => _session.PrepareArmySpeechBank(language);
    internal SceneAudioSession.ArmySpeechAudioBatch BeginArmySpeechAudio(ArmySpeechAudioContext? context, bool enabled)
        => _session.BeginArmySpeechAudio(context, enabled);
    // Ordinary tactics use the same three real Army spatial reservations and
    // source scene lifetime as map speech. Selection stays in its transaction;
    // Commit starts only the requests whose world result was actually adopted.
    internal bool PrepareTacticSpatialVoiceBank(NarrationVoiceLanguage language) => _session.PrepareArmySpeechBank(language);
    internal SceneAudioSession.ArmySpeechAudioBatch BeginTacticSpatialAudio(ArmySpeechAudioContext? context, bool enabled)
        => _session.BeginArmySpeechAudio(context, enabled);
    internal void StopArmySpeechVoices() => _session.StopArmySpeechVoices();
}
