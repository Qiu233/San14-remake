using System.Buffers.Binary;

namespace San14.Data;

public static partial class G1EffectDrawWriter
{
    private static IReadOnlyList<byte[]> ExpandSpecialFrameBlend(byte[] payload)
    {
        ushort type = H(payload, 0), flags = H(payload, 2), configuration = H(payload, 8);
        if ((type == 6 ? configuration & 7 : configuration) != 6 || (flags & 0x14) == 0)
            return [payload];
        // AA4880 constructs interpolation enabled (renderer2E0=1), copied
        // by AA59C0 to the working writer179. These wrappers are animation
        // atlas frame blending, despite the old missing-provider label.
        if (type is not (0 or 1 or 2 or 6)) return [payload];
        int cursorOffset = type == 6 ? 0x84 : type == 2 ? 0x74 : 0x44;
        Range(payload, cursorOffset, 4);
        var (bounds, fraction) = ReadSecondaryTile(payload, cursorOffset);
        int weight = unchecked((int)(fraction * 255f));
        var first = payload.ToArray(); var second = payload.ToArray();
        // The native wrapper clears source bits2,4,9 before either callback,
        // so the two rows use primary streams, not extended secondary streams.
        ushort callbackFlags = (ushort)(flags & ~0x214);
        BinaryPrimitives.WriteUInt16LittleEndian(first.AsSpan(2), callbackFlags);
        BinaryPrimitives.WriteUInt16LittleEndian(second.AsSpan(2), callbackFlags);
        if ((flags & 4) == 0)
        {
            first[0x13] = unchecked((byte)(payload[0x13] * (255 - weight) >> 8));
            second[0x13] = unchecked((byte)(payload[0x13] * weight >> 8));
        }
        else
            for (int component = 0x10; component < 0x13; component++)
            {
                first[component] = unchecked((byte)(payload[component] * (255 - weight) >> 8));
                second[component] = unchecked((byte)(payload[component] * weight >> 8));
            }
        Put(second, 0x20, bounds.X); Put(second, 0x24, bounds.Y);
        Put(second, 0x28, bounds.Z); Put(second, 0x2C, bounds.W);
        return [first, second];
    }
}
