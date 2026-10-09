using System.Numerics;

namespace San14.Data;

public readonly record struct TerrainFrustumPlane(Vector4 Equation, byte SupportX, byte SupportY, byte SupportZ);
public sealed record TerrainFrustumPlanes(IReadOnlyList<TerrainFrustumPlane> Planes);

/// <summary>Original ordinary View plane builder and AABB test, given an explicit row-vector clip matrix.</summary>
public static class TerrainFrustumMath
{
    /// <summary>Original normal RH perspective, clip Z in [0,1], with explicit owned lens inputs.</summary>
    public static Matrix4x4 Perspective(float verticalFovRadians, float aspect, float near, float far,
        bool reverseDepth = false, float projectionScale = 1)
    {
        if (!float.IsFinite(verticalFovRadians) || verticalFovRadians <= 0 || verticalFovRadians >= MathF.PI ||
            !float.IsFinite(aspect) || aspect <= 0 || !float.IsFinite(near) || near <= 0 ||
            !float.IsFinite(far) || far <= near || !float.IsFinite(projectionScale) || projectionScale <= 0)
            throw new InvalidDataException("Invalid ordinary View perspective inputs.");
        float halfAngle = verticalFovRadians * 0.5f;
        float denominator = MathF.Tan(halfAngle) * projectionScale;
        float yScale = 1f / denominator, xScale = yScale / aspect;
        float range = reverseDepth ? far - near : near - far;
        float depthScale = (reverseDepth ? near : far) / range;
        float depthOffset = depthScale * (reverseDepth ? far : near);
        var result = new Matrix4x4(xScale, 0, 0, 0, 0, yScale, 0, 0,
            0, 0, depthScale, -1, 0, 0, depthOffset, 0);
        if (!float.IsFinite(xScale) || !float.IsFinite(yScale) || !float.IsFinite(depthScale) || !float.IsFinite(depthOffset))
            throw new InvalidDataException("Original View perspective exceeds finite float arithmetic.");
        return result;
    }

    /// <summary>Original 17ADD0 row product grouping, rather than the framework's SIMD dot reduction.</summary>
    public static Matrix4x4 Multiply(Matrix4x4 left, Matrix4x4 right)
    {
        Vector4[] rows = [new(left.M11,left.M12,left.M13,left.M14), new(left.M21,left.M22,left.M23,left.M24),
            new(left.M31,left.M32,left.M33,left.M34), new(left.M41,left.M42,left.M43,left.M44)];
        Vector4[] columns = [new(right.M11,right.M21,right.M31,right.M41), new(right.M12,right.M22,right.M32,right.M42),
            new(right.M13,right.M23,right.M33,right.M43), new(right.M14,right.M24,right.M34,right.M44)];
        var values = new float[16];
        for (int row = 0; row < 4; row++)
            for (int column = 0; column < 4; column++)
            {
                Vector4 a = rows[row], b = columns[column];
                if (!Finite(a) || !Finite(b)) throw new InvalidDataException("Nonfinite original View matrix product input.");
                float x = a.X * b.X, y = a.Y * b.Y, z = a.Z * b.Z, w = a.W * b.W;
                float value = (z + x) + (w + y);
                if (!float.IsFinite(value)) throw new InvalidDataException("Original View matrix product exceeds finite float arithmetic.");
                values[row * 4 + column] = value;
            }
        return new(values[0],values[1],values[2],values[3],values[4],values[5],values[6],values[7],
            values[8],values[9],values[10],values[11],values[12],values[13],values[14],values[15]);
    }

    public static TerrainFrustumPlanes Create(Matrix4x4 worldToProjection, bool swapDepthPlanes = false)
    {
        Vector4 c0 = new(worldToProjection.M11, worldToProjection.M21, worldToProjection.M31, worldToProjection.M41);
        Vector4 c1 = new(worldToProjection.M12, worldToProjection.M22, worldToProjection.M32, worldToProjection.M42);
        Vector4 c2 = new(worldToProjection.M13, worldToProjection.M23, worldToProjection.M33, worldToProjection.M43);
        Vector4 c3 = new(worldToProjection.M14, worldToProjection.M24, worldToProjection.M34, worldToProjection.M44);
        if (!Finite(c0) || !Finite(c1) || !Finite(c2) || !Finite(c3))
            throw new InvalidDataException("Nonfinite original View clip matrix.");
        Vector4[] raw = [-c3 - c0, c0 - c3, -c3 - c1, c1 - c3, -c2, c2 - c3];
        if (swapDepthPlanes) (raw[4], raw[5]) = (raw[5], raw[4]);
        TerrainFrustumPlane[] planes = new TerrainFrustumPlane[6];
        for (int i = 0; i < planes.Length; i++)
        {
            Vector4 n = raw[i];
            float xx = n.X * n.X, yy = n.Y * n.Y, zz = n.Z * n.Z;
            float length = MathF.Sqrt((xx + yy) + zz);
            if (!float.IsFinite(length) || length <= 0)
                throw new InvalidDataException("Unsupported degenerate original View plane.");
            float inverse = 1f / length;
            Vector4 equation = new(inverse * n.X, inverse * n.Y, inverse * n.Z, inverse * n.W);
            if (!Finite(equation)) throw new InvalidDataException("Original View normalization exceeds finite float arithmetic.");
            planes[i] = new(equation, equation.X < 0 ? (byte)1 : (byte)0,
                equation.Y < 0 ? (byte)1 : (byte)0, equation.Z < 0 ? (byte)1 : (byte)0);
        }
        return new(Array.AsReadOnly(planes));
    }

    public static bool IntersectsAabb(TerrainFrustumPlanes frustum, Vector3 minimum, Vector3 maximum)
    {
        ArgumentNullException.ThrowIfNull(frustum);
        if (frustum.Planes is null || frustum.Planes.Count != 6 ||
            !Finite(minimum) || !Finite(maximum) || minimum.X > maximum.X || minimum.Y > maximum.Y || minimum.Z > maximum.Z)
            throw new InvalidDataException("Invalid ordinary View box or plane set.");
        // Preserve the source short-circuit order, including the depth plane
        // checked between horizontal and vertical pairs. Equality rejects.
        foreach (int index in new[] { 0, 1, 4, 2, 3, 5 })
        {
            TerrainFrustumPlane plane = frustum.Planes[index];
            if (!Finite(plane.Equation) || plane.SupportX > 1 || plane.SupportY > 1 || plane.SupportZ > 1)
                throw new InvalidDataException("Unsupported original View support-point flags.");
            float x = plane.SupportX == 0 ? minimum.X : maximum.X;
            float y = plane.SupportY == 0 ? minimum.Y : maximum.Y;
            float z = plane.SupportZ == 0 ? minimum.Z : maximum.Z;
            float xTerm = x * plane.Equation.X, yTerm = y * plane.Equation.Y, zTerm = z * plane.Equation.Z;
            float dot = ((yTerm + xTerm) + zTerm) + plane.Equation.W;
            if (!float.IsFinite(dot)) throw new InvalidDataException("Original View box test exceeds finite float arithmetic.");
            if (!(dot < 0)) return false;
        }
        return true;
    }

    private static bool Finite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    private static bool Finite(Vector4 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z) && float.IsFinite(value.W);
}
