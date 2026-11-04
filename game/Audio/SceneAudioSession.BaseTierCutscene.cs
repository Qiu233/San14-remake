public partial class SceneAudioSession
{
    internal void BeginBaseTierBgmScalar(object owner)
    {
        RequireEngineThread();
        (_service ?? throw new InvalidOperationException("base-tier.audio.service")).BeginBaseTierBgmScalar(owner);
    }
    internal void PrepareBaseTierBgmScalar(object owner)
    {
        RequireEngineThread();
        (_service ?? throw new InvalidOperationException("base-tier.audio.service")).PrepareBaseTierBgmScalar(owner);
    }
    internal void EndBaseTierBgmScalar(object owner)
    {
        RequireEngineThread();
        (_service ?? throw new InvalidOperationException("base-tier.audio.service")).EndBaseTierBgmScalar(owner);
    }
}
