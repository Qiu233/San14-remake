using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The actual ModelCPU +20 getter input: F58360 stores the separately
/// rounded center and absolute half extents, and C23660 reconstructs endpoints.
/// These are model bounds, including the source model's LOCAL pose extents.</summary>
public sealed record MapModelNativePreparedBounds(Vector3 Center3C, Vector3 HalfExtents30,
    Vector3 GetterMinimum, Vector3 GetterMaximum, string EvidenceUri, string EvidenceSha256);

/// <summary>Native A13250's 80-byte oriented box. All five W words are the
/// explicitly written positive zero. Basis vectors retain the raw quaternion;
/// they are neither normalized nor multiplied by WorldPQ scale.</summary>
public sealed record MapModelNativeOrientedBounds(Vector4 Center0, Vector4 Basis10,
    Vector4 Basis20, Vector4 Basis30, Vector4 HalfExtents40,
    string EvidenceUri, string EvidenceSha256, string Basis);

/// <summary>Current publication facts for the finite single-node header path.
/// Parent getters and DAEA00's optional child union require their own explicit
/// current sources. Visibility/noBound/queue flags remain selector inputs.</summary>
public sealed record MapModelNativeBoundsPublication(string CurrentSourceIdentity,
    bool? ParentBound, bool? ModelResourceBindingIsCurrent, bool? MergeChildren44Bit4)
{
    public static MapModelNativeBoundsPublication CurrentSingleUnparented(string identity)
        => new(identity, false, true, false);
}

/// <summary>Original ModelData -> ModelCPU getter -> WorldPQ DAE7D0 oriented
/// bound preparation. It reads canonical G1MG bounds; draw LOCAL matrices do
/// not participate in this prefix. Preparation is cached per source asset,
/// while current world fields and publication identity are supplied each time.</summary>
public static class MapModelNativeBoundsSources
{
    public const string NativeContractId = "ce-20261002-model-worldpq-oriented-bound";
    public const uint MinimumHalfExtentBits = 0x37480000;
    public static float MinimumHalfExtent => BitConverter.UInt32BitsToSingle(MinimumHalfExtentBits);
    private static readonly ConditionalWeakTable<MapStaticModelAsset, MapModelNativePreparedBounds> Cache = new();

    public static MapModelNativePreparedBounds PrepareSourceAsset(MapStaticModelAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        return Cache.GetValue(asset, static sourceAsset =>
        {
            LoadedRdbAsset source = sourceAsset.RawTransforms.Source;
            return PrepareHeader(sourceAsset.LocalMinimum, sourceAsset.LocalMaximum, source.ResourceUri, source.DecodedSha256);
        });
    }

    /// <summary>SC F583AE..F5849E / TC F573DE..F574CE, followed by the ModelCPU
    /// virtual+20 C23660 / TC C21FC0. This keeps both float32 rounding stages.</summary>
    public static MapModelNativePreparedBounds PrepareHeader(Vector3 minimum, Vector3 maximum,
        string evidenceUri, string evidenceSha256)
    {
        MapStaticModelTransforms.ValidateBounds(minimum, maximum);
        ValidateEvidence(evidenceUri, evidenceSha256);
        float ex = (maximum.X - minimum.X) * .5f;
        float ey = (maximum.Y - minimum.Y) * .5f;
        float ez = (maximum.Z - minimum.Z) * .5f;
        Vector3 extent = new(MathF.Abs(ex), MathF.Abs(ey), MathF.Abs(ez));
        Vector3 center = new((minimum.X + maximum.X) * .5f,
            (minimum.Y + maximum.Y) * .5f, (minimum.Z + maximum.Z) * .5f);
        Vector3 lo = new(center.X - extent.X, center.Y - extent.Y, center.Z - extent.Z);
        Vector3 hi = new(extent.X + center.X, extent.Y + center.Y, extent.Z + center.Z);
        MapStaticModelTransforms.ValidateBounds(lo, hi);
        return new(center, extent, lo, hi, evidenceUri, evidenceSha256);
    }

    public static MapModelNativeOrientedBounds FromSourceWorld(MapStaticModelInstance instance,
        MapModelNativeBoundsPublication publication)
    {
        ArgumentNullException.ThrowIfNull(instance); ArgumentNullException.ThrowIfNull(publication);
        if (string.IsNullOrWhiteSpace(publication.CurrentSourceIdentity) ||
            publication.CurrentSourceIdentity != instance.CurrentSourceIdentity)
            throw Error("current node identity differs from the instance publication");
        if (publication.ModelResourceBindingIsCurrent != true)
            throw Error("current model resource binding is unknown or differs from this asset");
        return BuildSingleNode(PrepareSourceAsset(instance.Asset),
            instance.SourceWorldState ?? throw Error("current WorldPQ fields are unknown"),
            publication.ParentBound, publication.MergeChildren44Bit4);
    }

    /// <summary>SC DAE7D0 / TC DAD880 after successful ModelCPU+20. The finite
    /// path requires explicit unparented world fields and disabled child union.
    /// Negative scale preserves the native maxss collapse to minimum extents.</summary>
    public static MapModelNativeOrientedBounds BuildSingleNode(MapModelNativePreparedBounds resource,
        UnitWorldTransformState world, bool? parentBound, bool? mergeChildren44Bit4)
    {
        ArgumentNullException.ThrowIfNull(resource); ArgumentNullException.ThrowIfNull(world);
        if (parentBound != false) throw Error("single-node source path requires explicit no-parent state");
        if (mergeChildren44Bit4 != false) throw Error("current child union requires explicit child bound sources");
        ValidateEvidence(resource.EvidenceUri, resource.EvidenceSha256);
        ValidateEvidence(world.EvidenceUri, world.EvidenceSha256);
        MapStaticModelTransforms.ValidateBounds(resource.GetterMinimum, resource.GetterMaximum);
        // Scale the getter's actual endpoints before either midpoint or extent.
        Vector3 lo = new(resource.GetterMinimum.X * world.Scale34.X,
            resource.GetterMinimum.Y * world.Scale34.Y, resource.GetterMinimum.Z * world.Scale34.Z);
        Vector3 hi = new(resource.GetterMaximum.X * world.Scale34.X,
            resource.GetterMaximum.Y * world.Scale34.Y, resource.GetterMaximum.Z * world.Scale34.Z);
        Vector3 middle = new((hi.X + lo.X) * .5f, (lo.Y + hi.Y) * .5f, (lo.Z + hi.Z) * .5f);
        Vector3 extents = new(MathF.Max((hi.X - lo.X) * .5f, MinimumHalfExtent),
            MathF.Max((hi.Y - lo.Y) * .5f, MinimumHalfExtent), MathF.Max((hi.Z - lo.Z) * .5f, MinimumHalfExtent));
        // 993A40 is the same separately rounded rotation builder as A12C80,
        // with zero translation. 016900 includes its explicit zero W products.
        Matrix4x4 rotation = UnitWorldTransformMath.QuaternionPositionA12C80(world.Quaternion24, Vector3.Zero);
        float x = middle.Y * rotation.M21 + middle.X * rotation.M11;
        x = x + middle.Z * rotation.M31; x = x + 0f * rotation.M41;
        float y = middle.X * rotation.M12 + middle.Y * rotation.M22;
        y = y + middle.Z * rotation.M32; y = y + 0f * rotation.M42;
        float z = middle.X * rotation.M13 + middle.Y * rotation.M23;
        z = z + middle.Z * rotation.M33; z = z + 0f * rotation.M43;
        var bound = new MapModelNativeOrientedBounds(new(x + world.Position18.X,
                y + world.Position18.Y, z + world.Position18.Z, 0f),
            new(rotation.M11, rotation.M12, rotation.M13, 0f),
            new(rotation.M21, rotation.M22, rotation.M23, 0f),
            new(rotation.M31, rotation.M32, rotation.M33, 0f), new(extents, 0f),
            resource.EvidenceUri, resource.EvidenceSha256,
            NativeContractId + "; F58360 / C23660 / DAE7D0 / 993A40 / 016900 / A13250; WORLD " +
            world.EvidenceUri + " SHA256=" + world.EvidenceSha256);
        Validate(bound); return bound;
    }

    public static void Validate(MapModelNativeOrientedBounds bound)
    {
        ArgumentNullException.ThrowIfNull(bound); ValidateEvidence(bound.EvidenceUri, bound.EvidenceSha256);
        foreach (Vector4 vector in new[] {bound.Center0, bound.Basis10, bound.Basis20, bound.Basis30, bound.HalfExtents40})
            if (!float.IsFinite(vector.X) || !float.IsFinite(vector.Y) || !float.IsFinite(vector.Z) ||
                BitConverter.SingleToUInt32Bits(vector.W) != 0)
                throw Error("oriented box requires finite XYZ and native positive-zero W words");
        if (bound.HalfExtents40.X < MinimumHalfExtent || bound.HalfExtents40.Y < MinimumHalfExtent ||
            bound.HalfExtents40.Z < MinimumHalfExtent || string.IsNullOrWhiteSpace(bound.Basis))
            throw Error("oriented box extents or evidence basis are incomplete");
    }

    private static void ValidateEvidence(string uri, string sha)
    {
        if (string.IsNullOrWhiteSpace(uri) || sha is not {Length: 64} || !sha.All(Uri.IsHexDigit))
            throw Error("bound input requires source URI and SHA-256");
    }
    private static InvalidDataException Error(string message) => new("Original native model bounds " + message + ".");
}
