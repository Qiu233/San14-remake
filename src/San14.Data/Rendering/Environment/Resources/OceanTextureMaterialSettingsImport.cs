using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Import the selected camera's texture material once per resource
/// generation. Texture availability and consumer clamps belong to the renderer.</summary>
public static class OceanTextureMaterialSettingsImport
{
    private static readonly ConditionalWeakTable<MapSeasonCameraPackage, OceanTextureMaterialSettings> imported = new();

    public static OceanTextureMaterialSettings Load(MapSeasonCameraPackage package)
        => imported.GetValue(package, static value => Read(value));

    private static OceanTextureMaterialSettings Read(MapSeasonCameraPackage package)
    {
        float Scalar(uint name)
        {
            var field = package.Camera.Properties.Single(value => value.NameHash == name);
            if (field.TypeCode != 8 || field.ElementCount != 1 || field.ElementSize != 4 || field.RawValue.Length != 4)
                throw new InvalidDataException($"Ocean texture material {name:X8} requires a scalar float declaration.");
            return BinaryPrimitives.ReadSingleLittleEndian(field.RawValue);
        }
        var uv = new OceanTextureUvSettings(Scalar(0xBE5655A1),
            new(Scalar(0x65480231), Scalar(0x4DC672EF)),
            new(Scalar(0x42AA9697), Scalar(0xD4AC8519)),
            new(Scalar(0x3FEE7F84), Scalar(0xEE2A6142)));
        var wave = new OceanTextureWaveSettings(Scalar(0xD1D1D5F2), Scalar(0x1FE84309),
            Scalar(0x4760B235), Scalar(0xD1ED31F5), Scalar(0xC98FEBB6),
            Scalar(0x2F8555ED), Scalar(0xE2A4B436));
        return new(uv, wave);
    }
}
