using System.Numerics;

namespace San14.Data;

public static partial class G1EffectDrawWriter
{
    // The secondary image is the next atlas tile. The payload's animation
    // cursor supplies modff's fractional return, not an angle or UV rotation.
    private static (Vector4 Bounds, float Fraction) ReadSecondaryTile(ReadOnlySpan<byte> payload, int cursorOffset)
    {
        float width = F(payload, 0x28) - F(payload, 0x20);
        float u0 = F(payload, 0x28), v0 = F(payload, 0x24);
        float u1 = u0 + width, v1 = F(payload, 0x2C);
        if (width * .5f + 1f < u1)
        {
            float height = v1 - v0;
            v0 = v1; u0 = 0; u1 = width; v1 += height;
            if (height * .5f + 1f < v1) { v0 = 0; v1 = height; }
        }
        float cursor = F(payload, cursorOffset);
        float fraction = float.IsNaN(cursor) ? cursor + cursor :
            MathF.Abs(cursor) >= 8388608f ? MathF.CopySign(0f, cursor) : cursor - MathF.Truncate(cursor);
        if (fraction == 0) fraction = MathF.CopySign(0f, cursor);
        return (new(u0, v0, u1, v1), fraction);
    }

    private static byte[] ExtendSpriteStream(ReadOnlySpan<byte> payload, byte[] primary)
    {
        if ((H(payload, 2) & 0x200) == 0) return primary;
        int cursor = H(payload, 0) == 2 ? 0x74 : 0x44;
        Range(payload, cursor, 4);
        var (bounds, fraction) = ReadSecondaryTile(payload, cursor);
        Vector2[] uv = [new(bounds.X, bounds.Y), new(bounds.X, bounds.W),
            new(bounds.Z, bounds.Y), new(bounds.Z, bounds.W)];
        int[] order = [0, 1, 2, 3];
        // For the few normal declarations that swap their two diagonals,
        // match the already-written primary UV rather than sorting again.
        if (F(primary, 24) != F(payload, 0x20) || F(primary, 28) != F(payload, 0x24)) order = [1, 0, 3, 2];
        var bytes = new byte[256];
        for (int i = 0; i < 4; i++)
        {
            primary.AsSpan(i * 32, 32).CopyTo(bytes.AsSpan(i * 64));
            Put(bytes, i * 64 + 32, uv[order[i]].X);
            Put(bytes, i * 64 + 36, uv[order[i]].Y);
            Put(bytes, i * 64 + 40, bounds.Z - bounds.X);
            Put(bytes, i * 64 + 44, bounds.W - bounds.Y);
            Put(bytes, i * 64 + 48, fraction);
        }
        return bytes;
    }

    private static byte[] ExtendMeshStream(ReadOnlySpan<byte> payload, byte[] primary)
    {
        if ((H(payload, 2) & 0x200) == 0) return primary;
        Range(payload, 0x84, 4);
        var (bounds, fraction) = ReadSecondaryTile(payload, 0x84);
        var bytes = new byte[128]; primary.CopyTo(bytes, 0);
        Put(bytes, 0x40, fraction); Put(bytes, 0x4C, 1f); Put(bytes, 0x6C, 1f);
        Put(bytes, 0x50, (ushort)((int)(bounds.X * 4096f) & 0xFFF));
        Put(bytes, 0x52, (ushort)((int)(bounds.Y * 4096f) & 0xFFF));
        Put(bytes, 0x54, unchecked((ushort)(int)((bounds.Z - bounds.X) * 4096f)));
        Put(bytes, 0x56, unchecked((ushort)(int)((bounds.W - bounds.Y) * 4096f)));
        return bytes;
    }
}
