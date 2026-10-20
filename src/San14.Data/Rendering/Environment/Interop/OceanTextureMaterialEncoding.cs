using System.Buffers.Binary;

namespace San14.Data;

/// <summary>The remake's OUM2 material packet, shared by initial preparation
/// and current frame submission. It contains named values, with no original
/// Main arena or sparse-field projection.</summary>
public static class OceanTextureMaterialEncoding
{
    public const int ByteLength = 60;
    public const string NativeFilename = "ocean-texture-material.bin";

    public static void Write(OceanTextureMaterialSettings settings, Span<byte> destination)
    {
        if (destination.Length < ByteLength)
            throw new ArgumentException("Ocean texture material requires a complete OUM2 packet.", nameof(destination));
        BinaryPrimitives.WriteUInt32LittleEndian(destination, 0x324D554Fu); // OUM2: separate kurtosis and speed.
        var uv = settings.Uv;
        var wave = settings.Wave;
        ReadOnlySpan<float> values = [uv.NormalScale, uv.NoiseScale.X, uv.NoiseScale.Y,
            uv.HeightScale.X, uv.HeightScale.Y, uv.HeightOffset.X, uv.HeightOffset.Y,
            wave.Amplitude, wave.Length, wave.Width, wave.Kurtosis,
            wave.Speed, wave.Scatter, wave.ShoalingScale];
        for (int index = 0; index < values.Length; index++)
            BinaryPrimitives.WriteSingleLittleEndian(destination.Slice(4 + index * 4, 4), values[index]);
    }

    public static void WriteNativeInputs(string directory, OceanTextureMaterialSettings settings)
    {
        byte[] bytes = new byte[ByteLength];
        Write(settings, bytes);
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, NativeFilename), bytes);
    }
}
