using System.Numerics;

namespace San14.Data;

public readonly record struct HeightFogWaveSettings(float Amplitude, float FrequencyX,
    float VelocityX, float FrequencyY, float VelocityY);

public readonly record struct HeightFogLayerSettings(uint Mode, uint DistanceFadeEnabled,
    float DistanceStart, float DistanceEnd, float HeightStart, float HeightEnd, Vector4 Color,
    float Velocity, HeightFogWaveSettings FirstWave, HeightFogWaveSettings SecondWave,
    HeightFogWaveSettings ThirdWave, HeightFogWaveSettings FourthWave)
{
    public HeightFogWaveSettings Wave(int index) => index switch
    {
        0 => FirstWave, 1 => SecondWave, 2 => ThirdWave, 3 => FourthWave,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };
}

public sealed record HeightFogSettings(HeightFogLayerSettings FirstLayer, HeightFogLayerSettings SecondLayer,
    HeightFogLayerSettings ThirdLayer, Vector4 Mask)
{
    public HeightFogLayerSettings Layer(int index) => index switch
    {
        0 => FirstLayer, 1 => SecondLayer, 2 => ThirdLayer,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };
}
