using System.Numerics;

namespace San14.Data;

public sealed record OriginalSkylightDirections(Vector4 Sun, Vector4 Moon);

/// <summary>Original Ortho/Perspective sky setter's 1176D60 direction calculation.
/// Inputs are explicit source parameters; this does not select or bind a lighting pass.</summary>
public static class OriginalSkylightDirectionMath
{
    private const float DegreeScale = 0.01745329238474369f;
    private const float Pi = 3.1415927410125732f;

    public static OriginalSkylightDirections Calculate(OriginalSkylightParameters input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (new[] { input.MonthDay, input.DateTime, input.Longitude, input.Latitude,
                input.Altitude, input.NorthDir, input.MoonAge }.Any(value => !float.IsFinite(value)))
            throw new InvalidDataException("Original Skylight direction input is not finite.");
        float longitude = M(input.Longitude, DegreeScale), latitude = M(input.Latitude, DegreeScale);
        float north = M(input.NorthDir, DegreeScale);
        float time = input.DateTime > 0 ? input.DateTime : 0;
        float season = M(S(input.MonthDay, 3.700000047683716f), 0.5235987901687622f);
        float hour = S(A(M(S(A(time, time), 1), Pi), season), longitude);

        Matrix4x4 geographic = RightY(Matrix4x4.Identity, north);
        geographic = RightX(geographic, latitude);
        geographic = RightZ(geographic, longitude);
        OriginalNativeSinCos quarterTurn = OriginalNativeTrigonometry.SinCos014590(1.5707963705062866f);
        Matrix4x4 sky = new(1, 0, 0, 0, 0, quarterTurn.Cos, quarterTurn.Sin, 0,
            0, -quarterTurn.Sin, quarterTurn.Cos, 0, 0, 0, 0, 1);
        sky = LeftY(sky, Pi);
        sky = RightY(sky, hour);
        Matrix4x4 transform = OriginalNativeMatrixMath.Invert(Combine3x3(sky, geographic), out _);

        OriginalNativeSinCos obliquity = OriginalNativeTrigonometry.SinCos014590(0.40909260511398315f);
        Vector4 axis = new(-obliquity.Sin, obliquity.Cos, 0, 0);
        Vector4 initial = new(0, 0, -1, 0);
        // The original sky setter passes extraArgument=0, so Sun and Moon use
        // the same obliquity. MoonAge remains its independent source fraction.
        float moonPhase = A(M(M(input.MoonAge, 2), Pi), season);
        Vector4 sun = TransformRow(Rotate(initial, axis, season), transform);
        Vector4 moon = TransformRow(Rotate(initial, axis, moonPhase), transform);
        if (!Finite(sun) || !Finite(moon))
            throw new InvalidDataException("Original Skylight direction result is not finite.");
        return new(sun, moon);
    }

    private static Matrix4x4 RightY(Matrix4x4 m, float angle)
    {
        var t = OriginalNativeTrigonometry.SinCos014590(angle);
        return new(A(M(m.M13,t.Sin),M(m.M11,t.Cos)), m.M12, S(M(m.M13,t.Cos),M(m.M11,t.Sin)), m.M14,
            A(M(m.M23,t.Sin),M(m.M21,t.Cos)), m.M22, S(M(m.M23,t.Cos),M(m.M21,t.Sin)), m.M24,
            A(M(m.M33,t.Sin),M(m.M31,t.Cos)), m.M32, S(M(m.M33,t.Cos),M(m.M31,t.Sin)), m.M34,
            A(M(m.M43,t.Sin),M(m.M41,t.Cos)), m.M42, S(M(m.M43,t.Cos),M(m.M41,t.Sin)), m.M44);
    }

    private static Matrix4x4 RightX(Matrix4x4 m, float angle)
    {
        var t = OriginalNativeTrigonometry.SinCos014590(angle);
        return new(m.M11, S(M(m.M12,t.Cos),M(m.M13,t.Sin)), A(M(m.M12,t.Sin),M(m.M13,t.Cos)), m.M14,
            m.M21, S(M(m.M22,t.Cos),M(m.M23,t.Sin)), A(M(m.M22,t.Sin),M(m.M23,t.Cos)), m.M24,
            m.M31, S(M(m.M32,t.Cos),M(m.M33,t.Sin)), A(M(m.M32,t.Sin),M(m.M33,t.Cos)), m.M34,
            m.M41, S(M(m.M42,t.Cos),M(m.M43,t.Sin)), A(M(m.M42,t.Sin),M(m.M43,t.Cos)), m.M44);
    }

    private static Matrix4x4 RightZ(Matrix4x4 m, float angle)
    {
        var t = OriginalNativeTrigonometry.SinCos014590(angle);
        return new(S(M(m.M11,t.Cos),M(m.M12,t.Sin)), A(M(m.M11,t.Sin),M(m.M12,t.Cos)), m.M13, m.M14,
            S(M(m.M21,t.Cos),M(m.M22,t.Sin)), A(M(m.M21,t.Sin),M(m.M22,t.Cos)), m.M23, m.M24,
            S(M(m.M31,t.Cos),M(m.M32,t.Sin)), A(M(m.M31,t.Sin),M(m.M32,t.Cos)), m.M33, m.M34,
            S(M(m.M41,t.Cos),M(m.M42,t.Sin)), A(M(m.M41,t.Sin),M(m.M42,t.Cos)), m.M43, m.M44);
    }

    private static Matrix4x4 LeftY(Matrix4x4 m, float angle)
    {
        var t = OriginalNativeTrigonometry.SinCos014590(angle);
        return new(S(M(m.M11,t.Cos),M(m.M31,t.Sin)), S(M(m.M12,t.Cos),M(m.M32,t.Sin)), S(M(m.M13,t.Cos),M(m.M33,t.Sin)), 0,
            m.M21, m.M22, m.M23, 0,
            A(M(m.M11,t.Sin),M(m.M31,t.Cos)), A(M(m.M32,t.Cos),M(m.M12,t.Sin)), A(M(m.M33,t.Cos),M(m.M13,t.Sin)), 0,
            m.M41, m.M42, m.M43, 1);
    }

    // Native A901E0(out,a,b) returns b*a, using Y+X then Z for each entry.
    private static Matrix4x4 Combine3x3(Matrix4x4 a, Matrix4x4 b) => new(
        Dot3(b.M11,b.M12,b.M13,a.M11,a.M21,a.M31), Dot3(b.M11,b.M12,b.M13,a.M12,a.M22,a.M32), Dot3(b.M11,b.M12,b.M13,a.M13,a.M23,a.M33), 0,
        Dot3(b.M21,b.M22,b.M23,a.M11,a.M21,a.M31), Dot3(b.M21,b.M22,b.M23,a.M12,a.M22,a.M32), Dot3(b.M21,b.M22,b.M23,a.M13,a.M23,a.M33), 0,
        Dot3(b.M31,b.M32,b.M33,a.M11,a.M21,a.M31), Dot3(b.M31,b.M32,b.M33,a.M12,a.M22,a.M32), Dot3(b.M31,b.M32,b.M33,a.M13,a.M23,a.M33), 0,
        0, 0, 0, 1);

    private static float Dot3(float x,float y,float z,float a,float b,float c) => A(A(M(y,b),M(x,a)),M(z,c));

    private static Vector4 Rotate(Vector4 v, Vector4 axis, float angle)
    {
        var t = OriginalNativeTrigonometry.SinCos014590(angle);
        float dot = A(A(M(axis.X,v.X),M(axis.Y,v.Y)),M(axis.Z,v.Z));
        float k = M(dot,S(1,t.Cos));
        return new(A(A(M(axis.X,k),M(v.X,t.Cos)),M(S(M(v.Z,axis.Y),M(axis.Z,v.Y)),t.Sin)),
            A(A(M(k,axis.Y),M(t.Cos,v.Y)),M(S(M(axis.Z,v.X),M(v.Z,axis.X)),t.Sin)),
            A(A(M(k,axis.Z),M(t.Cos,v.Z)),M(S(M(axis.X,v.Y),M(v.X,axis.Y)),t.Sin)), v.W);
    }

    private static Vector4 TransformRow(Vector4 v, Matrix4x4 m) => new(
        A(A(A(M(v.Y,m.M21),M(v.X,m.M11)),M(v.Z,m.M31)),M(v.W,m.M41)),
        A(A(A(M(v.X,m.M12),M(v.Y,m.M22)),M(v.Z,m.M32)),M(v.W,m.M42)),
        A(A(A(M(v.X,m.M13),M(v.Y,m.M23)),M(v.Z,m.M33)),M(v.W,m.M43)),
        A(A(A(M(v.X,m.M14),M(v.Y,m.M24)),M(v.Z,m.M34)),M(v.W,m.M44)));

    private static float A(float a,float b) => OriginalNativeFloat.Add(a,b);
    private static float S(float a,float b) => OriginalNativeFloat.Subtract(a,b);
    private static float M(float a,float b) => OriginalNativeFloat.Multiply(a,b);
    private static bool Finite(Vector4 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z) && float.IsFinite(v.W);
}
