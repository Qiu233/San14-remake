using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>One current climate and quality preparation. It contains raw layer
/// periods; shader clamps and retained animation phases belong to consumers.</summary>
public sealed class SkyCloudWeatherState
{
    private SkyCloudWeatherState(SkyCloudWeatherSettings settings, float cloudiness,
        float humidity, float saturation, uint lowCostLevel, uint resolution,
        float coverage, float detailAmount, float erosion, float density,
        Vector3 detailPeriod, Vector3 detailVelocity, float cirrusCoverage,
        float cirrusDistortion)
    {
        Settings = settings; Cloudiness = cloudiness; Humidity = humidity;
        Saturation = saturation; LowCostLevel = lowCostLevel; Resolution = resolution;
        Coverage = coverage; DetailAmount = detailAmount; Erosion = erosion;
        Density = density; DetailPeriod = detailPeriod; DetailVelocity = detailVelocity;
        CirrusCoverage = cirrusCoverage; CirrusDistortion = cirrusDistortion;
    }
    public SkyCloudWeatherSettings Settings { get; }
    public float Cloudiness { get; }
    public float Humidity { get; }
    public float Saturation { get; }
    public uint LowCostLevel { get; }
    public uint Resolution { get; }
    public float Coverage { get; }
    public float DetailAmount { get; }
    public float Erosion { get; }
    public float Density { get; }
    public Vector3 DetailPeriod { get; }
    public Vector3 DetailVelocity { get; }
    public float CirrusCoverage { get; }
    public float CirrusDistortion { get; }
    public SkyCloudNoiseLayer3 Base => Settings.Base;
    public SkyCloudNoiseLayer3 Detail => new(DetailPeriod, Settings.DetailPosition, DetailVelocity);
    public SkyCloudNoiseLayer2 Distort => Settings.Distort;
    public SkyCloudNoiseLayer2 Cirrus => Settings.Cirrus;
    public SkyCloudNoiseLayer2 CirrusDistort => Settings.CirrusDistort;

    public static SkyCloudWeatherState Prepare(SkyCloudWeatherSettings settings,
        float cloudiness, float humidity, float saturation,
        uint? lowCostLevel = null, uint? resolution = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!float.IsFinite(cloudiness) || !float.IsFinite(humidity))
            throw new InvalidDataException("Cloud final weather inputs are nonfinite.");
        float coverage = Clamp(M(cloudiness, settings.CoverageFactor));
        float complement = S(1, cloudiness);
        float erosion = Clamp(A(M(complement, settings.Erosion.Clear),
            M(cloudiness, settings.Erosion.Cloudy)));
        float humidityWeight = Weight(humidity, settings.HumidityResponse.Lower, settings.HumidityResponse.Upper);
        coverage = M(coverage, humidityWeight); erosion = M(erosion, humidityWeight);
        float density = A(M(humidity, Max(settings.Density.Cloudy, settings.Density.Clear)),
            M(S(1, humidity), settings.Density.Clear));
        float h = Weight(humidity, settings.HumidDetailThreshold, 1);
        float detail = A(M(S(1, h), settings.DetailAmount.Clear), M(h, settings.DetailAmount.Cloudy));
        float periodWeight = Weight(coverage, settings.DetailPeriod.Response.Lower, settings.DetailPeriod.Response.Upper);
        float velocityWeight = Weight(coverage, settings.DetailVelocity.Response.Lower, settings.DetailVelocity.Response.Upper);
        Vector3 period = Interpolate(settings.DetailPeriod, periodWeight);
        Vector3 velocity = Interpolate(settings.DetailVelocity, velocityWeight);
        float cirrusStrength = Clamp(A(M(complement, settings.CirrusCoverage.Clear),
            M(cloudiness, settings.CirrusCoverage.Cloudy)));
        float distortion = Clamp(A(M(complement, settings.CirrusDistortion.Clear),
            M(cloudiness, settings.CirrusDistortion.Cloudy)));
        float humidityCirrus = Weight(humidity, settings.CirrusHumidityResponse.Lower, settings.CirrusHumidityResponse.Upper);
        float cirrusCoverage = Finite(M(S(1, humidityCirrus), cirrusStrength));
        float cirrusDistortion = Finite(M(S(1, humidityCirrus), distortion));
        return new(settings, cloudiness, humidity, saturation,
            lowCostLevel ?? settings.LowCostLevel, resolution ?? settings.Resolution,
            Finite(coverage), Finite(detail), Finite(erosion), Finite(density),
            period, velocity, cirrusCoverage, cirrusDistortion);
    }
    private static Vector3 Interpolate(SkyCloudVectorResponse3 endpoints, float weight) => new(
        Finite(A(M(S(1, weight), endpoints.Clear.X), M(weight, endpoints.Cloudy.X))),
        Finite(A(M(S(1, weight), endpoints.Clear.Y), M(weight, endpoints.Cloudy.Y))),
        Finite(A(M(S(1, weight), endpoints.Clear.Z), M(weight, endpoints.Cloudy.Z))));
    private static float Finite(float value) => float.IsFinite(value) ? value :
        throw new InvalidDataException("Cloud current weather result is nonfinite.");
    private static float Weight(float x, float low, float high) => high > low ? Clamp(D(S(x, low), S(high, low))) : x >= low ? 1 : 0;
    private static float Clamp(float x) => x >= 1 ? 1 : Max(0, x);
    private static float Max(float a, float b) => a > b ? a : b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float A(float a, float b) => a + b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float S(float a, float b) => a - b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float M(float a, float b) => a * b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float D(float a, float b) => a / b;
}
