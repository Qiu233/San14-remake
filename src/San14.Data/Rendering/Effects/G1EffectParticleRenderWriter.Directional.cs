using System.Numerics;

namespace San14.Data;

public static partial class G1EffectDrawWriter
{
    // These callbacks replace material channels after the common quad geometry
    // is built and before the source diagonal-order writer copies its vertices.
    private static void WriteDirectionalSpriteMaterial(int renderEnum, int type,
        ReadOnlySpan<byte> payload, G1EffectDrawCamera camera, Matrix4x4 orientation,
        Vector4 center, float sine, float cosine, Vector4[] positions, Vector2[] uv,
        out uint[] colors, out uint[] frames)
    {
        if (renderEnum == 38 ? type != 1 : type != 2)
            throw new NotSupportedException($"Sprite material {renderEnum} has no implemented callback for payload {type}.");
        if (type == 2 && (H(payload, 2) & 2) != 0)
            throw new NotSupportedException("Directional sprite angle-opacity requires its source alpha modulation.");
        colors = new uint[4]; frames = new uint[4];
        int flags = H(payload, 2);
        uint alpha = payload[0x13]; // These callbacks retain the source alpha byte.
        int index = payload[0xE], alternate = (flags & 0x20) != 0 ? 128 : 0;
        float rgb = U(payload, 0x10) & 0xFFFFFF;
        const float diagonal = 0.70710677f;

        if (renderEnum == 38)
        {
            float sum = (sine + cosine) * diagonal, difference = (cosine - sine) * diagonal;
            Vector4[] directions = [new(-sum, difference, 0.6f, 0),
                new(-difference, -sum, 0.6f, 0), new(difference, sum, 0.6f, 0),
                new(sum, -difference, 0.6f, 0)];
            Vector4 cameraZ = Row(camera.CameraToWorld, 2);
            float tile = MathF.Ceiling(F(payload, 0x14) * 0.5f);
            for (int corner = 0; corner < 4; corner++)
            {
                Vector4 source = directions[corner];
                var basis = camera.CameraToWorld;
                Vector4 normal = new(
                    (source.Y * basis.M21 + source.X * basis.M11) + source.Z * basis.M31,
                    (source.X * basis.M12 + source.Y * basis.M22) + source.Z * basis.M32,
                    (source.X * basis.M13 + source.Y * basis.M23) + source.Z * basis.M33, 0);
                float reflection = ((normal.Y * cameraZ.Y + normal.X * cameraZ.X) + normal.Z * cameraZ.Z) * -2f;
                colors[corner] = UnitDirectionColor(normal) | alpha << 24;
                frames[corner] = UnitDirectionColor(new(
                    reflection * cameraZ.X + normal.X,
                    reflection * cameraZ.Y + normal.Y,
                    reflection * cameraZ.Z + normal.Z, 0)) | (uint)((index + alternate) & 255) << 24;
                positions[corner].W = rgb;
                uv[corner].X = uv[corner].X * 0.5f + tile;
            }
            return;
        }

        Vector4 rawX = Row(orientation, 0), rawY = Row(orientation, 1), rawZ = Row(orientation, 2);
        Vector4 x = NormalizeDirectionalAxis(rawX), y = NormalizeDirectionalAxis(rawY),
            z = NormalizeDirectionalAxis(rawZ);
        Vector3 delta = camera.Eye - new Vector3(center.X, center.Y, center.Z);
        bool back = (delta.Y * rawZ.Y + delta.X * rawZ.X) + delta.Z * rawZ.Z < 0;
        float tileOffset = MathF.Ceiling(F(payload, 0x1C));
        if (renderEnum == 40)
        {
            Vector4 negativeX = x * -diagonal, positiveY = y * diagonal;
            Vector4 first = negativeX + positiveY, second = negativeX - positiveY;
            Vector4[] directions = [first, second, -second, -first];
            float primaryBias = back ? -0.6f : 0.6f, secondaryBias = back ? 0.6f : -0.6f;
            for (int corner = 0; corner < 4; corner++)
            {
                Vector4 direction = directions[corner];
                Vector4 primary = new(z.X * primaryBias + direction.X,
                    z.Y * primaryBias + direction.Y, z.Z * primaryBias + direction.Z,
                    z.W * primaryBias + direction.W);
                Vector4 secondary = new(z.X * secondaryBias + direction.X,
                    z.Y * secondaryBias + direction.Y, z.Z * secondaryBias + direction.Z,
                    z.W * secondaryBias + direction.W);
                colors[corner] = ScaledDirectionColor(primary) | Byte(primary.W * 127.5f + alpha) << 24;
                frames[corner] = ScaledDirectionColor(secondary) |
                    Byte(secondary.W * 127.5f + ((index + alternate) & 255)) << 24;
                positions[corner].W = rgb;
                uv[corner].X = uv[corner].X * 0.5f + tileOffset;
            }
            return;
        }

        // Enum53 carries normalized X/-Y in the colors. The facing normal is
        // numeric 24-bit data in position.W, rather than the original color.
        uint firstColor = UnitDirectionColor(x) | alpha << 24;
        uint secondColor = Byte((1f - y.Z) * 127.5f) | Byte((1f - y.Y) * 127.5f) << 8 |
            Byte((1f - y.X) * 127.5f) << 16 | (uint)((index >> 1) + alternate) << 24;
        float sign = back ? -127.5f : 127.5f;
        uint normalColor = Byte(z.Z * sign + 127.5f) | Byte(z.Y * sign + 127.5f) << 8 |
            Byte(z.X * sign + 127.5f) << 16;
        for (int corner = 0; corner < 4; corner++)
        {
            colors[corner] = firstColor; frames[corner] = secondColor;
            positions[corner].W = normalColor;
            uv[corner].X = uv[corner].X * 0.5f + tileOffset;
        }
    }

    private static Vector4 NormalizeDirectionalAxis(Vector4 value)
    {
        float inverse = 1f / MathF.Sqrt((value.X * value.X + value.Y * value.Y) + value.Z * value.Z);
        return new(value.X * inverse, value.Y * inverse, value.Z * inverse, value.W);
    }

    private static uint UnitDirectionColor(Vector4 direction) =>
        Byte((direction.Z + 1f) * 127.5f) | Byte((direction.Y + 1f) * 127.5f) << 8 |
        Byte((direction.X + 1f) * 127.5f) << 16;

    private static uint ScaledDirectionColor(Vector4 direction) =>
        Byte(direction.Z * 127.5f + 127.5f) | Byte(direction.Y * 127.5f + 127.5f) << 8 |
        Byte(direction.X * 127.5f + 127.5f) << 16;
}
