using Godot;

internal sealed partial class SceneAudioBinding
{
    private long _monthlySpatialSequence;

    internal Task PlayMonthlyTerritorySoundAsync(int kind, Node3D world, Vector3 position,
        Func<float> cameraDistance, CancellationToken cancellationToken) => _session.PlayMonthlyTerritorySoundAsync(kind,
        "territory-effect:" + unchecked(++_monthlySpatialSequence), world, position, cameraDistance, cancellationToken);
}
