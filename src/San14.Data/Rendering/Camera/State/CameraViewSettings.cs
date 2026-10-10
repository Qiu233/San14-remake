namespace San14.Data;

/// <summary>Current camera choices used when preparing a map View.</summary>
public sealed record CameraViewSettings(CameraShadowViewSettings Shadow,
    uint OceanGroup, uint RainGroup);

/// <summary>Shadow selection and cascade controls shared by View preparation
/// and the native current-camera transport.</summary>
public readonly record struct CameraShadowViewSettings(
    float MaximumDistance, float FirstSplit, float SecondSplit, float ThirdSplit,
    int LightGroup, uint DirectionSelector, float DirectionScale, float FarOverride,
    uint DepthFormat, uint CascadeMode, uint RectangleOverride, uint Quality);
