using System.Numerics;

namespace San14.Data;

/// <summary>Adapt prepared-camera words to typed fog settings at publication.</summary>
public static class HeightFogSettingsEncoding
{
    public static HeightFogSettings ApplyPreparedOverrides(HeightFogSettings settings,
        IReadOnlyDictionary<int, uint> current)
    {
        uint U(int offset, uint fallback) => current.TryGetValue(offset, out uint bits) ? bits : fallback;
        float F(int offset, float fallback) => BitConverter.UInt32BitsToSingle(U(offset, BitConverter.SingleToUInt32Bits(fallback)));
        return new(
            new HeightFogLayerSettings(
            U(0x84C, settings.FirstLayer.Mode),
            U(0x840, settings.FirstLayer.DistanceFadeEnabled),
            F(0x844, settings.FirstLayer.DistanceStart),
            F(0x848, settings.FirstLayer.DistanceEnd),
            F(0x850, settings.FirstLayer.HeightStart),
            F(0x854, settings.FirstLayer.HeightEnd),
            new Vector4(F(0x858, settings.FirstLayer.Color.X), F(0x85C, settings.FirstLayer.Color.Y), F(0x860, settings.FirstLayer.Color.Z), F(0x864, settings.FirstLayer.Color.W)),
            F(0x88C, settings.FirstLayer.Velocity),
            new HeightFogWaveSettings(F(0x890, settings.FirstLayer.FirstWave.Amplitude), F(0x894, settings.FirstLayer.FirstWave.FrequencyX), F(0x898, settings.FirstLayer.FirstWave.VelocityX), F(0x89C, settings.FirstLayer.FirstWave.FrequencyY), F(0x8A0, settings.FirstLayer.FirstWave.VelocityY)),
            new HeightFogWaveSettings(F(0x8A4, settings.FirstLayer.SecondWave.Amplitude), F(0x8A8, settings.FirstLayer.SecondWave.FrequencyX), F(0x8AC, settings.FirstLayer.SecondWave.VelocityX), F(0x8B0, settings.FirstLayer.SecondWave.FrequencyY), F(0x8B4, settings.FirstLayer.SecondWave.VelocityY)),
            new HeightFogWaveSettings(F(0x8B8, settings.FirstLayer.ThirdWave.Amplitude), F(0x8BC, settings.FirstLayer.ThirdWave.FrequencyX), F(0x8C0, settings.FirstLayer.ThirdWave.VelocityX), F(0x8C4, settings.FirstLayer.ThirdWave.FrequencyY), F(0x8C8, settings.FirstLayer.ThirdWave.VelocityY)),
            new HeightFogWaveSettings(F(0x8CC, settings.FirstLayer.FourthWave.Amplitude), F(0x8D0, settings.FirstLayer.FourthWave.FrequencyX), F(0x8D4, settings.FirstLayer.FourthWave.VelocityX), F(0x8D8, settings.FirstLayer.FourthWave.FrequencyY), F(0x8DC, settings.FirstLayer.FourthWave.VelocityY))),
            new HeightFogLayerSettings(
            U(0x868, settings.SecondLayer.Mode),
            U(0x8E0, settings.SecondLayer.DistanceFadeEnabled),
            F(0x8E4, settings.SecondLayer.DistanceStart),
            F(0x8E8, settings.SecondLayer.DistanceEnd),
            F(0x86C, settings.SecondLayer.HeightStart),
            F(0x870, settings.SecondLayer.HeightEnd),
            new Vector4(F(0x874, settings.SecondLayer.Color.X), F(0x878, settings.SecondLayer.Color.Y), F(0x87C, settings.SecondLayer.Color.Z), F(0x880, settings.SecondLayer.Color.W)),
            F(0x8EC, settings.SecondLayer.Velocity),
            new HeightFogWaveSettings(F(0x8F0, settings.SecondLayer.FirstWave.Amplitude), F(0x8F4, settings.SecondLayer.FirstWave.FrequencyX), F(0x8F8, settings.SecondLayer.FirstWave.VelocityX), F(0x8FC, settings.SecondLayer.FirstWave.FrequencyY), F(0x900, settings.SecondLayer.FirstWave.VelocityY)),
            new HeightFogWaveSettings(F(0x904, settings.SecondLayer.SecondWave.Amplitude), F(0x908, settings.SecondLayer.SecondWave.FrequencyX), F(0x90C, settings.SecondLayer.SecondWave.VelocityX), F(0x910, settings.SecondLayer.SecondWave.FrequencyY), F(0x914, settings.SecondLayer.SecondWave.VelocityY)),
            new HeightFogWaveSettings(F(0x918, settings.SecondLayer.ThirdWave.Amplitude), F(0x91C, settings.SecondLayer.ThirdWave.FrequencyX), F(0x920, settings.SecondLayer.ThirdWave.VelocityX), F(0x924, settings.SecondLayer.ThirdWave.FrequencyY), F(0x928, settings.SecondLayer.ThirdWave.VelocityY)),
            new HeightFogWaveSettings(F(0x92C, settings.SecondLayer.FourthWave.Amplitude), F(0x930, settings.SecondLayer.FourthWave.FrequencyX), F(0x934, settings.SecondLayer.FourthWave.VelocityX), F(0x938, settings.SecondLayer.FourthWave.FrequencyY), F(0x93C, settings.SecondLayer.FourthWave.VelocityY))),
            new HeightFogLayerSettings(
            U(0x94C, settings.ThirdLayer.Mode),
            U(0x940, settings.ThirdLayer.DistanceFadeEnabled),
            F(0x944, settings.ThirdLayer.DistanceStart),
            F(0x948, settings.ThirdLayer.DistanceEnd),
            F(0x950, settings.ThirdLayer.HeightStart),
            F(0x954, settings.ThirdLayer.HeightEnd),
            new Vector4(F(0x958, settings.ThirdLayer.Color.X), F(0x95C, settings.ThirdLayer.Color.Y), F(0x960, settings.ThirdLayer.Color.Z), F(0x964, settings.ThirdLayer.Color.W)),
            F(0x968, settings.ThirdLayer.Velocity),
            new HeightFogWaveSettings(F(0x96C, settings.ThirdLayer.FirstWave.Amplitude), F(0x970, settings.ThirdLayer.FirstWave.FrequencyX), F(0x974, settings.ThirdLayer.FirstWave.VelocityX), F(0x978, settings.ThirdLayer.FirstWave.FrequencyY), F(0x97C, settings.ThirdLayer.FirstWave.VelocityY)),
            new HeightFogWaveSettings(F(0x980, settings.ThirdLayer.SecondWave.Amplitude), F(0x984, settings.ThirdLayer.SecondWave.FrequencyX), F(0x988, settings.ThirdLayer.SecondWave.VelocityX), F(0x98C, settings.ThirdLayer.SecondWave.FrequencyY), F(0x990, settings.ThirdLayer.SecondWave.VelocityY)),
            new HeightFogWaveSettings(F(0x994, settings.ThirdLayer.ThirdWave.Amplitude), F(0x998, settings.ThirdLayer.ThirdWave.FrequencyX), F(0x99C, settings.ThirdLayer.ThirdWave.VelocityX), F(0x9A0, settings.ThirdLayer.ThirdWave.FrequencyY), F(0x9A4, settings.ThirdLayer.ThirdWave.VelocityY)),
            new HeightFogWaveSettings(F(0x9A8, settings.ThirdLayer.FourthWave.Amplitude), F(0x9AC, settings.ThirdLayer.FourthWave.FrequencyX), F(0x9B0, settings.ThirdLayer.FourthWave.VelocityX), F(0x9B4, settings.ThirdLayer.FourthWave.FrequencyY), F(0x9B8, settings.ThirdLayer.FourthWave.VelocityY))),
            new Vector4(F(0x9BC, settings.Mask.X), F(0x9C0, settings.Mask.Y), F(0x9C4, settings.Mask.Z), F(0x9C8, settings.Mask.W)));
    }
}
