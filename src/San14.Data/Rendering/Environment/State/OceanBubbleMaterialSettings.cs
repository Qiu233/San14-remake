using System.Numerics;

namespace San14.Data;

/// <summary>Selected bubble author values in their actual consumer roles.
/// Animation, accepted texture ranges, spawned points and near/far composition
/// belong to the renderer. Resource names and layouts stay at the import boundary.</summary>
public sealed record OceanBubbleMaterialSettings(
    OceanBubbleLayerSettings Surface, OceanBubbleLayerSettings Inner, OceanBubbleLayerSettings Shore,
    OceanBubbleBandSettings SurfaceBand, OceanBubbleBandSettings InnerBand,
    OceanShoreBubbleSettings ShoreParameters, OceanBubbleShoalingSettings Shoaling,
    float Depth, Vector4 Scattering, Vector2 SurfaceScale, Vector2 InnerScale, Vector2 ShoreScale,
    Vector2 HorizontalScale, Vector3 AnimationSpeeds, OceanBubbleSpawnSettings Spawn, Vector2 MaskScales,
    float GeneratorPeriod);

/// <summary>One PB2 layer's appearance before the renderer composes its three color rows.</summary>
public sealed record OceanBubbleLayerSettings(Vector4 Color, Vector3 Reflectance, float Roughness,
    float AlphaThreshold, float NormalThreshold);

public sealed record OceanBubbleBandSettings(float BeginPhase, float Length, float FadingScale, float BanishingScale);

public sealed record OceanShoreBubbleSettings(float DisplacementScale, float NormalScale,
    float GlossinessDamping, float BanishingScale);

/// <summary>The requested shoaling value and three accepted transparency damping inputs.
/// The renderer owns the source setter's storage convention.</summary>
public sealed record OceanBubbleShoalingSettings(float Scale, Vector3 TransparencyDamping);

/// <summary>Controls for the retained point generator. Envelope duration also supplies
/// the initial velocity offset time; accepting new settings does not clear live points.</summary>
public sealed record OceanBubbleSpawnSettings(float PointScale, float EmissionPeriod,
    float SpeedMultiplier, float Amplitude, float EnvelopeDuration);
