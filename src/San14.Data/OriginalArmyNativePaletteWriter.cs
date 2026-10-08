using System.Numerics;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Labels the explicit caller snapshot; Current/Previous do not establish
/// that a snapshot was obtained from an active original animation.</summary>
public enum OriginalArmyNativePoseBufferKind { CallerProvided, Current, Previous }

public enum OriginalArmyNativePaletteBranch
{
    DirectJoint,
    ReferenceTimesJoint,
    SkippedJointOutsideCount
}

public sealed record OriginalArmyNativePaletteEntry(int PaletteEntryIndex,
    int SourceJointIndex, int SourceMatrixIndex, uint RawValue4,
    OriginalArmyNativePaletteBranch Branch, int? SourceReferenceMatrixIndex);

public sealed record OriginalArmyNativePaletteRows(int PaletteIndex, int JointCount,
    OriginalArmyNativePoseBufferKind PoseBufferKind, int WrittenEntryCount,
    IReadOnlyList<Vector4> Rows, IReadOnlyList<OriginalArmyNativePaletteEntry> Entries)
{
    public bool OriginalCurrentAnimationVerified => false;
}

/// <summary>Projects the native palette writer's explicit joint-matrix snapshot
/// into three shader dot rows per entry. RawValue4 == JointIndex selects the
/// joint directly; the other branch composes source reference × explicit joint.
/// Entries outside the signed joint count retain all three caller template rows.
/// This does not obtain or synthesize a current or previous animation pose.</summary>
public static class OriginalArmyNativePaletteWriter
{
    public static OriginalArmyNativePaletteRows Build(G1ModelTransforms transforms,
        int paletteIndex, IReadOnlyList<Matrix4x4> explicitJointMatrices, int jointCount,
        IReadOnlyList<Vector4> completeTemplate,
        OriginalArmyNativePoseBufferKind poseBufferKind = OriginalArmyNativePoseBufferKind.CallerProvided)
    {
        ArgumentNullException.ThrowIfNull(transforms);
        ArgumentNullException.ThrowIfNull(explicitJointMatrices);
        ArgumentNullException.ThrowIfNull(completeTemplate);
        if ((uint)paletteIndex >= transforms.JointPalettes.Count)
            throw Error("references a missing source joint palette");
        if (jointCount < 0 || jointCount > explicitJointMatrices.Count)
            throw Error("requires a nonnegative signed joint count within the explicit matrix snapshot");
        if (completeTemplate.Count != 384)
            throw Error("requires all 384 original shader row templates");
        if (!Enum.IsDefined(poseBufferKind))
            throw Error("has an unknown explicit pose buffer kind");
        G1JointPalette palette = transforms.JointPalettes[paletteIndex];
        if (palette.Index != paletteIndex || palette.Entries.Count > 128)
            throw Error("has a mismatched source palette or exceeds 128 shader matrices");

        Vector4[] rows = completeTemplate.ToArray();
        var entries = new OriginalArmyNativePaletteEntry[palette.Entries.Count];
        int written = 0;
        for (int i = 0; i < entries.Length; i++)
        {
            G1JointPaletteEntry entry = palette.Entries[i];
            if (entry.Index != i || entry.JointIndex < 0)
                throw Error("has a mismatched entry or a negative source joint index");
            if (entry.JointIndex >= jointCount)
            {
                entries[i] = Describe(entry, OriginalArmyNativePaletteBranch.SkippedJointOutsideCount, null);
                continue;
            }

            Matrix4x4 joint = explicitJointMatrices[entry.JointIndex];
            if (entry.RawValue4 == (uint)entry.JointIndex)
            {
                rows[3 * i] = new(joint.M11, joint.M21, joint.M31, joint.M41);
                rows[3 * i + 1] = new(joint.M12, joint.M22, joint.M32, joint.M42);
                rows[3 * i + 2] = new(joint.M13, joint.M23, joint.M33, joint.M43);
                entries[i] = Describe(entry, OriginalArmyNativePaletteBranch.DirectJoint, null);
            }
            else
            {
                if ((uint)entry.MatrixIndex >= transforms.ReferenceMatrices.Count ||
                    transforms.ReferenceMatrices[entry.MatrixIndex].Index != entry.MatrixIndex)
                    throw Error("references a missing or mismatched source reference matrix");
                Matrix4x4 reference = transforms.ReferenceMatrices[entry.MatrixIndex].Matrix;
                rows[3 * i] = Column(reference, joint.M11, joint.M21, joint.M31, joint.M41);
                rows[3 * i + 1] = Column(reference, joint.M12, joint.M22, joint.M32, joint.M42);
                rows[3 * i + 2] = Column(reference, joint.M13, joint.M23, joint.M33, joint.M43);
                entries[i] = Describe(entry, OriginalArmyNativePaletteBranch.ReferenceTimesJoint, entry.MatrixIndex);
            }
            written++;
        }
        return new(paletteIndex, jointCount, poseBufferKind, written,
            Array.AsReadOnly(rows), Array.AsReadOnly(entries));
    }

    private static OriginalArmyNativePaletteEntry Describe(G1JointPaletteEntry entry,
        OriginalArmyNativePaletteBranch branch, int? referenceIndex) =>
        new(entry.Index, entry.JointIndex, entry.MatrixIndex, entry.RawValue4, branch, referenceIndex);

    private static Vector4 Column(Matrix4x4 left, float rightX, float rightY, float rightZ, float rightW) => new(
        Component(left.M11, left.M12, left.M13, left.M14, rightX, rightY, rightZ, rightW),
        Component(left.M21, left.M22, left.M23, left.M24, rightX, rightY, rightZ, rightW),
        Component(left.M31, left.M32, left.M33, left.M34, rightX, rightY, rightZ, rightW),
        Component(left.M41, left.M42, left.M43, left.M44, rightX, rightY, rightZ, rightW));

    private static float Component(float x, float y, float z, float w,
        float rightX, float rightY, float rightZ, float rightW)
    {
        // Native 17B070 uses ((z * row2) + (x * row0)) + ((w * row3) + (y * row1)).
        float zx = Add(Multiply(z, rightZ), Multiply(x, rightX));
        float wy = Add(Multiply(w, rightW), Multiply(y, rightY));
        return Add(zx, wy);
    }

    // Separate non-inlined float operations preserve each native float32 rounding
    // boundary and prevent the JIT from replacing a multiply/add pair with FMA.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static float Multiply(float left, float right) => left * right;
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static float Add(float left, float right) => left + right;

    private static InvalidDataException Error(string message) => new($"Original Army native palette writer {message}.");
}
