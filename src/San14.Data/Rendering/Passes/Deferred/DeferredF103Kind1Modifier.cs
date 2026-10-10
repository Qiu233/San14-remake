using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

public sealed record DeferredF103Kind1ModifierPacket(byte[] Bytes, bool Copied, bool BuiltinModifierPresent);

/// <summary>D59210 and D76B69..D77005 on a complete selected kind1 payload.
/// This transforms owned bytes; it does not establish a current light owner.</summary>
public static class DeferredF103Kind1Modifier
{
    public static bool HasBuiltinModifier(ReadOnlySpan<byte> light)
        => HdrLightEncoding.ReadModifier(light).Present;

    public static DeferredF103Kind1ModifierPacket Apply(ReadOnlySpan<byte> light, bool arg18)
        => ApplyWithModifier(light, light, arg18);

    /// <summary>D76780 receives the outer record's L and the inner record's
    /// D59210 result separately. A null inner H falls back to the outer H.</summary>
    public static DeferredF103Kind1ModifierPacket ApplyWithModifier(ReadOnlySpan<byte> light,
        ReadOnlySpan<byte> innerLight, bool arg18)
    {
        var innerModifier = HdrLightEncoding.ReadModifier(innerLight);
        var modifier = innerModifier.Present ? innerModifier : HdrLightEncoding.ReadModifier(light);
        bool present = modifier.Present;
        bool copied = modifier.RequiresAdjustment(arg18);
        byte[] bytes = light.ToArray();
        if (!copied) return new(bytes, false, present);
        if (present)
        {
            for (int row = 0; row < 4; row++) ScaleRgb(bytes, 0x10 + row * 32, modifier.DirectionalFactor(row));
            if (!arg18) ScaleRgb(bytes, 0xE0, modifier.AmbientFactor);
            float hemisphere = modifier.HemisphereFactor;
            foreach (int row in new[] { 0x100, 0x110, 0x130, 0x140 }) ScaleRgb(bytes, row, hemisphere);
        }
        // D76DBA/D76FD1 load three original zero literals. Alpha is retained.
        if (arg18) bytes.AsSpan(0xE0, 12).Clear();
        return new(bytes, true, present);
    }

    private static void ScaleRgb(byte[] bytes, int offset, float multiplier)
    {
        if (multiplier == 1) return;
        Vector4 color = new(Read(bytes, offset), Read(bytes, offset + 4), Read(bytes, offset + 8), Read(bytes, offset + 12));
        var scaled = HdrLightModifier.ScaleRgb(color, multiplier);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset), scaled.X);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset + 4), scaled.Y);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset + 8), scaled.Z);
    }
    private static float Read(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadSingleLittleEndian(bytes[offset..]);
}
