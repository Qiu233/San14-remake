using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>Original LightData interchange boundary. Decode once when a current
/// light is published; render passes borrow the resulting typed values.</summary>
public static class HdrLightEncoding
{
    public static HdrLightState Read(ReadOnlySpan<byte> bytes)
    {
        var modifier = ReadModifier(bytes);
        var lights = new DeferredDirectionalLight[4];
        for (int i = 0; i < lights.Length; i++)
            lights[i] = new(Vector(bytes, i * 32), Vector(bytes, i * 32 + 16), Float(bytes, 0x80 + i * 4));
        return new(lights, Vector(bytes, 0xE0), modifier,
            new(Float(bytes, 0x31C), Float(bytes, 0x320), Float(bytes, 0x324), Float(bytes, 0x300)), Float(bytes, 0x318),
            new(Float(bytes, 0x30C), Float(bytes, 0x310), Float(bytes, 0x314), Float(bytes, 0x304)), Float(bytes, 0x308),
            new(Vector(bytes, 0x100), Vector(bytes, 0x110), Vector(bytes, 0x130), Vector(bytes, 0x140)),
            new(bytes[0xF1D], bytes[0xF1E]));
    }

    public static HdrLightModifier ReadModifier(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != CameraDirectionalLightData.LightDataBytes)
            throw new InvalidDataException("HDR light import requires complete LightData.");
        uint flags = BinaryPrimitives.ReadUInt32LittleEndian(bytes[0x1F4..]);
        bool present = (flags & 0xFFFFF0FF) != 0xFFFFF0FF ||
            (BinaryPrimitives.ReadUInt32LittleEndian(bytes[0x1F8..]) & 0x1FF) != 0x1FF;
        return new(present, (flags & 0x202000) != 0x202000,
            Vector(bytes, 0x350), Float(bytes, 0x360), Float(bytes, 0x364));
    }

    private static float Float(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadSingleLittleEndian(bytes[offset..]);
    private static Vector4 Vector(ReadOnlySpan<byte> bytes, int offset) => new(
        Float(bytes, offset), Float(bytes, offset + 4), Float(bytes, offset + 8), Float(bytes, offset + 12));
}
