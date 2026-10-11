using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Import the selected Camera's PB2 bubble declarations once per resource
/// generation. Required author values retain their source bits; setter clamps,
/// wrapper admission and current material composition belong to the renderer.</summary>
public static class OceanBubbleMaterialSettingsImport
{
    private static readonly ConditionalWeakTable<MapSeasonCameraPackage, OceanBubbleMaterialSettings> imported = new();

    public static OceanBubbleMaterialSettings Load(MapSeasonCameraPackage package)
        => imported.GetValue(package, static value => Read(value));

    private static OceanBubbleMaterialSettings Read(MapSeasonCameraPackage package)
    {
        ReadOnlySpan<byte> Floats(uint name, int count)
        {
            var field = package.Camera.Properties.Single(value => value.NameHash == name);
            if (field.TypeCode != 8 || field.ElementCount != count || field.ElementSize != 4 || field.RawValue.Length != count * 4)
                throw new InvalidDataException($"Ocean bubble setting {name:X8} requires its float declaration shape.");
            return field.RawValue;
        }
        float Scalar(uint name) => BinaryPrimitives.ReadSingleLittleEndian(Floats(name, 1));
        Vector3 Color(uint name)
        {
            var bytes = Floats(name, 3);
            return new(BinaryPrimitives.ReadSingleLittleEndian(bytes),
                BinaryPrimitives.ReadSingleLittleEndian(bytes[4..]), BinaryPrimitives.ReadSingleLittleEndian(bytes[8..]));
        }
        OceanBubbleLayerSettings Layer(uint rgb, uint alpha, uint reflectance, uint roughness,
            uint threshold, uint normalThreshold)
            => new(new Vector4(Color(rgb), Scalar(alpha)), Color(reflectance), Scalar(roughness),
                Scalar(threshold), Scalar(normalThreshold));

        // These names describe the actual destination roles. The selected Camera
        // installs WaterTransparencyDampScale in the color/depth alpha lane,
        // AlphaThreshold in the band/shore banishing lane, and BanishingScale
        // in the shoaling damping lane. Preserve that application mapping.
        var surface = Layer(0x6B55B8E6, 0x89EFE185, 0x34153F4F, 0x773AEC13, 0xD85DAF68, 0x07916B47);
        var inner = Layer(0x7381211D, 0x297AE57C, 0x20112DC6, 0x77833B8A, 0x0FB8D01F, 0x5A17F7BE);
        var shore = Layer(0x91FD1326, 0x47F6D785, 0x3E8D1FCF, 0x95FF2D93, 0x2E34C228, 0x7893E9C7);
        var surfaceBand = new OceanBubbleBandSettings(Scalar(0xB73A4D31), Scalar(0x9162CE1D),
            Scalar(0xD74A4C54), Scalar(0xF10226B6));
        var innerBand = new OceanBubbleBandSettings(Scalar(0xB3844C28), Scalar(0x59701814),
            Scalar(0x34BFB80B), Scalar(0x660EC2ED));
        var shoreParameters = new OceanShoreBubbleSettings(Scalar(0x9B97B076), Scalar(0xF1A4F366),
            Scalar(0xDC68EB9B), Scalar(0x848AB4F6));
        var shoaling = new OceanBubbleShoalingSettings(Scalar(0xA0A3A0A2),
            new(Scalar(0x6FACC2CC), Scalar(0x15BD6C83), Scalar(0x34395E8C)));
        var scattering = new Vector4(Color(0x3C645EC4), Scalar(0xAD554BE2));
        // SpawnScale reaches point.z; LifeSpan supplies envelope duration and
        // the initial velocity offset time. They are independent source values.
        var spawn = new OceanBubbleSpawnSettings(Scalar(0x441989FE), Scalar(0x15C07D0F),
            Scalar(0xDBA15F32), Scalar(0x01C9318A), Scalar(0x0049CA15));
        return new(surface, inner, shore, surfaceBand, innerBand, shoreParameters, shoaling,
            Scalar(0xEE77E8CC), scattering,
            new(Scalar(0xDCF269CC), Scalar(0xC7E6B64E)),
            new(Scalar(0x9640D383), Scalar(0xCB825885)),
            new(Scalar(0x1531E846), Scalar(0xFDB05904)),
            new(Scalar(0x0399C1B3), Scalar(0x7DD949F1)),
            new(Scalar(0xA278BE80), Scalar(0x9AFCC537), Scalar(0xB978B740)), spawn,
            new(Scalar(0x4C4E1341), Scalar(0xABF25638)),
            // WaveHeightFadingSpan is installed in the Generator-annotated
            // fourth texture frame period, independently of the two band phases.
            Scalar(0xADBA7046));
    }
}
