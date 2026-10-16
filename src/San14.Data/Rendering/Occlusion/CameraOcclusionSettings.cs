namespace San14.Data;

/// <summary>Current camera controls for screen-space contact occlusion.
/// Resource declarations and sparse Main updates are imported before rendering.</summary>
public sealed record CameraOcclusionSettings(
    float Radius, float EndRadius, float RangeStart, float RangeEnd,
    float MaxRadiusRate, float Contrast, float MaxOcclusion, float AngleBiasDegrees,
    uint Quality, uint Resolution, uint UseNormal, uint UseViewDepth,
    uint RayCount, uint StepCount, uint BlurSize, float BlurSharpness,
    uint BlurLinearFilter, uint BlurAtHalfSize, uint SuppressHalo);
