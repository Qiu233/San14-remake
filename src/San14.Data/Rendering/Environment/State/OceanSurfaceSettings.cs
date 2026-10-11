using System.Numerics;

namespace San14.Data;

/// <summary>Current water-plane, visibility and animated material controls.
/// These are settings; elapsed time and completed wave jobs belong to the renderer.</summary>
public sealed record OceanSurfaceSettings(float WaterHeight, float CutoffDistance,
    Vector2 TextureWaveDirection, float TextureWaveDisplacement, float NormalAnimationSpeed,
    uint BubbleMaskMode, float SurfaceBubblePeriod, float InnerBubblePeriod);
