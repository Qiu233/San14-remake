using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

public sealed record ArmyAnimationMappedJoint(int PackedTableIndex,
    int SourceJointMapIndex, int? SourceJointIndex, bool RotationSampled,
    bool TranslationSampled, bool ScaleSampled);

public sealed record ArmyAnimationLocalPose(float TimeSeconds,
    uint FixedFrameTime, byte[] LocalPoseBytes,
    IReadOnlyList<ArmyAnimationMappedJoint> MappedJoints)
{
    public bool HierarchyResolved => false;
    public bool AnimationStateFidelityVerified => false;
}

/// <summary>Ports native D84810's packed track to original skeleton-map write.
/// Missing channels copy the original skeleton defaults. Unmapped tracks and
/// joints absent from the clip retain the explicit complete local pose template.
/// The result is local S/Q/T and still requires native hierarchy resolution.</summary>
public static class ArmyAnimationLocalPoseWriter
{
    public static ArmyAnimationLocalPose Build(G1ModelTransforms skeleton,
        ArmyAnimationClip clip, ArmyAnimationSample sample,
        ReadOnlySpan<byte> completeLocalPoseTemplate)
    {
        ArgumentNullException.ThrowIfNull(skeleton);
        ArgumentNullException.ThrowIfNull(clip);
        ArgumentNullException.ThrowIfNull(sample);
        if (!sample.Complete)
            throw Error("cannot assemble unsupported sampled channels");
        if (skeleton.Joints.Count > short.MaxValue || skeleton.RawJointMap.Count > short.MaxValue ||
            (long)skeleton.Joints.Count * 48 != completeLocalPoseTemplate.Length)
            throw Error("requires the exact original signed skeleton counts and complete 48-byte local pose template");
        if (sample.Joints.Count != clip.PackedJointChannels.Count || sample.FixedFrameTime !=
            ArmyAnimationSampler.ToFixedFrameTime(sample.TimeSeconds, clip.FrameRate, clip.FrameCount))
            throw Error("sample does not match the explicit clip/time or complete native track order");
        byte[] output = completeLocalPoseTemplate.ToArray();
        var mappings = new ArmyAnimationMappedJoint[sample.Joints.Count];
        for (int i = 0; i < mappings.Length; i++)
        {
            ArmyAnimationJointSample jointSample = sample.Joints[i];
            int mapIndex = (int)((clip.PackedJointChannels[i] >> 4) & 0x3FF);
            if (jointSample.PackedTableIndex != i || jointSample.JointIndex != mapIndex)
                throw Error("sample lost original packed track order or skeleton map identity");
            int? target = null;
            if (mapIndex < skeleton.RawJointMap.Count)
            {
                int signedTarget = unchecked((short)skeleton.RawJointMap[mapIndex]);
                if (signedTarget >= 0) target = signedTarget;
            }
            mappings[i] = new(i, mapIndex, target, jointSample.Rotation is not null,
                jointSample.Translation is not null, jointSample.Scale is not null);
            if (target is null) continue;
            if ((uint)target.Value >= skeleton.Joints.Count ||
                skeleton.Joints[target.Value].Index != target.Value || skeleton.Joints[target.Value].RawBytes.Length != 48)
                throw Error("mapped track references a missing or mismatched original skeleton joint");
            ReadOnlySpan<byte> defaults = skeleton.Joints[target.Value].RawBytes;
            Span<byte> destination = output.AsSpan(target.Value * 48, 48);
            if (jointSample.Rotation is { } rotation)
                Write(destination[16..], new Vector4(rotation.X, rotation.Y, rotation.Z, rotation.W));
            else defaults.Slice(16, 16).CopyTo(destination[16..]);
            if (jointSample.Translation is { } translation)
                Write(destination[32..], new Vector4(translation, 1));
            else defaults.Slice(32, 16).CopyTo(destination[32..]);
            if (jointSample.Scale is { } scale)
            {
                BinaryPrimitives.WriteSingleLittleEndian(destination, scale.X);
                BinaryPrimitives.WriteSingleLittleEndian(destination[4..], scale.Y);
                BinaryPrimitives.WriteSingleLittleEndian(destination[8..], scale.Z);
            }
            else defaults[..12].CopyTo(destination);
            // Original destination +0C (parent/control word) is never overwritten.
        }
        return new(sample.TimeSeconds, sample.FixedFrameTime, output, Array.AsReadOnly(mappings));
    }

    private static void Write(Span<byte> destination, Vector4 value)
    {
        BinaryPrimitives.WriteSingleLittleEndian(destination, value.X);
        BinaryPrimitives.WriteSingleLittleEndian(destination[4..], value.Y);
        BinaryPrimitives.WriteSingleLittleEndian(destination[8..], value.Z);
        BinaryPrimitives.WriteSingleLittleEndian(destination[12..], value.W);
    }
    private static InvalidDataException Error(string message) =>
        new($"Original Army animation local pose writer {message}.");
}
