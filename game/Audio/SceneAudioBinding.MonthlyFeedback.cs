using Godot;
using San14.Data;

internal sealed partial class SceneAudioBinding
{
    internal Task<NormalSoundAdmission> PlayCaptureSoundAsync(int managerId, Node3D world, Vector3 position,
        SpatialSoundMode mode, Func<float> cameraDistance, CancellationToken cancellationToken) =>
        _session.PlayCaptureSoundAsync(managerId, world, position, mode, cameraDistance, cancellationToken);
}
