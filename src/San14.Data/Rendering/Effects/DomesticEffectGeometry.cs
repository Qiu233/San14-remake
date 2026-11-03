using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace San14.Data;

/// <summary>Pure domestic effect placement from the actual renderer view
/// position. The caller owns held-order scale carry and supplies the current
/// scale; this builder performs no map lookup, terrain adjustment or camera turn.</summary>
public static class DomesticEffectGeometry
{
    private const float Epsilon = 1.1920928955078125e-5f;

    /// <summary>The domestic wrapper's default forward is (0,0,1,0), not its
    /// up vector. Matrices use Matrix4x4 row vectors without transposition.
    /// The complete final fourth row is the supplied XYZW, including W.</summary>
    public static Matrix4x4 CreateTransform(Vector4 viewPosition, float uniformScale)
        => ComposeTransform(viewPosition, new Vector4(0f, 0f, 1f, 0f), uniformScale);

    /// <summary>The position/direction leaf shared by the source wrapper.
    /// Direction W does not participate in its XYZ basis construction.</summary>
    internal static Matrix4x4 ComposeTransform(Vector4 position, Vector4 direction, float scale)
    {
        if (!Sse.IsSupported)
            throw new PlatformNotSupportedException("Domestic effect matrix construction requires SSE.");

        float positionLength = Length(position.X, position.Y, position.Z);
        Matrix4x4 transform;
        // Near-zero position keeps identity, without applying uniform scale.
        // This is a strict comparison; unordered inputs retain the source's
        // subsequent arithmetic rather than becoming an invented valid basis.
        if (MathF.Abs(positionLength) < Epsilon) transform = Matrix4x4.Identity;
        else
        {
            float inversePositionLength = Divide(1f, positionLength);
            float x = Multiply(direction.X, inversePositionLength);
            float y = Multiply(direction.Y, inversePositionLength);
            float z = Multiply(direction.Z, inversePositionLength);
            float inverseDirectionLength = Divide(1f, Length(x, y, z));
            x = Multiply(x, inverseDirectionLength);
            y = Multiply(y, inverseDirectionLength);
            z = Multiply(z, inverseDirectionLength);

            float ux = 0f, uy = 1f, uz = 0f;
            // Both horizontal components must be strictly inside +/-epsilon.
            // Default forward Z never takes this generic near-vertical branch.
            if (x < Epsilon && x > -Epsilon && z < Epsilon && z > -Epsilon)
            {
                uy = 0f;
                uz = -y;
            }
            float rx = Subtract(Multiply(z, uy), Multiply(y, uz));
            float ry = Subtract(Multiply(x, uz), Multiply(z, ux));
            float rz = Subtract(Multiply(y, ux), Multiply(x, uy));
            float inverseRightLength = Divide(1f, Length(rx, ry, rz));
            rx = Multiply(rx, inverseRightLength);
            ry = Multiply(inverseRightLength, ry);
            rz = Multiply(inverseRightLength, rz);
            float qx = Subtract(Multiply(rz, y), Multiply(ry, z));
            float qy = Subtract(Multiply(rx, z), Multiply(rz, x));
            float qz = Subtract(Multiply(ry, x), Multiply(rx, y));
            var basis = new Matrix4x4(
                rx, ry, rz, 0f,
                qx, qy, qz, 0f,
                x, y, z, 0f,
                0f, 0f, 0f, 1f);
            var scaling = new Matrix4x4(
                scale, 0f, 0f, 0f,
                0f, scale, 0f, 0f,
                0f, 0f, scale, 0f,
                0f, 0f, 0f, 1f);
            transform = UnitWorldTransformMath.ComposeAffine17ADD0(basis, scaling);
        }
        // Translation is not scaled, normalized, or reconstructed from XYZ.
        transform.M41 = position.X;
        transform.M42 = position.Y;
        transform.M43 = position.Z;
        transform.M44 = position.W;
        return transform;
    }

    private static float Length(float x, float y, float z)
        => Sse.SqrtScalar(Vector128.CreateScalar(
            Add(Add(Multiply(x, x), Multiply(y, y)), Multiply(z, z)))).ToScalar();

    // Scalar SSE keeps every source float32 rounding and prevents fused
    // multiply-add or wider intermediates from replacing these operations.
    private static float Add(float x, float y)
        => Sse.AddScalar(Vector128.CreateScalar(x), Vector128.CreateScalar(y)).ToScalar();
    private static float Subtract(float x, float y)
        => Sse.SubtractScalar(Vector128.CreateScalar(x), Vector128.CreateScalar(y)).ToScalar();
    private static float Multiply(float x, float y)
        => Sse.MultiplyScalar(Vector128.CreateScalar(x), Vector128.CreateScalar(y)).ToScalar();
    private static float Divide(float x, float y)
        => Sse.DivideScalar(Vector128.CreateScalar(x), Vector128.CreateScalar(y)).ToScalar();
}
