internal sealed partial class SceneAudioBinding
{
    internal void BeginBaseTierCutsceneAudio(object owner) => _musicSession.BeginBaseTierBgmScalar(owner);
    internal void PrepareBaseTierCutsceneMovie(object owner) => _musicSession.PrepareBaseTierBgmScalar(owner);
    internal void EndBaseTierCutsceneAudio(object owner) => _musicSession.EndBaseTierBgmScalar(owner);
}
