using System.Runtime.CompilerServices;
using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The declaration is read once per loaded camera generation. Current
/// Main writes replace the corresponding values without retaining a raw camera tail.</summary>
public static class CameraOcclusionSettingsImport
{
    private static readonly ConditionalWeakTable<MapSeasonCameraPackage, CameraOcclusionSettings> Seasonal = new();

    public static CameraOcclusionSettings ReadCurrent(MapSourceCreatedSeasonLightingFrame present)
    {
        ArgumentNullException.ThrowIfNull(present);
        var selection = present.AcceptedSeasonCamera.Inputs.Source.Selection;
        if(selection.SecondPackage is not null)
            throw new NotSupportedException("Contact occlusion requires the actual directly selected camera controls; blended camera controls are not implemented.");
        // V468 copies the selected camera controls before Main applies its
        // sparse overrides. Main's own declaration is not that selected camera.
        var settings = Seasonal.GetValue(selection.FirstPackage, static package => ReadDefaults(package.Camera));
        var camera = present.CapturePreparedCamera();
        return camera.CurrentSeasonPostEffectScalars is { } current
            ? ApplyPreparedOverrides(settings, current.EncodedWrites) : settings;
    }

    private static CameraOcclusionSettings ReadDefaults(DokObject camera)
    {
        uint Word(uint hash, uint type = 4)
        {
            var fields = camera.Properties.Where(field => field.NameHash == hash).ToArray();
            if(fields.Length != 1 || fields[0].TypeCode != type || fields[0].ElementCount != 1 ||
                fields[0].ElementSize != 4 || fields[0].RawValue.Length != 4)
                throw new InvalidDataException($"Selected camera contact-occlusion control {hash:X8} requires one declared typed scalar.");
            return BinaryPrimitives.ReadUInt32LittleEndian(fields[0].RawValue);
        }
        float Scalar(uint hash) => BitConverter.UInt32BitsToSingle(Word(hash, 8));
        return new(Scalar(0x87F5F380), Scalar(0xA08F0C41),
            Scalar(0xA9CCE123), Scalar(0x58A92F8A),
            Scalar(0x77303958), Scalar(0xF3EC5CB0),
            Scalar(0x1C4D4FB7), Scalar(0x48DBFB42),
            Word(0xF1D2118F), Word(0x5A3E34DA),
            Word(0xAB2AB5A0), Word(0x6A19DEC5),
            Word(0xECDABC2A), Word(0x25BDFB14),
            Word(0x509335B6), Scalar(0x0DB06614),
            Word(0xC0416472), Word(0x6F313FDC), Word(0xE614803F));
    }

    /// <summary>Interchange boundary for the sparse prepared-camera writers.</summary>
    public static CameraOcclusionSettings ApplyPreparedOverrides(CameraOcclusionSettings settings,
        IEnumerable<KeyValuePair<int, uint>> writes)
    {
        foreach (var (offset, bits) in writes)
        {
            float scalar = BitConverter.UInt32BitsToSingle(bits);
            settings = offset switch
            {
                0xF60 => settings with { Radius = scalar },
                0xF64 => settings with { EndRadius = scalar },
                0xF68 => settings with { RangeStart = scalar },
                0xF6C => settings with { RangeEnd = scalar },
                0xF70 => settings with { MaxRadiusRate = scalar },
                0xF74 => settings with { Contrast = scalar },
                0xF78 => settings with { MaxOcclusion = scalar },
                0xF7C => settings with { AngleBiasDegrees = scalar },
                0xF80 => settings with { Quality = bits },
                0xF84 => settings with { Resolution = bits },
                0xF88 => settings with { UseNormal = bits },
                0xF8C => settings with { UseViewDepth = bits },
                0xF90 => settings with { RayCount = bits },
                0xF94 => settings with { StepCount = bits },
                0xF98 => settings with { BlurSize = bits },
                0xF9C => settings with { BlurSharpness = scalar },
                0xFA0 => settings with { BlurLinearFilter = bits },
                0xFA4 => settings with { BlurAtHalfSize = bits },
                0xFAC => settings with { SuppressHalo = bits },
                _ => settings
            };
        }
        return settings;
    }
}
