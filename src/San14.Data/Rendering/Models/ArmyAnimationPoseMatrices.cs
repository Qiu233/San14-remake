using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>A resolved model-space source pose entry. The fourth scale and
/// translation words are retained, but the native matrix writer does not use
/// them. This does not identify an active original animation.</summary>
public sealed record ArmyAnimationResolvedJoint(int JointIndex,
    Vector4 Scale, Quaternion Rotation, Vector4 Translation, byte[] RawBytes);

/// <summary>The original A22170 resolved S/Q/T to matrix conversion. This
/// conversion neither normalizes the source quaternion nor composes parents.
/// The caller must provide already resolved entries in native joint order.</summary>
public static class ArmyAnimationPoseMatrices
{
    public static IReadOnlyList<ArmyAnimationResolvedJoint> ReadResolvedEntries(
        ReadOnlySpan<byte> completeEntries, int jointCount)
    {
        if (jointCount < 0 || (long)jointCount * 48 != completeEntries.Length)
            throw Error("requires exactly 48 bytes per explicit resolved joint");
        var joints = new ArmyAnimationResolvedJoint[jointCount];
        for (int i = 0; i < joints.Length; i++)
        {
            ReadOnlySpan<byte> raw = completeEntries.Slice(i * 48, 48);
            joints[i] = new(i, Vector(raw, 0),
                new(Float(raw, 16), Float(raw, 20), Float(raw, 24), Float(raw, 28)),
                Vector(raw, 32), raw.ToArray());
        }
        return Array.AsReadOnly(joints);
    }

    public static IReadOnlyList<Matrix4x4> Build(
        IReadOnlyList<ArmyAnimationResolvedJoint> resolvedJoints)
    {
        ArgumentNullException.ThrowIfNull(resolvedJoints);
        var matrices = new Matrix4x4[resolvedJoints.Count];
        for (int i = 0; i < matrices.Length; i++)
        {
            ArmyAnimationResolvedJoint joint = resolvedJoints[i];
            if (joint is null || joint.JointIndex != i)
                throw Error("requires resolved joints in complete native index order");
            matrices[i] = Convert(joint.Scale, joint.Rotation, joint.Translation);
        }
        return Array.AsReadOnly(matrices);
    }

    public static Matrix4x4 Convert(Vector4 scale, Quaternion rotation, Vector4 translation)
    {
        float twiceZ = Add(rotation.Z, rotation.Z);
        float twiceX = Add(rotation.X, rotation.X);
        float twiceY = Add(rotation.Y, rotation.Y);
        float zz = Multiply(rotation.Z, twiceZ);
        float xz = Multiply(rotation.X, twiceZ);
        float wx = Multiply(rotation.W, twiceX);
        float wz = Multiply(rotation.W, twiceZ);
        float xy = Multiply(rotation.Y, twiceX);
        float yz = Multiply(rotation.Z, twiceY);
        float yy = Multiply(rotation.Y, twiceY);
        float wy = Multiply(rotation.W, twiceY);
        float xx = Multiply(rotation.X, twiceX);
        return new(
            Multiply(Subtract(Subtract(1, yy), zz), scale.X),
            Multiply(Add(xy, wz), scale.X),
            Multiply(Subtract(xz, wy), scale.X), 0,
            Multiply(Subtract(xy, wz), scale.Y),
            Multiply(Subtract(Subtract(1, zz), xx), scale.Y),
            Multiply(Add(yz, wx), scale.Y), 0,
            Multiply(Add(xz, wy), scale.Z),
            Multiply(Subtract(yz, wx), scale.Z),
            Multiply(Subtract(Subtract(1, xx), yy), scale.Z), 0,
            translation.X, translation.Y, translation.Z, 1);
    }

    private static Vector4 Vector(ReadOnlySpan<byte> bytes, int offset) => new(
        Float(bytes, offset), Float(bytes, offset + 4), Float(bytes, offset + 8), Float(bytes, offset + 12));
    private static float Float(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadSingleLittleEndian(bytes[offset..]);

    // Original scalar SSE operations round separately. Disabling inlining
    // prevents the JIT from collapsing native multiply/add boundaries to FMA.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static float Multiply(float left, float right) => left * right;
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static float Add(float left, float right) => left + right;
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static float Subtract(float left, float right) => left - right;
    private static InvalidDataException Error(string message) =>
        new($"Original Army animation pose matrices {message}.");
}
