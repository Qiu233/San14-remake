using System.Buffers.Binary;

namespace San14.Data;

/// <summary>The remake's CRP1 policy packet. Initial preparation and current
/// frames share this encoding; original Common record offsets stay outside it.</summary>
public static class CommonRenderPolicyEncoding
{
    public const int ByteLength = 12;
    public const string NativeFilename = "common-render-policy.bin";

    public static void Write(CommonRenderPolicy policy, Span<byte> destination)
    {
        if (destination.Length < ByteLength)
            throw new ArgumentException("Common render policy requires a complete CRP1 packet.", nameof(destination));
        BinaryPrimitives.WriteUInt32LittleEndian(destination, 0x31505243u); // CRP1
        BinaryPrimitives.WriteUInt32LittleEndian(destination[4..], policy.SuppressOceanDrawing.HasValue ? 1u : 0u);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[8..], policy.SuppressOceanDrawing == true ? 1u : 0u);
    }

    public static void WriteNativeInputs(string directory, CommonRenderPolicy policy)
    {
        byte[] bytes = new byte[ByteLength];
        Write(policy, bytes);
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, NativeFilename), bytes);
    }
}
