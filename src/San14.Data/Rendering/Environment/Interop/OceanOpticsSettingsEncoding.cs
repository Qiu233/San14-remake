namespace San14.Data;

/// <summary>Initial and same-frame optical settings interchange. Original
/// Main locations stay at this boundary; material callbacks consume names.</summary>
public static class OceanOpticsSettingsEncoding
{
    public const string NativeFilename = "ocean-optics.bin";

    public static void WriteNativeInputs(string directory, OceanOpticsSettings settings)
    {
        Directory.CreateDirectory(directory);
        using var writer = new BinaryWriter(File.Create(Path.Combine(directory, NativeFilename)));
        writer.Write(0x31504F4Fu); // OOP1
        writer.Write(settings.RefractionIndex); writer.Write(settings.RefractionTransparency);
        writer.Write(settings.Albedo.X); writer.Write(settings.Albedo.Y);
        writer.Write(settings.Albedo.Z); writer.Write(settings.Albedo.W);
        writer.Write(settings.Reflectance.X); writer.Write(settings.Reflectance.Y); writer.Write(settings.Reflectance.Z);
        writer.Write(settings.Roughness); writer.Write(settings.DiffuseTransparency);
    }

    public static IEnumerable<KeyValuePair<int, uint>> MainWords(OceanOpticsSettings settings)
    {
        yield return new(0xB08, BitConverter.SingleToUInt32Bits(settings.RefractionIndex));
        yield return new(0xB0C, BitConverter.SingleToUInt32Bits(settings.RefractionTransparency));
        yield return new(0xB60, BitConverter.SingleToUInt32Bits(settings.Albedo.X));
        yield return new(0xB64, BitConverter.SingleToUInt32Bits(settings.Albedo.Y));
        yield return new(0xB68, BitConverter.SingleToUInt32Bits(settings.Albedo.Z));
        yield return new(0xB6C, BitConverter.SingleToUInt32Bits(settings.Albedo.W));
        yield return new(0xB70, BitConverter.SingleToUInt32Bits(settings.Reflectance.X));
        yield return new(0xB74, BitConverter.SingleToUInt32Bits(settings.Reflectance.Y));
        yield return new(0xB78, BitConverter.SingleToUInt32Bits(settings.Reflectance.Z));
        yield return new(0xB80, BitConverter.SingleToUInt32Bits(settings.Roughness));
        yield return new(0xB84, BitConverter.SingleToUInt32Bits(settings.DiffuseTransparency));
    }
}
