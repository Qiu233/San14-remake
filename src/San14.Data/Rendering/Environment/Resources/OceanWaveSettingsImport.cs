using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Import once for each selected camera resource generation. The
/// direct Main selection copies these six declarations without transforming them.</summary>
public static class OceanWaveSettingsImport
{
    private static readonly ConditionalWeakTable<MapSeasonCameraPackage, OceanWaveSettings> imported = new();

    public static OceanWaveSettings Load(MapSeasonCameraPackage package)
        => imported.GetValue(package, static value => Read(value));

    private static OceanWaveSettings Read(MapSeasonCameraPackage package)
    {
        float Scalar(uint name)
        {
            var field = package.Camera.Properties.Single(value => value.NameHash == name);
            if (field.TypeCode != 8 || field.ElementCount != 1 || field.ElementSize != 4 || field.RawValue.Length != 4)
                throw new InvalidDataException($"Ocean wave setting {name:X8} requires a scalar float declaration.");
            return BinaryPrimitives.ReadSingleLittleEndian(field.RawValue);
        }
        return new(new Vector2(Scalar(0x9970EAA6), Scalar(0xEC10B3A8)),
            Scalar(0x80DBF720), Scalar(0x6572ADC1), Scalar(0x0838B36F), Scalar(0x9E838FF5));
    }
}
