using System.Numerics;

namespace San14.Data;

public readonly record struct SkyCloudRange(float Lower, float Upper);
public readonly record struct SkyCloudEndpoints(float Clear, float Cloudy);
public readonly record struct SkyCloudNoiseLayer3(Vector3 Period, Vector3 Position, Vector3 Velocity);
public readonly record struct SkyCloudNoiseLayer2(Vector2 Period, Vector2 Position, Vector2 Velocity);
public readonly record struct SkyCloudVectorResponse3(Vector3 Clear, Vector3 Cloudy, SkyCloudRange Response);
/// <summary>Henyey-Greenstein phase-function controls (Hg in shader/native interfaces).</summary>
public readonly record struct SkyCloudHgSettings(float Asymmetry0, float Asymmetry1, float Weight, float Intensity);
/// <summary>Retained authored controls with no current weather shader consumer.</summary>
public readonly record struct SkyCloudTemporalSettings(float SourceFactorA, float SourceFactorB, uint Mode, float SourceFactorC);

/// <summary>Immutable authored Cloud declarations. Animation phases, clocks,
/// current weather and quality overrides belong to other owners.</summary>
public sealed record SkyCloudWeatherSettings
{
    public required float BottomAltitude { get; init; }
    public required float Thickness { get; init; }
    public required uint RaySamples { get; init; }
    public required uint MinimumRaySamples { get; init; }
    public required float CoverageFactor { get; init; }
    public required SkyCloudEndpoints DetailAmount { get; init; }
    public required float HumidDetailThreshold { get; init; }
    public required SkyCloudEndpoints Erosion { get; init; }
    public required SkyCloudEndpoints Density { get; init; }
    public required SkyCloudTemporalSettings Temporal { get; init; }
    public required uint LowCostLevel { get; init; }
    public required uint RayExponent { get; init; }
    public required uint Resolution { get; init; }
    public required float SampleStep { get; init; }
    public required SkyCloudHgSettings Hg { get; init; }
    public required SkyCloudNoiseLayer3 Base { get; init; }
    public required float BaseErosion { get; init; }
    public required SkyCloudVectorResponse3 DetailPeriod { get; init; }
    public required SkyCloudVectorResponse3 DetailVelocity { get; init; }
    public required Vector3 DetailPosition { get; init; }
    public required float DetailErosion { get; init; }
    public required SkyCloudNoiseLayer2 Distort { get; init; }
    public required Vector3 DistortAmplitude { get; init; }
    public required float CirrusAltitude { get; init; }
    public required SkyCloudEndpoints CirrusCoverage { get; init; }
    public required SkyCloudEndpoints CirrusDistortion { get; init; }
    public required float CirrusDistortionAuxiliary { get; init; }
    public required SkyCloudNoiseLayer2 Cirrus { get; init; }
    public required SkyCloudNoiseLayer2 CirrusDistort { get; init; }
    public required Vector2 CirrusDistortAmplitude { get; init; }
    public required SkyCloudRange HumidityResponse { get; init; }
    public required SkyCloudRange CirrusHumidityResponse { get; init; }
}
