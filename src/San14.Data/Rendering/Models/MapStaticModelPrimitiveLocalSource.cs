using System.Numerics;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Three original shader affine rows, or the proven null primitive+48
/// branch for a negative raw draw+8. Null never represents unknown rows.</summary>
public sealed record MapStaticModelPrimitiveLocalRows(float[]? Rows,
    string EvidenceUri, string EvidenceSha256, string Basis);

/// <summary>Fresh ModelData LOCAL preparation: BD8FA0 copies actual skeleton
/// defaults, D8A620 resolves their hierarchy with the native absent-root literals,
/// A22170 builds joint matrices, and D8A470 writes the original palette rows.
/// Optional group-mode1 named matrices belong to primitive+50, not this +48
/// input. Later animation or wrapper replacement requires explicit current rows.</summary>
public static class MapStaticModelPrimitiveLocalSource
{
    private sealed record PreparedSource(
        IReadOnlyDictionary<int, MapStaticModelPrimitiveLocalRows> Rows);
    private static readonly ConditionalWeakTable<MapStaticModelAsset, PreparedSource> Cache = new();

    public static IReadOnlyDictionary<int, MapStaticModelPrimitiveLocalRows> Prepare(
        MapStaticModelAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        PreparedSource cached = Cache.GetValue(asset, PrepareSource);
        // Caller-owned arrays cannot change the canonical source rows retained by
        // the asset cache. A new asset revision receives a separate cache entry.
        return cached.Rows.ToDictionary(pair => pair.Key,
            pair => pair.Value with { Rows = pair.Value.Rows?.ToArray() });
    }

    private static PreparedSource PrepareSource(MapStaticModelAsset asset)
    {
        LoadedRdbAsset source = asset.RawTransforms.Source;
        G1ModelGeometry geometry = asset.RawTransforms.Geometry;
        G1ModelTransforms skeleton = asset.RawTransforms.Transforms;

        var result = new Dictionary<int, MapStaticModelPrimitiveLocalRows>();
        IReadOnlyList<Matrix4x4>? matrices = null;
        foreach (G1DrawRecord draw in asset.Draws)
        {
            if (unchecked((int)draw.UnknownValue8) < 0)
            {
                result.Add(draw.Index, new(null, source.ResourceUri, source.DecodedSha256,
                    "native F57CD6..F57D8C negative raw draw+8 retains null primitive+48"));
                continue;
            }
            if (draw.UnknownValue8 >= skeleton.JointPalettes.Count)
                throw Error($"draw {draw.Index} references a missing LOCAL palette");
            var palette = skeleton.JointPalettes[(int)draw.UnknownValue8];
            if (palette.Entries.Count != 1 || geometry.Layouts[draw.LayoutIndex].Attributes
                    .Any(attribute => (attribute.SemanticCode & 255) == 2))
                throw Error($"draw {draw.Index} requires the original indexed/skinned LOCAL palette path");
            if (matrices is null)
            {
                byte[] localBytes = skeleton.Joints.SelectMany(joint => joint.RawBytes).ToArray();
                var local = new ArmyAnimationLocalPose(0, 0, localBytes, []);
                // F57A65..F57AA2 clears the pose pointers, including root+28.
                // D8A633/D8A64C consequently keep these actual source literals.
                var root = new ArmyAnimationRootTransform(new(1, 1, 1, 0),
                    new(0, 0, 0, 1), new(0, 0, 0, 1));
                matrices = ArmyAnimationHierarchy.Build(skeleton, local, root,
                    useAlternateSourceRunCount: true).BuildMatrices();
            }
            G1JointPaletteEntry entry = palette.Entries[0];
            if ((uint)entry.JointIndex >= matrices.Count)
                throw Error($"draw {draw.Index} skips its LOCAL write and requires an explicit prior destination");
            // Every requested row is written; the unused 381-row temporary cannot
            // supply any returned word or hide a skipped native write.
            var native = ArmyNativePaletteWriter.Build(skeleton, (int)draw.UnknownValue8,
                matrices, matrices.Count, new Vector4[384]);
            if (native.WrittenEntryCount != 1)
                throw Error($"draw {draw.Index} has no complete native LOCAL write");
            Vector4 a = native.Rows[0], b = native.Rows[1], c = native.Rows[2];
            float[] rows = [a.X, a.Y, a.Z, a.W, b.X, b.Y, b.Z, b.W, c.X, c.Y, c.Z, c.W];
            if (rows.Any(value => !float.IsFinite(value)))
                throw Error($"draw {draw.Index} has nonfinite native LOCAL rows");
            result.Add(draw.Index, new(rows, source.ResourceUri, source.DecodedSha256,
                $"fresh ModelData BD8FA0 source defaults / D8A620 absent-root hierarchy / A22170 / D8A470 {native.Entries[0].Branch}; palette {draw.UnknownValue8}; primitive+48"));
        }
        return new(result);
    }

    private static InvalidDataException Error(string message) =>
        new($"Original static primitive LOCAL {message}.");
}
