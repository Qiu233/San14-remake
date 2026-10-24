using San14.Core;
using San14.Data;

internal sealed partial class SceneAudioBinding
{
    //The growth outbox remains OfficerGrowthFeedback. Its native SoundCue
    //operation fixes SE4; use the existing selected-sample audio channel only.
    internal void PlayOfficerGrowthFeedbackSound() => PlayPersonnelPresentationAudio(
        new(TalentCommandPresentationOperation.SoundCue, 4, null, null, null, null),
        null, NarrationVoiceLanguage.Japanese);
}
