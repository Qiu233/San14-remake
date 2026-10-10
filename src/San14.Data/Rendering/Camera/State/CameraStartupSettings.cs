namespace San14.Data;

/// <summary>Startup settings used by shadow preparation and the scene environment.</summary>
public sealed record CameraStartupSettings(ShadowStartupSettings Shadow, VegetationWindSettings Wind,
    float TerritoryReferenceHeight, uint ExposureRectangleFlags);

public readonly record struct ShadowStartupSettings(
    float MaximumDistance,
    float NearDistance,
    float FirstSplit,
    float SecondSplit,
    float ThirdSplit,
    uint LightGroup,
    uint DirectionSelector,
    float FirstDepthBias,
    float SecondDepthBias,
    float ThirdDepthBias,
    float FourthDepthBias,
    float DirectionScale,
    float FarOverride,
    uint DepthFormat,
    uint CascadeMode,
    uint PerCascadeBias,
    uint RectangleOverride,
    uint Quality);

public readonly record struct VegetationWindSettings(
    float DirectionX,
    float DirectionY,
    float DirectionZ,
    float DirectionW,
    float BaseStrength,
    float StrengthVariation,
    float RiseSeconds,
    float FallSeconds,
    float PeakSeconds,
    float MinimumStrength,
    float RestSeconds,
    float RestVariation,
    float JitterDegrees,
    float JitterFrequency,
    float InteractionExtent,
    float WindExtent,
    float StrengthScale);
