using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Selected Camera declarations, imported once per loaded generation.
/// NormalTextureScaleZ supplies the source normal animation speed. The resource
/// TextureWaveScale controls displacement of the wave noise and visibility bounds. Consumer
/// clamps and direction normalization belong to the water renderer.</summary>
public static class OceanSurfaceSettingsImport
{
    private static readonly ConditionalWeakTable<MapSeasonCameraPackage, OceanSurfaceSettings> imported = new();

    public static OceanSurfaceSettings Load(MapSeasonCameraPackage package)
        => imported.GetValue(package, static value => Read(value));

    private static OceanSurfaceSettings Read(MapSeasonCameraPackage package)
    {
        uint Scalar(uint name, uint type)
        {
            var field = package.Camera.Properties.Single(value => value.NameHash == name);
            if (field.TypeCode != type || field.ElementCount != 1 || field.ElementSize != 4 || field.RawValue.Length != 4)
                throw new InvalidDataException($"Ocean surface setting {name:X8} requires its scalar declaration.");
            return BinaryPrimitives.ReadUInt32LittleEndian(field.RawValue);
        }
        float Float(uint name) => BitConverter.UInt32BitsToSingle(Scalar(name, 8));
        return new(Float(0xAE5641C9), Float(0x0CAE2711),
            new Vector2(Float(0xF80A0E7E), Float(0xC5BB8E00)), Float(0xA4518EB9),
            Float(0xA6D4C65F), Scalar(0x6CC7F804, 5), Float(0x13E8E0B4), Float(0x0EB7E46B));
    }
}
