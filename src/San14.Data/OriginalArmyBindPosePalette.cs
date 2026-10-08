using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalArmyBindPosePaletteRows(int PaletteIndex, int WrittenEntryCount,
    IReadOnlyList<Vector4> Rows, IReadOnlyList<int> SourceJointIndices,
    IReadOnlyList<int> SourceReferenceMatrixIndices)
{
    public bool OriginalCurrentAnimationVerified => false;
}

/// <summary>Adapts existing source reference × source joint bind matrices to
/// original PB2-I2m shader's three affine dot-product rows. Unused complete
/// template rows remain caller values; current pose and bSkin flags are separate.
/// This uses the same matrix chain as G1ArmyBindPoseReader, not resampled animation.</summary>
public static class OriginalArmyBindPosePalette
{
    public static OriginalArmyBindPosePaletteRows Build(G1ModelTransforms transforms,
        int paletteIndex, IReadOnlyList<Vector4> completeTemplate)
    {
        ArgumentNullException.ThrowIfNull(transforms);
        ArgumentNullException.ThrowIfNull(completeTemplate);
        if ((uint)paletteIndex >= transforms.JointPalettes.Count)
            throw new InvalidDataException("Army bind-pose palette references a missing source joint palette.");
        if (completeTemplate.Count != 384)
            throw new InvalidDataException("Army bind-pose palette requires all 384 original shader row templates.");
        var palette = transforms.JointPalettes[paletteIndex];
        if (palette.Entries.Count > 128)
            throw new InvalidDataException("Army bind-pose palette exceeds the actual original shader's 128 affine matrices.");
        var rows = completeTemplate.ToArray();
        int[] joints = new int[palette.Entries.Count], references = new int[palette.Entries.Count];
        for (int i = 0; i < palette.Entries.Count; i++)
        {
            var entry = palette.Entries[i];
            if (entry.Index != i || (uint)entry.JointIndex >= transforms.JointModelMatrices.Count ||
                (uint)entry.MatrixIndex >= transforms.ReferenceMatrices.Count ||
                transforms.ReferenceMatrices[entry.MatrixIndex].Index != entry.MatrixIndex)
                throw new InvalidDataException("Army bind-pose palette has a missing/mismatched original matrix or joint reference.");
            Matrix4x4 value = transforms.ReferenceMatrices[entry.MatrixIndex].Matrix * transforms.JointModelMatrices[entry.JointIndex];
            if (value.M14 != 0 || value.M24 != 0 || value.M34 != 0 || value.M44 != 1)
                throw new InvalidDataException("Army bind-pose palette is outside the verified source affine matrix profile.");
            rows[3*i] = new(value.M11,value.M21,value.M31,value.M41);
            rows[3*i+1] = new(value.M12,value.M22,value.M32,value.M42);
            rows[3*i+2] = new(value.M13,value.M23,value.M33,value.M43);
            if (!Finite(rows[3*i]) || !Finite(rows[3*i+1]) || !Finite(rows[3*i+2]))
                throw new InvalidDataException("Army bind-pose palette contains a non-finite source affine row.");
            joints[i] = entry.JointIndex; references[i] = entry.MatrixIndex;
        }
        return new(paletteIndex,palette.Entries.Count,Array.AsReadOnly(rows),
            Array.AsReadOnly(joints),Array.AsReadOnly(references));
    }

    private static bool Finite(Vector4 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) &&
        float.IsFinite(value.Z) && float.IsFinite(value.W);
}
