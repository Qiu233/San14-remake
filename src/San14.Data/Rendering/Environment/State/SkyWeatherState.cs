using System.Numerics;

namespace San14.Data;

public sealed record CameraWeatherClimate(float Cloudiness, float Temperature, float Humidity, float Wind);
public sealed record SkyWeatherFrameInputs(CameraWeatherClimate Climate, float EyeHeight, float Altitude);
public sealed record SkyWeatherResult(SkyWeatherKnownFogRows Fog, float Strength, float Saturation);

/// <summary>The known fog values. The allocation's unknown trailing bytes are not state.</summary>
public sealed record SkyWeatherFogValues(Vector4 Background, float Start, float End, float PlanetRadius,
    Vector3 RayleighCoefficients, float Density, Vector3 MieCoefficients, float Anisotropy,
    float Pivot, float Blend);
