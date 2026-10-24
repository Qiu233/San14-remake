using Godot;

internal sealed partial class SceneAudioBinding
{
    internal void PlayForceExperienceGainSound()
    {
        StopPersonnelEffect();
        var lifetime = _personnelEffect = new CancellationTokenSource();
        _ = PlayForceExperienceGainSoundAsync(lifetime);
    }

    private async Task PlayForceExperienceGainSoundAsync(CancellationTokenSource lifetime)
    {
        try { await _session.PlayForceExperienceGainSoundAsync(PersonnelEffectChannel, lifetime.Token); }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            if (ReferenceEquals(_personnelEffect, lifetime))
                GD.PushWarning("Force experience sound: " + error.Message);
        }
    }
}
