using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Authored per-light receiver bias. The operation applies an override
/// only when Scale is positive, then resets its own state after the record.</summary>
public readonly record struct ShadowReceiverBias(float Scale, float Offset);

/// <summary>Imports the two authored fields once at the light boundary and
/// transports their bits independently of the legacy numeric light prefix.</summary>
public static class ShadowReceiverBiasEncoding
{
    public const uint PacketSignature = 0x31435253; // SRC1: shadow receiver current inputs.
    public const int ByteLength = 8;

    public static ShadowReceiverBias ReadLight(ReadOnlySpan<byte> light)
    {
        if (light.Length < 0xF2C)
            throw new ArgumentException("Receiver bias requires the complete authored light.", nameof(light));
        return new(BinaryPrimitives.ReadSingleLittleEndian(light[0xF24..]),
            BinaryPrimitives.ReadSingleLittleEndian(light[0xF28..]));
    }

    public static void Write(ShadowReceiverBias bias, Span<byte> destination)
    {
        BinaryPrimitives.WriteSingleLittleEndian(destination, bias.Scale);
        BinaryPrimitives.WriteSingleLittleEndian(destination[4..], bias.Offset);
    }
}
