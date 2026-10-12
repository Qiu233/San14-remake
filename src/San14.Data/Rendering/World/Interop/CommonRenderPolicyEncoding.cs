using System.Buffers.Binary;

namespace San14.Data;

/// <summary>The remake's CRP4 policy packet. Initial preparation and current
/// frames share this encoding; original Common record offsets stay outside it.</summary>
public static class CommonRenderPolicyEncoding
{
    public const int ByteLength = 24;
    public const string NativeFilename = "common-render-policy.bin";

    public static void Write(CommonRenderPolicy policy, Span<byte> destination)
    {
        if (destination.Length < ByteLength)
            throw new ArgumentException("Common render policy requires a complete CRP4 packet.", nameof(destination));
        bool skyEnabled = policy.SuppressOceanDrawing.HasValue && policy.EnableSkyDrawing;
        BinaryPrimitives.WriteUInt32LittleEndian(destination, 0x34505243u); // CRP4
        BinaryPrimitives.WriteUInt32LittleEndian(destination[4..], policy.SuppressOceanDrawing.HasValue ? 1u : 0u);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[8..], policy.SuppressOceanDrawing == true ? 1u : 0u);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[12..], skyEnabled ? 1u : 0u);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[16..], (uint)(policy.SuppressOceanDrawing.HasValue
            ? policy.SkyAlphaBlend : SkyAlphaBlendMode.Maximum));
        BinaryPrimitives.WriteUInt32LittleEndian(destination[20..],
            !policy.SuppressOceanDrawing.HasValue || policy.AllowAmbientGeneration ? 1u : 0u);
    }

    public static void WriteNativeInputs(string directory, CommonRenderPolicy policy)
    {
        byte[] bytes = new byte[ByteLength];
        Write(policy, bytes);
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, NativeFilename), bytes);
    }
}
