using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace San14.Data;

/// <summary>Pure native unit-world matrix builders. Every scalar intermediate is
/// single precision, and the input quaternion is used without normalization.</summary>
public static class UnitWorldTransformMath
{
    /// <summary>Complete CE SC A12C80..A12DF5 / TC A11C80..A11DF5 builder. The native
    /// stores place translation in the fourth row, matching Matrix4x4 row vectors.</summary>
    public static Matrix4x4 QuaternionPositionA12C80(Quaternion rotation, Vector3 position)
    {
        // A12CBF/A12CD3/A12CE5 double by addition before the products.
        float z2 = rotation.Z + rotation.Z;
        float x2 = rotation.X + rotation.X;
        float y2 = rotation.Y + rotation.Y;
        float wz = rotation.W * z2;
        float xz = rotation.X * z2;
        float xy = rotation.Y * x2;
        float zz = rotation.Z * z2;
        float yz = rotation.Z * y2;
        float wy = rotation.W * y2;
        float yy = rotation.Y * y2;
        float xx = rotation.X * x2;
        float wx = rotation.W * x2;

        // A12D42..A12D59 subtract twice, with this exact order.
        float m11 = 1f - yy;
        m11 = m11 - zz;
        float m22 = 1f - zz;
        m22 = m22 - xx;
        float m33 = 1f - xx;
        m33 = m33 - yy;

        return new Matrix4x4(
            m11, xy + wz, xz - wy, 0f,
            xy - wz, m22, yz + wx, 0f,
            xz + wy, yz - wx, m33, 0f,
            position.X, position.Y, position.Z, 1f);
    }

    /// <summary>Complete CE SC A23210..A233B2 / TC A22170..A22312 builder.
    /// Each completed rotation entry is multiplied by its own basis-row scale;
    /// translation and the native positive-zero fourth-column stores are direct.</summary>
    public static Matrix4x4 ScaleQuaternionPositionA23210(
        Vector3 scale, Quaternion rotation, Vector3 position)
    {
        Matrix4x4 result = QuaternionPositionA12C80(rotation, position);
        // Both native builders have identical separately rounded rotation entries.
        // A232D4..A2335D multiply after the rotation additions/subtractions.
        result.M11 *= scale.X;
        result.M12 *= scale.X;
        result.M13 *= scale.X;
        result.M21 *= scale.Y;
        result.M22 *= scale.Y;
        result.M23 *= scale.Y;
        result.M31 *= scale.Z;
        result.M32 *= scale.Z;
        result.M33 *= scale.Z;
        return result;
    }

    /// <summary>Complete CE SC 17ADD0..17AF1B / TC 17B070..17B1BB matrix product.
    /// The caller supplies the first matrix in XMM0..3 and the second through RDX.
    /// Draw preparation calls this with WORLD shader rows first and primitive
    /// LOCAL shader rows second, then retains the first three returned rows.</summary>
    public static Matrix4x4 ComposeAffine17ADD0(Matrix4x4 first, Matrix4x4 second)
    {
        if (!Sse.IsSupported) throw new PlatformNotSupportedException("Original unit-world matrix composition requires SSE.");
        var b1 = Vector128.Create(second.M11, second.M12, second.M13, second.M14);
        var b2 = Vector128.Create(second.M21, second.M22, second.M23, second.M24);
        var b3 = Vector128.Create(second.M31, second.M32, second.M33, second.M34);
        var b4 = Vector128.Create(second.M41, second.M42, second.M43, second.M44);
        Vector128<float> a = ComposeRow(new(first.M11, first.M12, first.M13, first.M14), b1, b2, b3, b4);
        Vector128<float> b = ComposeRow(new(first.M21, first.M22, first.M23, first.M24), b1, b2, b3, b4);
        Vector128<float> c = ComposeRow(new(first.M31, first.M32, first.M33, first.M34), b1, b2, b3, b4);
        Vector128<float> d = ComposeRow(new(first.M41, first.M42, first.M43, first.M44), b1, b2, b3, b4);
        return new(
            a.GetElement(0), a.GetElement(1), a.GetElement(2), a.GetElement(3),
            b.GetElement(0), b.GetElement(1), b.GetElement(2), b.GetElement(3),
            c.GetElement(0), c.GetElement(1), c.GetElement(2), c.GetElement(3),
            d.GetElement(0), d.GetElement(1), d.GetElement(2), d.GetElement(3));
    }

    private static Vector128<float> ComposeRow(Vector4 first,
        Vector128<float> b1, Vector128<float> b2, Vector128<float> b3, Vector128<float> b4)
    {
        // 17AE48/17AE5A/17AE6F and corresponding operations for the other rows:
        // (z*row3 + x*row1) + (w*row4 + y*row2), with all seven SSE roundings.
        var z = Sse.Multiply(Vector128.Create(first.Z), b3);
        var x = Sse.Multiply(Vector128.Create(first.X), b1);
        var w = Sse.Multiply(Vector128.Create(first.W), b4);
        var y = Sse.Multiply(Vector128.Create(first.Y), b2);
        var zx = Sse.Add(z, x);
        var wy = Sse.Add(w, y);
        return Sse.Add(zx, wy);
    }
}
