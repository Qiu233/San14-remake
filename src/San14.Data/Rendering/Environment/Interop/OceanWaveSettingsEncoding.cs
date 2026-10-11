namespace San14.Data;

/// <summary>Initial and current wave transport. Prepared Main offsets remain
/// an interchange detail; the water domain consumes named settings.</summary>
public static class OceanWaveSettingsEncoding
{
    public const string NativeFilename = "ocean-wave.bin";

    public static void WriteNativeInputs(string directory, OceanWaveSettings settings)
    {
        Directory.CreateDirectory(directory);
        using var writer = new BinaryWriter(File.Create(Path.Combine(directory, NativeFilename)));
        writer.Write(0x3153574Fu); // OWS1
        writer.Write(settings.Wind.X); writer.Write(settings.Wind.Y);
        writer.Write(settings.Amplitude); writer.Write(settings.ChoppyFactor);
        writer.Write(settings.SmallWaveSuppressionLength); writer.Write(settings.Gravity);
    }

    public static IEnumerable<KeyValuePair<int, uint>> MainWords(OceanWaveSettings settings)
    {
        yield return new(0xB20, BitConverter.SingleToUInt32Bits(settings.Wind.X));
        yield return new(0xB24, BitConverter.SingleToUInt32Bits(settings.Wind.Y));
        yield return new(0xB28, BitConverter.SingleToUInt32Bits(settings.Amplitude));
        yield return new(0xB2C, BitConverter.SingleToUInt32Bits(settings.ChoppyFactor));
        yield return new(0xB30, BitConverter.SingleToUInt32Bits(settings.SmallWaveSuppressionLength));
        yield return new(0xB34, BitConverter.SingleToUInt32Bits(settings.Gravity));
    }
}
