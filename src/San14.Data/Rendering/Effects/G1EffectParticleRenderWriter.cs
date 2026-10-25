using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

public static partial class G1EffectDrawWriter
{
    private static byte[] WriteParticle(ReadOnlySpan<byte> payload, G1EffectDrawCamera camera,
        out int stride, out int count, out int renderType, out uint header,
        out uint flags, out byte alpha)
    {
        Range(payload, 0, 0x40);
        ushort type = H(payload, 0), sourceFlags = H(payload, 2), renderEnum = H(payload, 0xA);
        if ((sourceFlags & 0x14) != 0)
            throw new NotSupportedException("Particle fog-split writer requires the actual fog providers.");
        if ((sourceFlags & 0x200) != 0)
            throw new NotSupportedException("Particle extended stream requires its actual secondary UV provider.");
        flags = 0x800;
        foreach (var (source, target) in new[] { (7, 1), (8, 2), (9, 3), (10, 4), (11, 5), (12, 6) })
            flags |= ((uint)sourceFlags >> source & 1) << target;
        flags |= ((uint)sourceFlags >> 9 & 1) << 16;
        alpha = (sourceFlags & 8) != 0 ? (byte)~payload[0x13] : (byte)0;
        if (type == 6)
        {
            stride = 64; count = 1;
            renderType = (renderEnum & 255) switch
            {
                134 => 109, 150 => 125, 151 => 126, 152 => 127,
                _ => throw new NotSupportedException($"Mesh rendering enum {renderEnum:X4} is not implemented.")
            };
            header = (uint)(H(payload, 8) & 7) | (uint)((renderEnum >> 4) & 0x30) |
                (uint)((renderEnum >> 12) & 8) | (uint)(payload[6] << 6);
            return WriteMesh(payload, camera);
        }
        if (type is not (0 or 1 or 2))
            throw new NotSupportedException($"Particle payload dispatch {type} is not implemented.");
        renderType = renderEnum switch
        {
            7 => 7, 19 => 17, 26 => 22, 34 => 18,
            _ => throw new NotSupportedException($"Sprite rendering enum {renderEnum} is not implemented.")
        };
        header = 4u | ((uint)H(payload, 8) << 3 & 0x38) | 4u << 8;
        stride = 32; count = 4;
        return WriteSprite(payload, camera);
    }

    private static byte[] WriteMesh(ReadOnlySpan<byte> payload, G1EffectDrawCamera camera)
    {
        Range(payload, 0, 0x80);
        Matrix4x4 transform = Orient(Matrix(payload, 0x30), H(payload, 2), camera);
        var bytes = new byte[64];
        float[] scale = [F(payload, 0x14), F(payload, 0x18), F(payload, 0x1C)];
        for (int component = 0; component < 3; component++)
        {
            for (int axis = 0; axis < 3; axis++)
                Put(bytes, component * 16 + axis * 4, scale[axis] * Component(Row(transform, axis), component));
            // Position remains the original payload translation when the
            // orientation-only camera transform supplies zero translation.
            Put(bytes, component * 16 + 12, F(payload, 0x60 + component * 4));
        }
        int renderEnum = H(payload, 0xA), flags = H(payload, 2), index = payload[0xE];
        int shift = (renderEnum & 255) - 0x92;
        if ((uint)shift < 0x16 && ((0x33900Fu >> shift) & 1) != 0) index >>= 1;
        if ((flags & 0x20) != 0) index += 128;
        int alpha = (flags & 8) != 0 ? 255 : payload[0x13];
        Put(bytes, 0x30, (ushort)(payload[0x12] | index << 8));
        Put(bytes, 0x32, (ushort)(payload[0x11] | payload[0xF] << 8));
        Put(bytes, 0x34, (ushort)(payload[0x10] | H(payload, 0x78) << 8));
        Put(bytes, 0x36, (ushort)(alpha | (renderEnum >> 4 & 0x100)));
        Put(bytes, 0x38, (ushort)((int)(F(payload, 0x20) * 4096f) & 0xFFF));
        Put(bytes, 0x3A, (ushort)((int)(F(payload, 0x24) * 4096f) & 0xFFF));
        Put(bytes, 0x3C, unchecked((ushort)(int)((F(payload, 0x28) - F(payload, 0x20)) * 4096f)));
        Put(bytes, 0x3E, unchecked((ushort)(int)((F(payload, 0x2C) - F(payload, 0x24)) * 4096f)));
        return bytes;
    }

    private static byte[] WriteSprite(ReadOnlySpan<byte> payload, G1EffectDrawCamera camera)
    {
        int type = H(payload, 0), flags = H(payload, 2), renderEnum = H(payload, 0xA);
        float halfWidth = F(payload, 0x14) * 0.5f, halfHeight = F(payload, 0x18) * 0.5f;
        Vector4 x, y, center, normal;
        if (type is 0 or 1)
        {
            x = Row(camera.CameraToWorld, 0); y = Row(camera.CameraToWorld, 1);
            normal = Row(camera.CameraToWorld, 2);
            if (type == 1)
            {
                float angle = F(payload, 0x1C);
                if (MathF.Abs(angle) > 6.2831855f) angle -= (int)(angle / 6.2831855f) * 6.2831855f;
                var (sine, cosine) = G1EffectShapeMath.SinCos(angle);
                Vector4 originalX = x;
                x = cosine * x + sine * y;
                y = cosine * y - sine * originalX;
            }
            center = V(payload, 0x30);
        }
        else
        {
            Range(payload, 0x30, 64);
            Matrix4x4 transform = Orient(Matrix(payload, 0x30), flags, camera);
            x = Row(transform, 0); y = Row(transform, 1); normal = Row(transform, 2);
            center = V(payload, 0x60);
        }
        x *= halfWidth; y *= halfHeight;
        Vector4[] positions = [center - x + y, center - x - y, x + center + y, x + center - y];
        Vector2[] uv = [new(F(payload, 0x20), F(payload, 0x24)), new(F(payload, 0x20), F(payload, 0x2C)),
            new(F(payload, 0x28), F(payload, 0x24)), new(F(payload, 0x28), F(payload, 0x2C))];
        int index = (payload[0xE] + ((flags & 0x20) != 0 ? 128 : 0)) & 255;
        uint packed = (uint)index << 24;
        if (renderEnum == 26)
        {
            Vector3 n = new(normal.X, normal.Y, normal.Z);
            Vector3 delta = camera.Eye - new Vector3(center.X, center.Y, center.Z);
            float length = MathF.Sqrt(n.X * n.X + n.Y * n.Y + n.Z * n.Z);
            float sign = (delta.Y * n.Y + delta.X * n.X) + delta.Z * n.Z < 0 ? -127.5f : 127.5f;
            float inverse = 1f / length;
            packed |= Byte(n.Z * inverse * sign + 127.5f) |
                Byte(n.Y * inverse * sign + 127.5f) << 8 | Byte(n.X * inverse * sign + 127.5f) << 16;
            for (int i = 0; i < 4; i++) positions[i].W = F(payload, 0x1C);
        }
        else if (renderEnum is 19 or 34)
        {
            // This callback writes the half width into W for its shader; it is
            // not a fourth spatial coordinate or an arbitrary opacity value.
            for (int i = 0; i < 4; i++) positions[i].W = halfWidth;
        }
        else if (renderEnum != 7)
            throw new NotSupportedException($"Sprite material writer {renderEnum} is not implemented.");
        uint color = U(payload, 0x10);
        if ((flags & 8) != 0) color |= 0xFF000000;
        int[] order = [0, 1, 2, 3];
        if (renderEnum < 42 && ((0x3F300360000UL >> renderEnum) & 1) != 0)
        {
            Vector3 eyeAxis = new(camera.CameraToWorld.M11, camera.CameraToWorld.M12, camera.CameraToWorld.M13);
            Vector3 a = new(positions[2].X - positions[1].X, positions[2].Y - positions[1].Y, positions[2].Z - positions[1].Z);
            Vector3 b = new(positions[3].X - positions[0].X, positions[3].Y - positions[0].Y, positions[3].Z - positions[0].Z);
            if (MathF.Abs(Vector3.Dot(eyeAxis, b)) < MathF.Abs(Vector3.Dot(eyeAxis, a))) order = [1, 0, 3, 2];
        }
        var bytes = new byte[128];
        for (int indexOfVertex = 0; indexOfVertex < 4; indexOfVertex++)
        {
            int source = order[indexOfVertex], at = indexOfVertex * 32;
            Put(bytes, at, positions[source]);
            Put(bytes, at + 16, color); Put(bytes, at + 20, packed);
            Put(bytes, at + 24, uv[source].X); Put(bytes, at + 28, uv[source].Y);
        }
        return bytes;
    }

    private static Matrix4x4 Orient(Matrix4x4 original, int flags, G1EffectDrawCamera camera)
    {
        int mode = flags & 0x41;
        if (mode == 0) return original;
        if (mode == 0x40)
        {
            Vector4 R(Vector4 r) => r.Y * Row(camera.CameraToWorld, 1) +
                r.X * Row(camera.CameraToWorld, 0) + r.Z * Row(camera.CameraToWorld, 2);
            Vector4 x = R(Row(original, 0)), y = R(Row(original, 1)), z = R(Row(original, 2));
            x.W = y.W = z.W = 0;
            return Matrix(x, y, z, new(0, 0, 0, 1));
        }
        if (mode != 1) throw new NotSupportedException("Particle orientation combines incompatible camera modes.");
        Vector3 originalX = new(original.M11, original.M12, original.M13);
        Vector3 yAxis = new(original.M21, original.M22, original.M23);
        Vector3 originalZ = new(original.M31, original.M32, original.M33);
        Vector3 delta = camera.Eye - new Vector3(original.M41, original.M42, original.M43);
        Vector3 xAxis = Vector3.Cross(yAxis, delta);
        float square = xAxis.X * xAxis.X + xAxis.Y * xAxis.Y + xAxis.Z * xAxis.Z;
        Vector4 xRow;
        if (square >= 1.1920929e-5f)
        {
            xAxis *= originalX.Length() / MathF.Sqrt(square);
            xRow = new(xAxis, 0);
        }
        else { xAxis = originalX; xRow = Row(original, 0); }
        Vector3 zAxis = Vector3.Cross(xAxis, yAxis);
        zAxis *= originalZ.Length() / zAxis.Length();
        return Matrix(xRow, Row(original, 1), new(zAxis, 0), Row(original, 3));
    }

    private static float Component(Vector4 v, int component) => component switch { 0 => v.X, 1 => v.Y, _ => v.Z };
    private static uint Byte(float value) => unchecked((uint)(int)Math.Clamp(value, 0, 255));
    private static void Put(Span<byte> b, int at, float value) => BinaryPrimitives.WriteSingleLittleEndian(b[at..], value);
    private static void Put(Span<byte> b, int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(b[at..], value);
    private static void Put(Span<byte> b, int at, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(b[at..], value);
    private static void Put(Span<byte> b, int at, Vector4 value)
    { Put(b, at, value.X); Put(b, at + 4, value.Y); Put(b, at + 8, value.Z); Put(b, at + 12, value.W); }
}
