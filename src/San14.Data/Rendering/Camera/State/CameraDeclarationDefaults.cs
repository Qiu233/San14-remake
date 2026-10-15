namespace San14.Data;

/// <summary>Unparented pose and perspective settings imported from a camera resource.</summary>
public sealed record PerspectiveCameraDeclaration(MapCameraCurrentPose Pose, CameraLens Lens, float LookDistance);

public sealed record CameraAmbientDefaults(float Frequency, float InterpolationRate,
    uint FilterSampleCount, uint TriggerMode);

/// <summary>Declaration defaults remain distinct from current frame settings.</summary>
public sealed record CameraDeclarationDefaults(PerspectiveCameraDeclaration Perspective,
    CameraAmbientDefaults Ambient, uint DepthOfFieldMode, float FogLightingBlend,
    CameraRefractionFilterSettings RefractionFilters)
{
    public const uint InitialExposureRectangleFlags = 0;
    public uint? ExternalDirectionalSelectorKey { get; init; }
    public uint SkyDomeSceneKey { get; init; }
}

/// <summary>Negative overrides use the loaded Common filter settings.</summary>
public readonly record struct CameraRefractionFilterSettings(
    int TransparencyResolutionOverride, int DepthFilterOverride);
