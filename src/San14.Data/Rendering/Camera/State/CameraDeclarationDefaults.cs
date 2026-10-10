namespace San14.Data;

/// <summary>Unparented pose and perspective settings imported from a camera resource.</summary>
public sealed record PerspectiveCameraDeclaration(MapCameraCurrentPose Pose, CameraLens Lens, float LookDistance);

public sealed record CameraAmbientDefaults(float Frequency, float InterpolationRate,
    uint FilterSampleCount, uint TriggerMode);

/// <summary>Declaration defaults remain distinct from current frame settings.</summary>
public sealed record CameraDeclarationDefaults(PerspectiveCameraDeclaration Perspective,
    CameraAmbientDefaults Ambient, uint DepthOfFieldMode, float FogLightingBlend)
{
    public const uint InitialExposureRectangleFlags = 0;
}
