public partial class SceneAudioSession
{
    internal int CaptureAudioSpecialMode()
    {
        RequireEngineThread();
        return _service?.CaptureAudioSpecialMode() ??
            throw new InvalidOperationException("Collection event needs its actual audio service.");
    }

    internal void RestoreAudioSpecialMode(int mode)
    {
        RequireEngineThread();
        if (_service is null) throw new InvalidOperationException("Collection event needs its actual audio service.");
        _service.RestoreAudioSpecialMode(mode);
    }
}
