using System.Numerics;

namespace San14.Data;

/// <summary>Birth-frame and movement compensation math. Inputs are the actual
/// emitter/shape transforms and definition flags, not sampled process payloads.</summary>
public static class G1EffectBirthTransform
{
    public static Matrix4x4 EmissionFrame(uint emitterFlags, Matrix4x4 shapeFrame)
    {
        Vector4 x, y, z;
        if ((emitterFlags & 0x80) != 0)
        {
            x = Normalize(Row0(shapeFrame));
            y = Normalize(Row1(shapeFrame));
            z = Normalize(Row2(shapeFrame));
        }
        else
        {
            x = new(1, 0, 0, 0);
            y = new(0, 1, 0, 0);
            z = new(0, 0, 1, 0);
        }
        return Frame(x, y, z, Row3(shapeFrame));
    }

    public static Matrix4x4 Orientation(uint definitionFlags, Matrix4x4 emissionFrame,
        Vector4 sampledPosition, Vector4 shapeCenter)
    {
        uint mode = definitionFlags & 15;
        if (mode == 0) return emissionFrame;
        Vector4 z = sampledPosition - shapeCenter;
        if (Dot(z, z) < 1.1920929e-5f) return emissionFrame;
        Vector4 x;
        Vector4 y = Row1(emissionFrame);
        if (mode == 1)
        {
            z = Normalize(z);
            if (MathF.Abs(Dot(z, y)) <= 0.9988079f)
            {
                x = Normalize(Cross(y, z));
                y = Cross(z, x);
            }
            else
            {
                Vector4 originalX = Row0(emissionFrame);
                y = Normalize(Dot(z, Row2(emissionFrame)) < 0f
                    ? Cross(originalX, z) : Cross(z, originalX));
                x = Cross(y, z);
            }
        }
        else
        {
            float projection = Dot(y, z);
            z = new(z.X - y.X * projection, z.Y - y.Y * projection,
                z.Z - y.Z * projection, z.W);
            z = Dot(z, z) >= 1.1920929e-5f ? Normalize(z) : Row2(emissionFrame);
            x = Cross(y, z);
        }
        return Frame(x, y, z, Row3(emissionFrame));
    }

    public static Vector4 CompensateMovement(Vector4 sample, Matrix4x4 emitterVelocity,
        float birthOffsetSeconds, bool particleUsesWorldSpace)
    {
        if (particleUsesWorldSpace) return sample;
        Vector4 velocity = Transform(sample, emitterVelocity);
        return new(sample.X - velocity.X * birthOffsetSeconds,
            sample.Y - velocity.Y * birthOffsetSeconds,
            sample.Z - velocity.Z * birthOffsetSeconds,
            sample.W - velocity.W * birthOffsetSeconds);
    }

    public static Vector4 InheritedVelocity(uint definitionFlags, Vector4 position,
        Matrix4x4 emitterVelocity, float inheritanceScale, bool particleUsesWorldSpace)
    {
        if (particleUsesWorldSpace || (definitionFlags & 0x10) == 0) return Vector4.Zero;
        var m = emitterVelocity;
        Vector4 velocity = new(
            position.Y * m.M21 + position.X * m.M11 + position.Z * m.M31 + position.W * m.M41,
            position.X * m.M12 + position.Y * m.M22 + position.Z * m.M32 + position.W * m.M42,
            position.X * m.M13 + position.Y * m.M23 + position.Z * m.M33 + position.W * m.M43,
            position.X * m.M14 + position.Y * m.M24 + position.Z * m.M34 + position.W * m.M44);
        return new(velocity.X * inheritanceScale, velocity.Y * inheritanceScale,
            velocity.Z * inheritanceScale, velocity.W);
    }

    private static Vector4 Transform(Vector4 p, Matrix4x4 m) => new(
        p.Y * m.M21 + p.X * m.M11 + p.Z * m.M31 + p.W * m.M41,
        p.Y * m.M22 + p.X * m.M12 + p.Z * m.M32 + p.W * m.M42,
        p.Y * m.M23 + p.X * m.M13 + p.Z * m.M33 + p.W * m.M43,
        p.Y * m.M24 + p.X * m.M14 + p.Z * m.M34 + p.W * m.M44);

    private static Vector4 Normalize(Vector4 v)
    {
        float inverse = 1f / MathF.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
        return new(v.X * inverse, v.Y * inverse, v.Z * inverse, v.W);
    }

    private static float Dot(Vector4 a, Vector4 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    private static Vector4 Cross(Vector4 a, Vector4 b) => new(
        a.Y * b.Z - b.Y * a.Z, b.X * a.Z - a.X * b.Z, b.Y * a.X - a.Y * b.X, 0);
    private static Vector4 Row0(Matrix4x4 m) => new(m.M11, m.M12, m.M13, m.M14);
    private static Vector4 Row1(Matrix4x4 m) => new(m.M21, m.M22, m.M23, m.M24);
    private static Vector4 Row2(Matrix4x4 m) => new(m.M31, m.M32, m.M33, m.M34);
    private static Vector4 Row3(Matrix4x4 m) => new(m.M41, m.M42, m.M43, m.M44);
    private static Matrix4x4 Frame(Vector4 x, Vector4 y, Vector4 z, Vector4 position) => new(
        x.X, x.Y, x.Z, x.W, y.X, y.Y, y.Z, y.W,
        z.X, z.Y, z.Z, z.W, position.X, position.Y, position.Z, position.W);
}
