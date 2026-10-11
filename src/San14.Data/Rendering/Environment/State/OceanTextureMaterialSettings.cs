using System.Numerics;

namespace San14.Data;

/// <summary>Texture coordinates and the procedural wave material selected for
/// this camera generation. Animation and surface displacement have their own
/// current owners; resource and shader layouts stay outside these values.</summary>
public sealed record OceanTextureMaterialSettings(OceanTextureUvSettings Uv, OceanTextureWaveSettings Wave);

public sealed record OceanTextureUvSettings(float NormalScale, Vector2 NoiseScale,
    Vector2 HeightScale, Vector2 HeightOffset);

public sealed record OceanTextureWaveSettings(float Amplitude, float Length, float Width,
    float Scale, float Kurtosis, float Scatter, float ShoalingScale);
