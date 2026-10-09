using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Exact scalar and SSE-order math used when a Model::WorldPQ node's
/// first parent pointer is nonnull and that parent record's selector at +8 is -1.
/// Parent fields are scale +10, quaternion +20 and position +30. These methods
/// consume explicit source values; they do not infer a parent or current state.</summary>
public static class UnitWorldParentMath
{
    /// <summary>SC DB2270..DB2432. Native getter builds child and parent Q/P
    /// matrices from their raw quaternions, scales the local translation by
    /// (parent scale * local scale), composes child then parent and writes XYZ.
    /// The special branch leaves the caller's fourth position component untouched.</summary>
    public static Vector3 GetPosition(Vector3 localPosition, Quaternion localQuaternion,
        Vector3 localScale, Vector3 parentScale, Quaternion parentQuaternion, Vector3 parentPosition)
    {
        Vector3 translated = new(
            Product(Product(parentScale.X, localScale.X), localPosition.X),
            Product(Product(parentScale.Y, localScale.Y), localPosition.Y),
            Product(Product(parentScale.Z, localScale.Z), localPosition.Z));
        Matrix4x4 child = UnitWorldTransformMath.QuaternionPositionA12C80(localQuaternion, translated);
        Matrix4x4 parent = UnitWorldTransformMath.QuaternionPositionA12C80(parentQuaternion, parentPosition);
        Matrix4x4 result = UnitWorldTransformMath.ComposeAffine17ADD0(child, parent);
        return new(result.M41, result.M42, result.M43);
    }

    /// <summary>SC DB2490..DB250E: normalize the native ordered product of
    /// parent quaternion and raw local quaternion.</summary>
    public static Quaternion GetQuaternion(Quaternion localQuaternion, Quaternion parentQuaternion) =>
        NormalizeQuaternion1CF8B0(MultiplyQuaternion1CF760(parentQuaternion, localQuaternion));

    /// <summary>SC DB2560..DB25B1: three separately rounded local * parent products.</summary>
    public static Vector3 GetScale(Vector3 localScale, Vector3 parentScale) =>
        new(Product(localScale.X, parentScale.X), Product(localScale.Y, parentScale.Y),
            Product(localScale.Z, parentScale.Z));

    /// <summary>SC DC2730..DC2865. The caller supplies the quaternion and scale
    /// obtained by the current virtual +88 and +90 getters before the position
    /// write. The parent inverse is the native rigid transpose operation even
    /// when the supplied parent quaternion is nonunit.</summary>
    public static Vector3 SetPosition(Vector3 targetPosition, Quaternion currentWorldQuaternion,
        Vector3 currentWorldScale, Quaternion parentQuaternion, Vector3 parentPosition)
    {
        Matrix4x4 target = UnitWorldTransformMath.QuaternionPositionA12C80(currentWorldQuaternion, targetPosition);
        Matrix4x4 parent = UnitWorldTransformMath.QuaternionPositionA12C80(parentQuaternion, parentPosition);
        Matrix4x4 result = UnitWorldTransformMath.ComposeAffine17ADD0(target, InverseRigid3F6D20(parent));
        return new(Divide(result.M41, currentWorldScale.X),
            Divide(result.M42, currentWorldScale.Y), Divide(result.M43, currentWorldScale.Z));
    }

    /// <summary>SC DC2900..DC29AD. Flip only the three parent XYZ sign bits,
    /// multiply that conjugate by the target quaternion, then normalize.</summary>
    public static Quaternion SetQuaternion(Quaternion targetQuaternion, Quaternion parentQuaternion) =>
        NormalizeQuaternion1CF8B0(MultiplyQuaternion1CF760(ConjugateQuaternion(parentQuaternion), targetQuaternion));

    /// <summary>SC DC2C00..DC2C6D: three scalar target / parent divisions.
    /// Zero divisors retain the native floating point infinities or NaNs.</summary>
    public static Vector3 SetScale(Vector3 targetScale, Vector3 parentScale) =>
        new(Divide(targetScale.X, parentScale.X), Divide(targetScale.Y, parentScale.Y),
            Divide(targetScale.Z, parentScale.Z));

    /// <summary>SC 1CF760..1CF8B0. This preserves the native grouping of each
    /// product/add/subtract rather than the framework quaternion operator.</summary>
    public static Quaternion MultiplyQuaternion1CF760(Quaternion first, Quaternion second)
    {
        float dot = Add(Product(second.X, first.X), Product(second.Y, first.Y));
        dot = Add(dot, Product(second.Z, first.Z));
        float w = Subtract(Product(second.W, first.W), dot);
        float x = Add(Subtract(Product(second.Z, first.Y), Product(first.Z, second.Y)),
            Add(Product(first.X, second.W), Product(second.X, first.W)));
        float y = Add(Subtract(Product(first.Z, second.X), Product(second.Z, first.X)),
            Add(Product(first.Y, second.W), Product(second.Y, first.W)));
        float z = Add(Subtract(Product(first.X, second.Y), Product(second.X, first.Y)),
            Add(Product(first.Z, second.W), Product(second.Z, first.W)));
        return new(x, y, z, w);
    }

    /// <summary>SC 1CF8B0..1CF947 with the source sqrtss finite branch in
    /// 10209D8. Squared length accumulates X,Y,Z,W before 1/sqrt and the four
    /// scalar multiplies. No zero-length fallback is introduced.</summary>
    public static Quaternion NormalizeQuaternion1CF8B0(Quaternion value)
    {
        float lengthSquared = Add(Product(value.X, value.X), Product(value.Y, value.Y));
        lengthSquared = Add(lengthSquared, Product(value.Z, value.Z));
        lengthSquared = Add(lengthSquared, Product(value.W, value.W));
        float factor = Divide(1f, MathF.Sqrt(lengthSquared));
        return new(Product(value.X, factor), Product(factor, value.Y),
            Product(factor, value.Z), Product(factor, value.W));
    }

    public static Quaternion ConjugateQuaternion(Quaternion value) =>
        new(FlipSign(value.X), FlipSign(value.Y), FlipSign(value.Z), value.W);

    /// <summary>Complete SC 3F6D20..3F6E86. Native code transposes the 3x3,
    /// stores +0 in the last column and 1 at M44, and sign-XORs the three
    /// separately grouped translation dot products. It does not test a
    /// determinant, divide the basis, or reject nonunit input rotations.</summary>
    public static Matrix4x4 InverseRigid3F6D20(Matrix4x4 value)
    {
        float x = Add(Product(value.M42, value.M12), Product(value.M41, value.M11));
        x = Add(x, Product(value.M43, value.M13));
        float y = Add(Product(value.M42, value.M22), Product(value.M41, value.M21));
        y = Add(y, Product(value.M43, value.M23));
        float z = Add(Product(value.M42, value.M32), Product(value.M41, value.M31));
        z = Add(z, Product(value.M43, value.M33));
        return new(value.M11, value.M21, value.M31, 0f,
            value.M12, value.M22, value.M32, 0f,
            value.M13, value.M23, value.M33, 0f,
            FlipSign(x), FlipSign(y), FlipSign(z), 1f);
    }

    private static float FlipSign(float value) =>
        BitConverter.UInt32BitsToSingle(BitConverter.SingleToUInt32Bits(value) ^ 0x80000000u);
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Product(float a, float b) => a * b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Add(float a, float b) => a + b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Subtract(float a, float b) => a - b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Divide(float a, float b) => a / b;
}
