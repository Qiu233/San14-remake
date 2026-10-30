using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>The matrix distribution expression attached to one definition.
/// Its scalar context is the definition's count/index, not the source emitter.</summary>
public static class G1EffectDistribution
{
    public static G1EffectParticleBirthInputs Apply(G1EffectParticleDefinition definition,
        G1EffectParticleBirthInputs inputs, int index, int count, G1EffectRandom random)
    {
        if (definition.Distribution.IsEmpty) return inputs;
        byte[] context = new byte[Math.Max(0xA0, definition.Record.Bytes.Length)];
        definition.Record.Bytes.CopyTo(context);
        BinaryPrimitives.WriteSingleLittleEndian(context.AsSpan(0x98), count);
        BinaryPrimitives.WriteSingleLittleEndian(context.AsSpan(0x9C), index);
        int cursor = 0;
        var matrix = Matrix(definition.Distribution, ref cursor, context, random);
        Vector4 position = inputs.Position;
        position.X += matrix.M42 * inputs.Orientation.M21 + matrix.M41 * inputs.Orientation.M11 + matrix.M43 * inputs.Orientation.M31;
        position.Y += matrix.M41 * inputs.Orientation.M12 + matrix.M42 * inputs.Orientation.M22 + matrix.M43 * inputs.Orientation.M32;
        position.Z += matrix.M41 * inputs.Orientation.M13 + matrix.M42 * inputs.Orientation.M23 + matrix.M43 * inputs.Orientation.M33;
        matrix = Multiply(matrix, inputs.Orientation);
        return inputs with { Orientation = matrix, Position = position };
    }

    private static Matrix4x4 Matrix(ReadOnlyMemory<byte> bytes, ref int cursor,
        ReadOnlyMemory<byte> context, G1EffectRandom random)
    {
        int opcode = G1EffectBirthExpression.ReadInt(bytes, ref cursor);
        if (opcode == 0) return Matrix(bytes, ref cursor, context, random);
        if (opcode == 1)
        {
            int at = Relative(bytes, ref cursor, 64);
            return new(F(bytes, at), F(bytes, at + 4), F(bytes, at + 8), F(bytes, at + 12),
                F(bytes, at + 16), F(bytes, at + 20), F(bytes, at + 24), F(bytes, at + 28),
                F(bytes, at + 32), F(bytes, at + 36), F(bytes, at + 40), F(bytes, at + 44),
                F(bytes, at + 48), F(bytes, at + 52), F(bytes, at + 56), F(bytes, at + 60));
        }
        if (opcode == 7)
        {
            var translated = Matrix(bytes, ref cursor, context, random);
            Vector4 offset = Vector(bytes, ref cursor, context, random);
            // The native operation adds row.W * offset.xyz to every row.
            translated.M11 = translated.M14 * offset.X + translated.M11;
            translated.M12 = translated.M14 * offset.Y + translated.M12;
            translated.M13 = translated.M14 * offset.Z + translated.M13;
            translated.M21 = translated.M24 * offset.X + translated.M21;
            translated.M22 = translated.M24 * offset.Y + translated.M22;
            translated.M23 = translated.M24 * offset.Z + translated.M23;
            translated.M31 = translated.M34 * offset.X + translated.M31;
            translated.M32 = translated.M34 * offset.Y + translated.M32;
            translated.M33 = translated.M34 * offset.Z + translated.M33;
            translated.M41 = translated.M44 * offset.X + translated.M41;
            translated.M42 = translated.M44 * offset.Y + translated.M42;
            translated.M43 = translated.M44 * offset.Z + translated.M43;
            return translated;
        }
        if (opcode != 6) throw new NotSupportedException($"Effect distribution matrix opcode {opcode} is not implemented.");
        var result = Matrix(bytes, ref cursor, context, random);
        Vector4 axis = Vector(bytes, ref cursor, context, random);
        int trig = G1EffectBirthExpression.ReadInt(bytes, ref cursor);
        if (trig != 1) throw new NotSupportedException($"Effect distribution trigonometry opcode {trig} is not implemented.");
        float angle = G1EffectBirthExpression.Evaluate(bytes, ref cursor, context, random);
        const float tau = 6.2831855f;
        if (MathF.Abs(angle) > tau) angle -= (int)(angle / tau) * tau;
        var (sine, cosine) = G1EffectShapeMath.SinCos(angle);
        return Multiply(result, Rotation(axis, sine, cosine));
    }

    private static Vector4 Vector(ReadOnlyMemory<byte> bytes, ref int cursor,
        ReadOnlyMemory<byte> context, G1EffectRandom random)
    {
        int opcode = G1EffectBirthExpression.ReadInt(bytes, ref cursor);
        if (opcode == 0) return Vector(bytes, ref cursor, context, random);
        if (opcode == 8)
        {
            Vector4 vector = Vector(bytes, ref cursor, context, random);
            float scale = G1EffectBirthExpression.Evaluate(bytes, ref cursor, context, random);
            return new(scale * vector.X, scale * vector.Y, scale * vector.Z, scale * vector.W);
        }
        if (opcode != 1) throw new NotSupportedException($"Effect distribution vector opcode {opcode} is not implemented.");
        int at = Relative(bytes, ref cursor, 16);
        return new(F(bytes, at), F(bytes, at + 4), F(bytes, at + 8), F(bytes, at + 12));
    }

    internal static Matrix4x4 Rotation(Vector4 axis, float sine, float cosine)
    {
        float x = axis.X, y = axis.Y, z = axis.Z;
        float square = x * x + y * y + z * z;
        float inverse = square < 0.9999881f || square > 1.0000119f ? 1f / square : 1f;
        float normal = MathF.Sqrt(inverse), c = 1f - cosine;
        float xy = y * x * inverse, xz = z * x * inverse, yz = z * y * inverse;
        float sx = x * sine * normal, sy = y * sine * normal, sz = z * sine * normal;
        return new(x*x*inverse*c+cosine, xy*c+sz, xz*c-sy, 0,
            xy*c-sz, y*y*inverse*c+cosine, yz*c+sx, 0,
            xz*c+sy, yz*c-sx, z*z*inverse*c+cosine, 0, 0, 0, 0, 1);
    }

    private static Vector4 Transform(Vector4 p, Matrix4x4 m) => new(
        (p.Z*m.M31+p.X*m.M11)+(p.W*m.M41+p.Y*m.M21),
        (p.Z*m.M32+p.X*m.M12)+(p.W*m.M42+p.Y*m.M22),
        (p.Z*m.M33+p.X*m.M13)+(p.W*m.M43+p.Y*m.M23),
        (p.Z*m.M34+p.X*m.M14)+(p.W*m.M44+p.Y*m.M24));

    internal static Matrix4x4 Multiply(Matrix4x4 left, Matrix4x4 right)
    {
        // The source returns four SIMD rows in xmm0..3; its decompiler only
        // declares the first row. Preserve the actual grouped additions.
        var a = Transform(new(left.M11,left.M12,left.M13,left.M14),right);
        var b = Transform(new(left.M21,left.M22,left.M23,left.M24),right);
        var c = Transform(new(left.M31,left.M32,left.M33,left.M34),right);
        var d = Transform(new(left.M41,left.M42,left.M43,left.M44),right);
        return new(a.X,a.Y,a.Z,a.W,b.X,b.Y,b.Z,b.W,c.X,c.Y,c.Z,c.W,d.X,d.Y,d.Z,d.W);
    }

    private static int Relative(ReadOnlyMemory<byte> bytes, ref int cursor, int size)
    {
        int at = cursor;
        at = checked(at + G1EffectBirthExpression.ReadInt(bytes, ref cursor));
        if (at < 0 || at > bytes.Length - size) throw new InvalidDataException("Effect distribution literal escapes its record.");
        return at;
    }
    private static float F(ReadOnlyMemory<byte> bytes, int at) => BinaryPrimitives.ReadSingleLittleEndian(bytes.Span[at..]);
}
