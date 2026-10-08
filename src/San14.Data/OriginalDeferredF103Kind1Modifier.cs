using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace San14.Data;

public sealed record OriginalDeferredF103Kind1ModifierPacket(byte[] Bytes, bool Copied, bool BuiltinModifierPresent);

/// <summary>D59210 and D76B69..D77005 on a complete selected kind1 payload.
/// This transforms owned bytes; it does not establish a current light owner.</summary>
public static class OriginalDeferredF103Kind1Modifier
{
    public static bool HasBuiltinModifier(ReadOnlySpan<byte> light)
    {
        RequireComplete(light);
        return (Word(light, 0x1F4) & 0xFFFFF0FF) != 0xFFFFF0FF ||
            (Word(light, 0x1F8) & 0x1FF) != 0x1FF;
    }

    public static OriginalDeferredF103Kind1ModifierPacket Apply(ReadOnlySpan<byte> light, bool arg18)
        => ApplyWithModifier(light, light, arg18);

    /// <summary>D76780 receives the outer record's L and the inner record's
    /// D59210 result separately. A null inner H falls back to the outer H.</summary>
    public static OriginalDeferredF103Kind1ModifierPacket ApplyWithModifier(ReadOnlySpan<byte> light,
        ReadOnlySpan<byte> innerLight, bool arg18)
    {
        bool present = HasBuiltinModifier(innerLight);
        ReadOnlySpan<byte> selected = present ? innerLight : light;
        present = HasBuiltinModifier(selected);
        bool copied = arg18 || present && (Word(selected, 0x1F4) & 0x202000) != 0x202000;
        byte[] bytes = light.ToArray();
        if (!copied) return new(bytes, false, present);
        if (present)
        {
            for (int row = 0; row < 4; row++) ScaleRgb(bytes, 0x10 + row * 32, Read(selected, 0x350 + row * 4));
            if (!arg18) ScaleRgb(bytes, 0xE0, Read(selected, 0x360));
            float hemisphere = Read(selected, 0x364);
            foreach (int row in new[] { 0x100, 0x110, 0x130, 0x140 }) ScaleRgb(bytes, row, hemisphere);
        }
        // D76DBA/D76FD1 load three original zero literals. Alpha is retained.
        if (arg18) bytes.AsSpan(0xE0, 12).Clear();
        return new(bytes, true, present);
    }

    private static void ScaleRgb(byte[] bytes, int offset, float multiplier)
    {
        if (multiplier == 1) return;
        for (int channel = 0; channel < 3; channel++)
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset + channel * 4, 4),
                Multiply(Read(bytes, offset + channel * 4), multiplier));
    }
    private static float Read(ReadOnlySpan<byte> bytes, int offset)
    {
        float value = BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(offset, 4));
        if (!float.IsFinite(value)) throw new InvalidDataException("F103 selected modifier consumes a nonfinite color or multiplier.");
        return value;
    }
    private static uint Word(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4));
    private static void RequireComplete(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != OriginalCameraDirectionalLightData.LightDataBytes)
            throw new InvalidDataException("F103 selected modifier requires complete10C0 LightData.");
    }
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Multiply(float first, float second) => first * second;
}
