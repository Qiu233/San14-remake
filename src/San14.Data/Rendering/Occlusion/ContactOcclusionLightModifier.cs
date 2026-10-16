namespace San14.Data;

/// <summary>Only the modifier values consumed by contact occlusion. SuppressCopy
/// preserves the initialized group values; it does not mean the light is absent.</summary>
public sealed record ContactOcclusionLightModifier(bool Present, bool SuppressCopy,
    float Fade, float Radius, float EndRadius, float RangeStart, float RangeEnd,
    float MaxRadiusRate, float Contrast, float MaxOcclusion, float AngleBias, float StepCount);
