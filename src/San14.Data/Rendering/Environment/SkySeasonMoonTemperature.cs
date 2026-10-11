using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Canonical seasonal Camera property939. This is a source declaration,
/// copied by C358A0/C40C16 to prepared21C; it does not read or replace later
/// current Camera edits. The supported stock6500 lookup has three nonzero RGB
/// channels, identical to the native reference lookup, so C3F880 ratios are1.</summary>
public static class SkySeasonMoonTemperature
{
    public const int PropertyOrdinal = 939;
    public const uint PropertyHash = 0x3C750447;
    public const int SourceCpuOffset = 0xC8;
    public const int PreparedCameraOffset = 0x21C;
    public const uint SupportedKelvin = 6500;
    public const string Native6500TableRowHex = "641900000000803FC1CA713F9F3C7C3F";
    public const string EvidencePath = SkyAtmoTableResources.NativeEvidence;

    public static uint ReadKelvin(MapSeasonCameraPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        var property = package.Camera.Properties[PropertyOrdinal];
        if (property.Index != PropertyOrdinal || property.NameHash != PropertyHash || property.TypeCode != 4 ||
            property.ElementCount != 1 || property.ElementSize != 4 || property.RawValue.Length != 4 ||
            BinaryPrimitives.ReadUInt32LittleEndian(property.RawValue) != SupportedKelvin)
            throw new InvalidDataException("Seasonal Moon temperature requires the exact canonical Camera939 uint6500 source.");
        return SupportedKelvin;
    }

    public static uint ReadKelvin(MapSeasonCameraSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection); selection.Validate();
        uint first = ReadKelvin(selection.FirstPackage);
        if (selection.SecondPackage is not null)
        {
            uint second = ReadKelvin(selection.SecondPackage);
            // C9B9F6..C9BA35: source int32 -> float32, two separate
            // products, sum, +.5, native floor, then signed truncation.
            float blended = NativeFloat.Add(
                NativeFloat.Multiply(first, NativeFloat.Subtract(1, selection.Weight)),
                NativeFloat.Multiply(second, selection.Weight));
            if (!float.IsFinite(blended) || NativeFloatRounding.Floor101EB80(
                NativeFloat.Add(blended, .5f)) != SupportedKelvin)
                throw new InvalidDataException("Explicit Camera pair interpolation changes the bounded native6500 identity temperature.");
        }
        return first;
    }

    public static string Explain(MapSeasonCameraPackage package)
    {
        ReadKelvin(package);
        return package.SourceEvidence + $"#property{PropertyOrdinal}/{PropertyHash:X8}/type4/count1=6500; " +
            "TC C52B09 loader -> sourceCPU C8; C35A40/C35A47 unconditional chosen-package copy; " +
            "C40C16/C40C1C -> preparedCamera21C; C3F880/1147EE0 same6500 native lookup; " +
            "nonzeroRGB row " + Native6500TableRowHex + "; three binary32 ratios=1; source-only/current edits explicit; " + EvidencePath;
    }
}
