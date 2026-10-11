namespace San14.Data;

/// <summary>Initial and current water settings transport. Camera layout
/// offsets are confined to this boundary; no clock observations are encoded.</summary>
public static class OceanSurfaceSettingsEncoding
{
    public const string NativeFilename = "ocean-surface.bin";

    public static void WriteNativeInputs(string directory, OceanSurfaceSettings settings)
    {
        Directory.CreateDirectory(directory);
        using var writer = new BinaryWriter(File.Create(Path.Combine(directory, NativeFilename)));
        writer.Write(0x3153534Fu); // OSS1
        writer.Write(settings.WaterHeight); writer.Write(settings.CutoffDistance);
        writer.Write(settings.TextureWaveDirection.X); writer.Write(settings.TextureWaveDirection.Y);
        writer.Write(settings.TextureWaveDisplacement); writer.Write(settings.NormalAnimationSpeed);
        writer.Write(settings.BubbleMaskMode); writer.Write(settings.SurfaceBubblePeriod);
        writer.Write(settings.InnerBubblePeriod);
    }

    public static IEnumerable<KeyValuePair<int, uint>> MainWords(OceanSurfaceSettings settings)
    {
        yield return new(0xB1C, BitConverter.SingleToUInt32Bits(settings.WaterHeight));
        yield return new(0xB3C, BitConverter.SingleToUInt32Bits(settings.CutoffDistance));
        yield return new(0xBA8, BitConverter.SingleToUInt32Bits(settings.TextureWaveDirection.X));
        yield return new(0xBAC, BitConverter.SingleToUInt32Bits(settings.TextureWaveDirection.Y));
        yield return new(0xB9C, BitConverter.SingleToUInt32Bits(settings.TextureWaveDisplacement));
        yield return new(0xB54, BitConverter.SingleToUInt32Bits(settings.NormalAnimationSpeed));
        yield return new(0xD18, settings.BubbleMaskMode);
        yield return new(0xC24, BitConverter.SingleToUInt32Bits(settings.SurfaceBubblePeriod));
        yield return new(0xC5C, BitConverter.SingleToUInt32Bits(settings.InnerBubblePeriod));
    }
}
