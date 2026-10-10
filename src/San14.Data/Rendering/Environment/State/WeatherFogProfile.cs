using System.Numerics;

namespace San14.Data;

/// <summary>Immutable selected declaration values consumed by the supported weather/Fog branch.</summary>
public sealed record WeatherFogProfile
{
    public float CloudinessScale { get; init; }
    public float CloudHumidityLow { get; init; }
    public float CloudHumidityHigh { get; init; }
    public float FogStrength { get; init; }
    public float FogHeightBlend { get; init; }
    public float FogOpacity { get; init; }
    public float AtmosphereHumidityLow { get; init; }
    public float AtmosphereHumidityHigh { get; init; }
    public float WetRayleighScale { get; init; }
    public float DryRayleighScale { get; init; }
    public float FogDepthScale { get; init; }
    public float WetMieScale { get; init; }
    public float DryMieScale { get; init; }
    public float SaturationLow { get; init; }
    public float SaturationHigh { get; init; }
    public float FogStart { get; init; }
    public float Anisotropy { get; init; }
    public float FogPivot { get; init; }
    public float FogBlend { get; init; }
    public float FogBlendScale { get; init; }
    public Vector3 MieTint { get; init; }
    public Vector3 RayleighTint { get; init; }
}
