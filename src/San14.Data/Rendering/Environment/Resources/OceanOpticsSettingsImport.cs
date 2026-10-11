using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Direct selected-Camera PB2 declarations, imported once per loaded
/// generation. Current Main quality overrides and the renderer's refractive
/// index setter are applied by their respective owners.</summary>
public static class OceanOpticsSettingsImport
{
    private static readonly ConditionalWeakTable<MapSeasonCameraPackage, OceanOpticsSettings> imported = new();

    public static OceanOpticsSettings Load(MapSeasonCameraPackage package)
        => imported.GetValue(package, static value => Read(value));

    private static OceanOpticsSettings Read(MapSeasonCameraPackage package)
    {
        float[] Floats(uint name, int count)
        {
            var field = package.Camera.Properties.Single(value => value.NameHash == name);
            if (field.TypeCode != 8 || field.ElementCount != count || field.ElementSize != 4 || field.RawValue.Length != count * 4)
                throw new InvalidDataException($"Ocean optical setting {name:X8} requires its float declaration shape.");
            return Enumerable.Range(0, count).Select(index =>
                BinaryPrimitives.ReadSingleLittleEndian(field.RawValue.AsSpan(index * 4, 4))).ToArray();
        }
        var albedo = Floats(0x124256BC, 4);
        var reflectance = Floats(0xC2674EF7, 3);
        return new(Floats(0x511CF646, 1)[0], Floats(0x2BF6DC8A, 1)[0],
            new(albedo[0], albedo[1], albedo[2], albedo[3]),
            new(reflectance[0], reflectance[1], reflectance[2]),
            Floats(0x45FBF1B3, 1)[0], Floats(0x981D6629, 1)[0]);
    }
}
