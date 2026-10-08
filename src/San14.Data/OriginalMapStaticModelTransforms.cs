using System.Numerics;

namespace San14.Data;

/// <summary>Complete shader-ready affine rows with their source evidence. Both
/// matrices remain explicit because original CPU normal and world transforms
/// are separate inputs. Rows use the original dp4/dp3 column-vector shader
/// convention; Matrix4x4 adapters below use System.Numerics row vectors.</summary>
public sealed record OriginalMapStaticModelTransform(float[] PositionRows, float[] NormalRows,
    string EvidenceUri, string EvidenceSha256, string Basis)
{
    public OriginalMapStaticModelTransform Snapshot()
    {
        OriginalMapStaticModelTransforms.Validate(this);
        return this with {PositionRows = PositionRows.ToArray(), NormalRows = NormalRows.ToArray()};
    }
}

public sealed record OriginalMapStaticModelCamera(float[] WorldToProjectionRows, float[] WorldToViewRows,
    TerrainFrustumPlanes Frustum)
{
    public static OriginalMapStaticModelCamera FromExplicitRows(float[] projectionRows, float[] viewRows)
    {
        OriginalMapStaticModelTransforms.ValidateRows(projectionRows, 16);
        OriginalMapStaticModelTransforms.ValidateRows(viewRows, 12);
        // mW2P is row-major storage for a row-vector world product. The three
        // mWV float4s are supplied separately in the source shader's row order.
        Matrix4x4 clip = new(projectionRows[0], projectionRows[1], projectionRows[2], projectionRows[3],
            projectionRows[4], projectionRows[5], projectionRows[6], projectionRows[7],
            projectionRows[8], projectionRows[9], projectionRows[10], projectionRows[11],
            projectionRows[12], projectionRows[13], projectionRows[14], projectionRows[15]);
        return new(projectionRows.ToArray(), viewRows.ToArray(), TerrainFrustumMath.Create(clip));
    }
}

/// <summary>Storage conversion and conservative bounds only. No missing matrix
/// becomes identity; no scene quaternion is normalized or interpreted, and no
/// preview height is substituted for original world Y.</summary>
public static class OriginalMapStaticModelTransforms
{
    public static OriginalMapStaticModelTransform FromExplicitMatrices(Matrix4x4 position,
        Matrix4x4 normal, string evidenceUri, string evidenceSha256, string basis)
    {
        ValidateAffine(position); ValidateAffine(normal);
        return FromExplicitShaderRows(ToShaderAffineRows(position), ToShaderAffineRows(normal),
            evidenceUri, evidenceSha256, basis);
    }

    public static OriginalMapStaticModelTransform FromExplicitShaderRows(float[] positionRows,
        float[] normalRows, string evidenceUri, string evidenceSha256, string basis)
    {
        var result = new OriginalMapStaticModelTransform(positionRows, normalRows, evidenceUri, evidenceSha256, basis);
        return result.Snapshot();
    }

    public static float[] ToShaderAffineRows(Matrix4x4 matrix)
    {
        ValidateAffine(matrix);
        return [matrix.M11, matrix.M21, matrix.M31, matrix.M41,
            matrix.M12, matrix.M22, matrix.M32, matrix.M42,
            matrix.M13, matrix.M23, matrix.M33, matrix.M43];
    }

    public static Vector3 TransformPosition(Vector3 position, OriginalMapStaticModelTransform transform)
    {
        Validate(transform); ValidateVector(position);
        float[] m = transform.PositionRows;
        var result = new Vector3(position.X * m[0] + position.Y * m[1] + position.Z * m[2] + m[3],
            position.X * m[4] + position.Y * m[5] + position.Z * m[6] + m[7],
            position.X * m[8] + position.Y * m[9] + position.Z * m[10] + m[11]);
        ValidateVector(result); return result;
    }

    public static (Vector3 Minimum, Vector3 Maximum) WorldBounds(OriginalMapStaticModelAsset asset,
        OriginalMapStaticModelTransform transform)
    {
        ArgumentNullException.ThrowIfNull(asset); ValidateBounds(asset.LocalMinimum, asset.LocalMaximum); Validate(transform);
        Vector3 minimum = new(float.PositiveInfinity), maximum = new(float.NegativeInfinity);
        for (int mask = 0; mask < 8; mask++)
        {
            var p = new Vector3((mask & 1) == 0 ? asset.LocalMinimum.X : asset.LocalMaximum.X,
                (mask & 2) == 0 ? asset.LocalMinimum.Y : asset.LocalMaximum.Y,
                (mask & 4) == 0 ? asset.LocalMinimum.Z : asset.LocalMaximum.Z);
            Vector3 world = TransformPosition(p, transform);
            minimum = Vector3.Min(minimum, world); maximum = Vector3.Max(maximum, world);
        }
        return (minimum, maximum);
    }

    public static void Validate(OriginalMapStaticModelTransform transform)
    {
        ArgumentNullException.ThrowIfNull(transform); ValidateRows(transform.PositionRows, 12); ValidateRows(transform.NormalRows, 12);
        if (string.IsNullOrWhiteSpace(transform.EvidenceUri) || string.IsNullOrWhiteSpace(transform.Basis) ||
            transform.EvidenceSha256 is not {Length: 64} hash || !hash.All(Uri.IsHexDigit))
            throw new InvalidDataException("Native map model matrices require source evidence URI, SHA-256 and basis.");
        foreach (float[] p in new[] {transform.PositionRows, transform.NormalRows})
        {
            float determinant = p[0] * (p[5] * p[10] - p[6] * p[9]) -
                p[1] * (p[4] * p[10] - p[6] * p[8]) + p[2] * (p[4] * p[9] - p[5] * p[8]);
            if (!float.IsFinite(determinant) || determinant == 0)
                throw new InvalidDataException("Native map model matrix is singular or unresolved.");
        }
    }

    internal static void ValidateRows(float[]? rows, int count)
    {
        if (rows is null || rows.Length != count || rows.Any(v => !float.IsFinite(v)))
            throw new InvalidDataException("Native map model requires complete finite explicit matrix rows.");
    }
    internal static void ValidateBounds(Vector3 minimum, Vector3 maximum)
    {
        ValidateVector(minimum); ValidateVector(maximum);
        if (minimum.X > maximum.X || minimum.Y > maximum.Y || minimum.Z > maximum.Z)
            throw new InvalidDataException("Native map model bounds have reversed source endpoints.");
    }
    private static void ValidateAffine(Matrix4x4 m)
    {
        ValidateRows([m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24,
            m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44], 16);
        if (m.M14 != 0 || m.M24 != 0 || m.M34 != 0 || m.M44 != 1)
            throw new InvalidDataException("Native map model matrix has an unsupported affine row layout.");
    }
    private static void ValidateVector(Vector3 v)
    {
        if (!float.IsFinite(v.X) || !float.IsFinite(v.Y) || !float.IsFinite(v.Z))
            throw new InvalidDataException("Native map model vector exceeds finite float arithmetic.");
    }
}
