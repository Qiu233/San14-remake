using System.Numerics;

namespace San14.Data;

/// <summary>World transform published by the map controller.</summary>
public readonly record struct MapCameraCurrentPose(Vector3 Position80,
    Quaternion Quaternion88, Vector3 Scale90);

/// <summary>One prepared view. Input picking, scene publication and lens changes
/// use these values; the native representation is derived at the output boundary.</summary>
public readonly record struct MapCameraGeometry(
    MapCameraCurrentPose Pose, Vector4 Eye, Vector4 Target, Vector4 Up,
    CameraLens Lens, MapCameraConstructionMatrices Matrices)
{
    public DeferredCameraInputs SceneInputs =>
        new(Matrices.WorldToView80, Matrices.WorldToProjection100, Eye);

    public MapCameraGeometry WithLens(CameraLens lens, uint clipProfile,
        NativeCameraTangentDispatch tangentDispatch) => this with
    {
        Lens = lens,
        Matrices = MapCameraConstructionMath.CompleteAB6C60(
            Eye, Target, Up, lens, clipProfile, tangentDispatch)
    };
}
