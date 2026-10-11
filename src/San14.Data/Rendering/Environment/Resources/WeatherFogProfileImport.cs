using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Import once for the loaded seasonal package, after supported-branch admission.</summary>
internal static class WeatherFogProfileImport
{
    private static readonly ConditionalWeakTable<MapSeasonCameraPackage, WeatherFogProfile> profiles = new();
    internal static WeatherFogProfile FromPackage(MapSeasonCameraPackage package) => profiles.GetValue(package, Read);

    private static WeatherFogProfile Read(MapSeasonCameraPackage source)
    {
        var camera = source.Camera;
        float Field(int ordinal, int lane = 0)
        {
            var prop = camera.Properties.Single(x => x.Index == ordinal);
            if (prop.TypeCode != 8 || prop.ElementSize != 4 || prop.RawValue.Length != prop.ElementCount * 4 || lane >= prop.ElementCount)
                throw new InvalidDataException("Source Main weather property layout differs from its actual loader copy.");
            float value = BinaryPrimitives.ReadSingleLittleEndian(prop.RawValue.AsSpan(lane * 4, 4));
            if (!float.IsFinite(value)) throw new InvalidDataException("Source Main weather property is nonfinite.");
            return value;
        }
        return new()
        {
            CloudinessScale = Field(770),
            CloudHumidityLow = Field(879),
            CloudHumidityHigh = Field(880),
            FogStrength = Field(923),
            FogHeightBlend = Field(925),
            FogOpacity = Field(924),
            AtmosphereHumidityLow = Field(896),
            AtmosphereHumidityHigh = Field(897),
            WetRayleighScale = Field(899),
            DryRayleighScale = Field(898),
            FogDepthScale = Field(920),
            WetMieScale = Field(901),
            DryMieScale = Field(900),
            SaturationLow = Field(973),
            SaturationHigh = Field(974),
            FogStart = Field(921),
            Anisotropy = Field(595),
            FogPivot = Field(919),
            FogBlend = Field(914),
            FogBlendScale = Field(915),
            MieTint = new(Field(926, 0), Field(926, 1), Field(926, 2)),
            RayleighTint = new(Field(927, 0), Field(927, 1), Field(927, 2))
        };
    }
}
