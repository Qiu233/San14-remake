using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;

namespace San14.Data;

/// <summary>Initial named Main Sky values from the existing camera and lighting
/// preparation. Shader destinations are imported separately; WORK cube values
/// and runtime animation state do not belong to this packet.</summary>
public static class SkyMainMaterialEncoding
{
    public const int ByteLength = 472;
    public const string NativeFilename = "initial-state.bin";
    internal static ReadOnlySpan<int> FieldByteLengths =>
        [64, 4, 64, 16, 64, 16, 16, 16, 16, 16, 16, 16, 64, 16, 16, 16, 16, 16];

    public static void Write(Sky2DrawInputs material, Span<byte> destination)
    {
        if(destination.Length < ByteLength)
            throw new ArgumentException("Main Sky requires a complete SMI1 packet.", nameof(destination));
        var vertex = material.Vertex;
        var pixel = material.Pixel;
        // Main and WORK publish one shared fog value to both shader stages.
        // SMI1 field 3 carries that value; distinct stage inputs cannot be
        // represented by this packet, including differences in signed zero.
        ReadOnlySpan<float> vertexFog = [vertex.Fog.X, vertex.Fog.Y, vertex.Fog.Z, vertex.Fog.W];
        ReadOnlySpan<float> pixelFog = [pixel.Fog.X, pixel.Fog.Y, pixel.Fog.Z, pixel.Fog.W];
        if (!MemoryMarshal.AsBytes(vertexFog).SequenceEqual(MemoryMarshal.AsBytes(pixelFog)))
            throw new ArgumentException("Main Sky requires the same fog bits in both shader stages.", nameof(material));
        if(pixel.Scattering.Count != 4 || pixel.MoonFace is null || pixel.MoonScreenPosition is null)
            throw new NotSupportedException("Main Sky needs its complete scattering and Moon producers.");
        float[] values =
        [
            .. Words(vertex.CameraToLocal), vertex.ProjectionFarZ, .. Words(vertex.LocalToGlobal),
            .. Words(pixel.Fog), .. pixel.Scattering.SelectMany(Words),
            .. Words(pixel.ViewPosition), .. Words(pixel.SkyInfo0), .. Words(pixel.SkyInfo1),
            .. Words(pixel.SunRgb), .. Words(pixel.SunDirection), .. Words(pixel.MoonRgb), .. Words(pixel.MoonDirection),
            .. Words(pixel.MoonFace.Value), .. Words(pixel.SunScreenPosition), .. Words(pixel.MoonScreenPosition.Value),
            .. Words(pixel.AltitudeInfo), .. Words(pixel.StableDimensions), .. Words(pixel.ClampValue)
        ];
        BinaryPrimitives.WriteUInt32LittleEndian(destination, 0x31494D53); // SMI1: Main Sky initial values.
        for(int index=0;index<values.Length;index++)
            BinaryPrimitives.WriteSingleLittleEndian(destination.Slice(4 + index*4, 4), values[index]);
    }

    public static void WriteNativeInputs(string directory, Sky2DrawInputs material)
    {
        byte[] bytes = new byte[ByteLength];
        Write(material, bytes);
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, NativeFilename), bytes);
    }

    private static float[] Words(Vector4 value) => [value.X, value.Y, value.Z, value.W];
    private static float[] Words(Matrix4x4 value) =>
        [value.M11,value.M12,value.M13,value.M14,value.M21,value.M22,value.M23,value.M24,
         value.M31,value.M32,value.M33,value.M34,value.M41,value.M42,value.M43,value.M44];
}
