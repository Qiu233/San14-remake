using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Explicit native root S/Q/T. This input supplies no animation
/// choice, scene transform or current original-frame observation.</summary>
public sealed record OriginalArmyAnimationRootTransform(Vector4 Scale,
    Quaternion Rotation, Vector4 Translation);

public sealed record OriginalArmyAnimationHierarchyPose(byte[] ResolvedPoseBytes,
    IReadOnlyList<OriginalArmyAnimationResolvedJoint> ResolvedJoints,
    IReadOnlyList<int> SourceParentIndices, IReadOnlyList<int> SourceJointRuns,
    bool UsesAlternateSourceRunCount)
{
    public bool OriginalCurrentAnimationVerified => false;
    public IReadOnlyList<Matrix4x4> BuildMatrices() => OriginalArmyAnimationPoseMatrices.Build(ResolvedJoints);
}

/// <summary>Original D8A620 local-to-resolved S/Q/T hierarchy arithmetic for
/// the verified source skeleton run profile. Parent rotation composes the local
/// quaternion, rotates parent-scaled local translation, and each resulting
/// quaternion is normalized with the original separate scalar operations.</summary>
public static class OriginalArmyAnimationHierarchy
{
    public static OriginalArmyAnimationHierarchyPose Build(G1ModelTransforms skeleton,
        OriginalArmyAnimationLocalPose localPose, OriginalArmyAnimationRootTransform root,
        bool useAlternateSourceRunCount = false)
    {
        ArgumentNullException.ThrowIfNull(skeleton);
        ArgumentNullException.ThrowIfNull(localPose);
        ArgumentNullException.ThrowIfNull(root);
        if (!Finite(root.Scale) || !Finite(root.Translation) || !Finite(root.Rotation))
            throw Error("requires finite explicit root scale, rotation and translation");
        IReadOnlyList<int> runs = OriginalArmyAnimationHierarchySources.ReadJointRuns(skeleton, useAlternateSourceRunCount);
        if ((long)skeleton.Joints.Count * 48 != localPose.LocalPoseBytes.Length)
            throw Error("requires a complete original local pose buffer");
        var localJoints = OriginalArmyAnimationPoseMatrices.ReadResolvedEntries(localPose.LocalPoseBytes, skeleton.Joints.Count);
        var resolved = new OriginalArmyAnimationResolvedJoint[localJoints.Count];
        var parents = new int[localJoints.Count];
        byte[] rawResolved = new byte[localPose.LocalPoseBytes.Length];
        int index = 0;
        foreach (int run in runs)
        {
            for (int i = 0; i < run; i++, index++)
            {
                OriginalArmyAnimationResolvedJoint local = localJoints[index];
                int parentIndex = parents[index] = BinaryPrimitives.ReadInt32LittleEndian(local.RawBytes.AsSpan(12));
                if (parentIndex >= index)
                    throw Error("requires already resolved preceding parents in the native source run order");
                Vector4 parentScale = root.Scale, parentTranslation = root.Translation;
                Quaternion parentRotation = root.Rotation;
                if (parentIndex >= 0)
                {
                    var parent = resolved[parentIndex];
                    parentScale = parent.Scale; parentRotation = parent.Rotation; parentTranslation = parent.Translation;
                }
                Quaternion rotation = OriginalArmyAnimationPoseMath.Multiply(parentRotation, local.Rotation);
                Vector4 scaledTranslation = new(Multiply(local.Translation.X, parentScale.X),
                    Multiply(local.Translation.Y, parentScale.Y), Multiply(parentScale.Z, local.Translation.Z), local.Translation.W);
                Vector4 translated = OriginalArmyAnimationPoseMath.Rotate(parentRotation, scaledTranslation);
                Vector4 translation = new(Add(parentTranslation.X, translated.X), Add(parentTranslation.Y, translated.Y),
                    Add(parentTranslation.Z, translated.Z), parentTranslation.W);
                Vector4 scale = new(Multiply(local.Scale.X, parentScale.X), Multiply(local.Scale.Y, parentScale.Y),
                    Multiply(local.Scale.Z, parentScale.Z), parentScale.W);
                float squared = Add(Add(Add(Multiply(rotation.Y, rotation.Y), Multiply(rotation.X, rotation.X)),
                    Multiply(rotation.Z, rotation.Z)), Multiply(rotation.W, rotation.W));
                float length = MathF.Sqrt(squared); // Native finite sqrtss helper.
                if (!float.IsFinite(length) || length == 0)
                    throw Error("encountered an unsupported zero or nonfinite composed quaternion");
                float factor = Divide(1, length);
                rotation = new(Multiply(rotation.X, factor), Multiply(factor, rotation.Y),
                    Multiply(factor, rotation.Z), Multiply(factor, rotation.W));
                if (!Finite(scale) || !Finite(rotation) || !Finite(translation))
                    throw Error("encountered a nonfinite resolved source transform");
                Span<byte> destination = rawResolved.AsSpan(index * 48, 48);
                Write(destination, scale);
                Write(destination[16..], new(rotation.X, rotation.Y, rotation.Z, rotation.W));
                Write(destination[32..], translation);
                resolved[index] = new(index, scale, rotation, translation, destination.ToArray());
            }
        }
        if (index != resolved.Length)
            throw Error("source runs do not consume the complete native joint buffer");
        return new(rawResolved, Array.AsReadOnly(resolved), Array.AsReadOnly(parents), runs, useAlternateSourceRunCount);
    }

    private static void Write(Span<byte> destination, Vector4 value)
    {
        BinaryPrimitives.WriteSingleLittleEndian(destination, value.X);
        BinaryPrimitives.WriteSingleLittleEndian(destination[4..], value.Y);
        BinaryPrimitives.WriteSingleLittleEndian(destination[8..], value.Z);
        BinaryPrimitives.WriteSingleLittleEndian(destination[12..], value.W);
    }
    private static bool Finite(Vector4 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z) && float.IsFinite(v.W);
    private static bool Finite(Quaternion q) => float.IsFinite(q.X) && float.IsFinite(q.Y) && float.IsFinite(q.Z) && float.IsFinite(q.W);
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Multiply(float a, float b) => a * b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Add(float a, float b) => a + b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Divide(float a, float b) => a / b;
    private static InvalidDataException Error(string message) => new($"Original Army animation hierarchy {message}.");
}
