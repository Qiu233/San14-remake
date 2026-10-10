using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>Camera transport adapter. Unrelated fields and padding remain caller-owned.</summary>
public static class SkyLightingEncoding
{
    public static SkyLightingState Decode(ReadOnlySpan<byte> prefix)
    {
        if (prefix.Length != PreparedCameraLighting.PrefixByteLength)
            throw new InvalidDataException("Celestial lighting import requires a complete camera prefix.");
        uint order = BinaryPrimitives.ReadUInt32LittleEndian(prefix[0x3B0..]);
        return new(
            new(Read3(prefix, 0x2AC), new(Read3(prefix, 0x2B8), 0), Read4(prefix, 0x2C4), Read4(prefix, 0x2D4), Read3(prefix, 0x2E4)),
            new(Read3(prefix, 0x2F0), new(Read3(prefix, 0x2FC), 0), Read4(prefix, 0x308), Read4(prefix, 0x318), Read3(prefix, 0x328)),
            new((SkyLightingTransitionPhase)(order & 0x7FFFFFFF), (order & 0x80000000) != 0,
                Read(prefix, 0x3B4), Read(prefix, 0x3B8), Read(prefix, 0x3BC)));
    }

    public static uint EncodeOrder(SkyLightingTransitionState transition) =>
        (uint)transition.Phase | (transition.SunPrimary ? 0x80000000u : 0);

    public static byte[] Apply(ReadOnlySpan<byte> prefix, SkyLightingState lighting, bool clearAbsentTableFog = false)
    {
        byte[] result = prefix.ToArray();
        Write(result, lighting);
        if (clearAbsentTableFog) BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0x350, 4), 0);
        return result;
    }

    public static byte[] InitializeConstructorLighting(ReadOnlySpan<byte> prefix)
    {
        if (prefix.Length != PreparedCameraLighting.PrefixByteLength)
            throw new InvalidDataException("Camera constructor lighting requires a complete prefix.");
        byte[] result = prefix.ToArray();
        result.AsSpan(0x2AC, 0x88).Clear();
        result.AsSpan(0x340, 0x60).Clear();
        result.AsSpan(0x3B0, 0x10).Clear();
        return result;
    }

    public static void Write(Span<byte> prefix, SkyLightingState lighting)
    {
        if (prefix.Length != PreparedCameraLighting.PrefixByteLength)
            throw new InvalidDataException("Celestial lighting export requires a complete camera prefix.");
        Write3(prefix, 0x2AC, lighting.Sun.SelectedDirection);
        Write3(prefix, 0x2B8, new(lighting.Sun.Direction.X, lighting.Sun.Direction.Y, lighting.Sun.Direction.Z));
        Write4(prefix, 0x2C4, lighting.Sun.BaseColor); Write4(prefix, 0x2D4, lighting.Sun.CurrentColor);
        Write3(prefix, 0x2E4, lighting.Sun.PreviousColor);
        Write3(prefix, 0x2F0, lighting.Moon.SelectedDirection);
        Write3(prefix, 0x2FC, new(lighting.Moon.Direction.X, lighting.Moon.Direction.Y, lighting.Moon.Direction.Z));
        Write4(prefix, 0x308, lighting.Moon.BaseColor); Write4(prefix, 0x318, lighting.Moon.CurrentColor);
        Write3(prefix, 0x328, lighting.Moon.PreviousColor);
        BinaryPrimitives.WriteUInt32LittleEndian(prefix[0x3B0..], EncodeOrder(lighting.Transition));
        Put(prefix, 0x3B4, lighting.Transition.Elapsed); Put(prefix, 0x3B8, lighting.Transition.Continuation);
        Put(prefix, 0x3BC, lighting.Transition.Strength);
    }

    private static float Read(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadSingleLittleEndian(bytes[offset..]);
    private static Vector3 Read3(ReadOnlySpan<byte> bytes, int offset) => new(Read(bytes, offset), Read(bytes, offset+4), Read(bytes, offset+8));
    private static Vector4 Read4(ReadOnlySpan<byte> bytes, int offset) => new(Read3(bytes, offset), Read(bytes, offset+12));
    private static void Put(Span<byte> bytes, int offset, float value) => BinaryPrimitives.WriteSingleLittleEndian(bytes[offset..], value);
    private static void Write3(Span<byte> bytes, int offset, Vector3 value)
    { Put(bytes, offset, value.X); Put(bytes, offset+4, value.Y); Put(bytes, offset+8, value.Z); }
    private static void Write4(Span<byte> bytes, int offset, Vector4 value)
    { Write3(bytes, offset, new(value.X, value.Y, value.Z)); Put(bytes, offset+12, value.W); }
}
