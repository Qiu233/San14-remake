using System.Security.Cryptography;
using Godot;
using San14.Data;
using San14.Data.Formats;

public sealed record ModelMeshUvStream(int AttributeIndex, int BufferIndex,
    int BufferReferenceSlot, int ByteOffset, ushort TypeCode, ushort SemanticCode,
    byte UsageIndex, G1AttributeEncoding Encoding, int VertexStart, int VertexCount,
    Mesh.ArrayType? GodotArraySlot, IReadOnlyList<Vector2> Values,
    byte[] RawDescriptor, byte[] RawSourceBytes);

public sealed record ModelMeshSurface(int SurfaceIndex, int DrawIndex, int LayoutIndex,
    int PrimaryVertexBufferIndex, int IndexBufferIndex, int PaletteIndex,
    int VertexStart, int VertexCount, int IndexStart, int IndexCount,
    IReadOnlyList<int> SourceIndices, IReadOnlyList<int> PaletteEntryIndices, byte[] RawDrawRecord)
{
    public IReadOnlyList<ModelMeshUvStream> SourceUvStreams { get; init; } = [];
    /// <summary>Complete weighted bind-pose inputs. Ordinary meshes retain an empty list.</summary>
    public IReadOnlyList<IReadOnlyList<G1ArmyBindPoseInfluence>> SourceArmyBindPoseInfluences { get; init; } = [];
}

public sealed record ModelMeshResult(ArrayMesh Mesh, LoadedRdbAsset Source,
    IReadOnlyList<ModelMeshSurface> Surfaces);

/// <summary>
/// Adapts verified model-local static positions, original triangle lists and UV layers 0/1 to Godot.
/// No axis conversion, rescaling, generated normals, shader selection or original
/// material interpretation is performed. Call on the Godot main thread.
/// </summary>
public static class ModelMesh
{
    public static ModelMeshResult Build(LoadedModelTransforms loaded, bool usePreviewMaterial = false)
        => BuildCore(loaded, usePreviewMaterial, false);

    /// <summary>Explicit Army bind-pose preview. Uses the original palette,
    /// stored weights and implicit last weight; current animation is not inferred.</summary>
    public static ModelMeshResult BuildArmyBindPose(LoadedModelTransforms loaded, bool usePreviewMaterial = false)
        => BuildCore(loaded, usePreviewMaterial, true);

    private static ModelMeshResult BuildCore(LoadedModelTransforms loaded,
        bool usePreviewMaterial, bool allowArmyBindPose)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        string uri = loaded.Source.ResourceUri;
        var pending = new List<(Vector3[] Positions, int[] Indices, ModelMeshSurface Source)>();
        try
        {
            string actualHash = Convert.ToHexString(SHA256.HashData(loaded.Source.Bytes));
            if (!actualHash.Equals(loaded.Source.DecodedSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Loaded source bytes no longer match their recorded hash.");
            VerifyGeometry(G1ModelGeometryReader.Read(loaded.Source.Bytes, loaded.Source.Bytes.Length), loaded.Geometry);
            // Verify that the provided geometry still belongs to these exact source bytes.
            // The caller's parsed static transforms are reused below.
            G1ModelTransforms sourceTransforms = G1ModelTransformReader.Read(loaded.Source.Bytes, loaded.Geometry);
            VerifyTransforms(sourceTransforms, loaded.Transforms);
            G1ModelVertexData vertices = G1ModelVertexReader.Read(loaded.Geometry);
            G1ModelDrawBindings bindings = G1ModelDrawReader.Read(loaded.Geometry);
            if (bindings.Draws.Count == 0) throw new InvalidDataException("No source draws exist.");
            foreach (G1DrawRecord draw in bindings.Draws)
            {
                try
                {
                    G1TriangleList triangles = G1ModelTriangleReader.Read(loaded.Geometry, draw);
                    IReadOnlyList<ModelMeshUvStream> uvStreams = ReadUvStreams(
                        loaded.Geometry, draw, vertices.Layouts[draw.LayoutIndex]);
                    G1DecodedVertexLayout layout = vertices.Layouts[draw.LayoutIndex];
                    G1ArmyBindPoseDrawPositions? weighted = allowArmyBindPose &&
                        layout.Attributes.Any(a => a.UsageCode == 1 && a.Encoding is
                            G1AttributeEncoding.Float32x1 or G1AttributeEncoding.Float32x2 or G1AttributeEncoding.Float32x3)
                        ? G1ArmyBindPoseReader.TransformPositions(loaded.Transforms, draw, layout) : null;
                    G1RestPoseDrawPositions pose = weighted?.Positions ?? G1ModelTransformReader.TransformPositions(
                        loaded.Transforms, draw, layout);
                    if (pose.Positions.Count != draw.VertexCount || pose.VertexStart != draw.VertexStart ||
                        pose.DrawIndex != draw.Index || pose.PaletteEntryIndices.Count != draw.VertexCount)
                        throw new InvalidDataException("Static position result does not match the source draw window.");
                    if (draw.VertexCount == 0 || draw.IndexCount == 0)
                        throw new InvalidDataException("An empty draw cannot be represented by one indexed Godot triangle surface.");
                    var positions = new Vector3[draw.VertexCount];
                    for (int i = 0; i < positions.Length; i++)
                    {
                        System.Numerics.Vector3 value = pose.Positions[i];
                        positions[i] = new Vector3(value.X, value.Y, value.Z);
                    }
                    var sourceIndices = new int[draw.IndexCount];
                    var indices = new int[draw.IndexCount];
                    int next = 0;
                    foreach (G1IndexedTriangle triangle in triangles.Triangles)
                    {
                        sourceIndices[next++] = triangle.VertexIndex0;
                        sourceIndices[next++] = triangle.VertexIndex1;
                        sourceIndices[next++] = triangle.VertexIndex2;
                    }
                    if (next != draw.IndexCount) throw new InvalidDataException("Triangle indices do not match the source index count.");
                    for (int i = 0; i < indices.Length; i++)
                    {
                        indices[i] = sourceIndices[i] - draw.VertexStart;
                        if ((uint)indices[i] >= draw.VertexCount)
                            throw new InvalidDataException("Compact index falls outside the static position array.");
                    }
                    pending.Add((positions, indices, new ModelMeshSurface(pending.Count, draw.Index,
                        draw.LayoutIndex, draw.PrimaryVertexBufferIndex, draw.IndexBufferIndex, pose.PaletteIndex,
                        draw.VertexStart, draw.VertexCount, draw.IndexStart, draw.IndexCount,
                        Array.AsReadOnly(sourceIndices), Array.AsReadOnly(pose.PaletteEntryIndices.ToArray()),
                        draw.RawBytes.ToArray()) { SourceUvStreams = uvStreams,
                            SourceArmyBindPoseInfluences = weighted?.Influences ?? [] }));
                }
                catch (Exception error) when (error is InvalidDataException or ArgumentException or InvalidOperationException or OverflowException)
                { throw new InvalidDataException($"Original model mesh {uri}, draw {draw.Index}: {error.Message}", error); }
            }
        }
        catch (Exception error) when (error is InvalidDataException or ArgumentException or InvalidOperationException or OverflowException)
        { throw new InvalidDataException($"Original model mesh source {uri}: {error.Message}", error); }

        var mesh = new ArrayMesh { ResourceName = $"OriginalStaticGeometry_{loaded.Source.FileId:X8}" };
        StandardMaterial3D? preview = null;
        try
        {
            if (usePreviewMaterial) preview = CreatePreviewMaterial();
            var surfaceMetadata = new Godot.Collections.Array<Godot.Collections.Dictionary>();
            foreach (var item in pending)
            {
                try
                {
                    using Godot.Collections.Array arrays = [];
                    arrays.Resize((int)Mesh.ArrayType.Max);
                    arrays[(int)Mesh.ArrayType.Vertex] = item.Positions;
                    arrays[(int)Mesh.ArrayType.Index] = item.Indices;
                    foreach (ModelMeshUvStream uv in item.Source.SourceUvStreams)
                        if (uv.GodotArraySlot is Mesh.ArrayType slot)
                            arrays[(int)slot] = uv.Values.ToArray();
                    mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays, flags: (Mesh.ArrayFormat)0);
                    int surface = item.Source.SurfaceIndex;
                    if (mesh.GetSurfaceCount() != surface + 1 || mesh.SurfaceGetArrayLen(surface) != item.Positions.Length ||
                        mesh.SurfaceGetArrayIndexLen(surface) != item.Indices.Length)
                        throw new InvalidDataException("Godot did not retain the complete source surface.");
                    mesh.SurfaceSetName(surface, $"OriginalDraw_{item.Source.DrawIndex}");
                    if (preview is not null) mesh.SurfaceSetMaterial(surface, preview);
                    var uvMetadata = new Godot.Collections.Array<Godot.Collections.Dictionary>();
                    foreach (ModelMeshUvStream uv in item.Source.SourceUvStreams)
                        uvMetadata.Add(new Godot.Collections.Dictionary
                        {
                            ["attribute_index"] = uv.AttributeIndex, ["buffer_index"] = uv.BufferIndex,
                            ["buffer_reference_slot"] = uv.BufferReferenceSlot, ["byte_offset"] = uv.ByteOffset,
                            ["type_code"] = uv.TypeCode, ["semantic_code"] = uv.SemanticCode,
                            ["usage_code"] = 5, ["usage_index"] = uv.UsageIndex,
                            ["encoding"] = uv.Encoding.ToString(), ["vertex_start"] = uv.VertexStart,
                            ["vertex_count"] = uv.VertexCount,
                            ["godot_array_slot"] = uv.GodotArraySlot is Mesh.ArrayType slot ? (int)slot : -1,
                            ["adapted"] = uv.GodotArraySlot is not null,
                            ["raw_descriptor"] = uv.RawDescriptor, ["raw_source_bytes"] = uv.RawSourceBytes,
                            ["source_values"] = uv.Values.ToArray()
                        });
                    surfaceMetadata.Add(new Godot.Collections.Dictionary
                    {
                        ["surface_index"] = surface, ["draw_index"] = item.Source.DrawIndex,
                        ["layout_index"] = item.Source.LayoutIndex,
                        ["primary_vertex_buffer_index"] = item.Source.PrimaryVertexBufferIndex,
                        ["index_buffer_index"] = item.Source.IndexBufferIndex, ["palette_index"] = item.Source.PaletteIndex,
                        ["vertex_start"] = item.Source.VertexStart, ["vertex_count"] = item.Source.VertexCount,
                        ["index_start"] = item.Source.IndexStart, ["index_count"] = item.Source.IndexCount,
                        ["source_indices"] = item.Source.SourceIndices.ToArray(),
                        ["palette_entry_indices"] = item.Source.PaletteEntryIndices.ToArray(),
                        ["palette_entry_indices_are_first_influence_only"] = item.Source.SourceArmyBindPoseInfluences.Count != 0,
                        ["source_skin_influence_count"] = item.Source.SourceArmyBindPoseInfluences.Count == 0 ? 0 : item.Source.SourceArmyBindPoseInfluences[0].Count,
                        ["source_skin_register_offsets"] = item.Source.SourceArmyBindPoseInfluences.SelectMany(row => row).Select(value => value.RegisterOffset).ToArray(),
                        ["source_skin_palette_entry_indices"] = item.Source.SourceArmyBindPoseInfluences.SelectMany(row => row).Select(value => value.PaletteEntryIndex).ToArray(),
                        ["source_skin_weights"] = item.Source.SourceArmyBindPoseInfluences.SelectMany(row => row).Select(value => value.Weight).ToArray(),
                        ["source_skin_implicit_last"] = item.Source.SourceArmyBindPoseInfluences.SelectMany(row => row).Select(value => value.ImplicitLastWeight ? 1 : 0).ToArray(),
                        ["raw_draw_record"] = item.Source.RawDrawRecord,
                        ["source_uv_streams"] = uvMetadata,
                        ["unadapted_uv_usage_indices"] = item.Source.SourceUvStreams
                            .Where(uv => uv.GodotArraySlot is null).Select(uv => (int)uv.UsageIndex).ToArray()
                    });
                }
                catch (Exception error) when (error is InvalidDataException or ArgumentException or InvalidOperationException or OverflowException)
                { throw new InvalidDataException($"Original model mesh {uri}, draw {item.Source.DrawIndex}: {error.Message}", error); }
            }
            mesh.SetMeta("source_resource_uri", uri);
            mesh.SetMeta("source_decoded_sha256", loaded.Source.DecodedSha256);
            mesh.SetMeta("source_file_id", loaded.Source.FileId);
            mesh.SetMeta("source_is_override", loaded.Source.OverrideSource is not null);
            mesh.SetMeta("source_surfaces", surfaceMetadata);
            mesh.SetMeta("positions_space", "original model-local static pose");
            mesh.SetMeta("source_army_weighted_bind_pose", pending.Any(item => item.Source.SourceArmyBindPoseInfluences.Count != 0));
            mesh.SetMeta("original_current_animation_verified", false);
            mesh.SetMeta("axis_conversion_applied", false);
            mesh.SetMeta("normals_generated", false);
            mesh.SetMeta("original_materials_resolved", false);
            mesh.SetMeta("preview_material_assigned", usePreviewMaterial);
            mesh.SetMeta("uv_flip_applied", false);
            mesh.SetMeta("uv_clamp_applied", false);
            mesh.SetMeta("uv_rescale_applied", false);
            mesh.SetMeta("uv_layers_adapted", pending.SelectMany(item => item.Source.SourceUvStreams)
                .Where(uv => uv.GodotArraySlot is not null).Select(uv => (int)uv.UsageIndex).Distinct().Order().ToArray());
            return new(mesh, loaded.Source, pending.Select(item => item.Source).ToArray());
        }
        catch
        {
            mesh.Dispose();
            preview?.Dispose();
            throw;
        }
    }

    public static StandardMaterial3D CreatePreviewMaterial() => new()
    {
        ResourceName = "NeutralStaticGeometryPreview",
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        AlbedoColor = new Color(0.7f, 0.7f, 0.7f, 1)
    };

    private static IReadOnlyList<ModelMeshUvStream> ReadUvStreams(G1ModelGeometry geometry,
        G1DrawRecord draw, G1DecodedVertexLayout layout)
    {
        G1DecodedAttribute[] attributes = layout.Attributes.Where(attribute => attribute.UsageCode == 5).ToArray();
        if (attributes.GroupBy(attribute => attribute.UsageIndex).Any(group => group.Count() != 1))
            throw new InvalidDataException("UV usage/index is not unique in the source layout.");
        var result = new List<ModelMeshUvStream>(attributes.Length);
        foreach (G1DecodedAttribute attribute in attributes)
        {
            if (attribute.ComponentCount != 2 || attribute.Encoding is not (G1AttributeEncoding.Float32x2 or G1AttributeEncoding.Float16x2))
                throw new InvalidDataException($"UV layer {attribute.UsageIndex} does not use verified two-component storage.");
            // Equal buffer counts alone do not establish a common index space.
            // All supported source UVs explicitly refer to the primary position buffer.
            if (attribute.BufferIndex != draw.PrimaryVertexBufferIndex)
                throw new InvalidDataException($"UV layer {attribute.UsageIndex} refers to a different vertex buffer; its index-space correspondence is unverified.");
            if (draw.VertexStart < 0 || draw.VertexCount < 0 ||
                (long)draw.VertexStart + draw.VertexCount > attribute.ElementCount)
                throw new InvalidDataException($"UV layer {attribute.UsageIndex} does not cover the source draw vertex window.");
            G1VertexBuffer buffer = geometry.VertexBuffers[attribute.BufferIndex];
            int encodedWidth = attribute.Encoding == G1AttributeEncoding.Float32x2 ? 8 : 4;
            var raw = new byte[checked(draw.VertexCount * encodedWidth)];
            var values = new Vector2[draw.VertexCount];
            for (int i = 0; i < draw.VertexCount; i++)
            {
                int sourceIndex = checked(draw.VertexStart + i);
                ReadOnlySpan<float> value = attribute.GetElement(sourceIndex);
                values[i] = new Vector2(value[0], value[1]);
                buffer.Data.AsSpan(checked(sourceIndex * buffer.Stride + attribute.Source.ByteOffset), encodedWidth)
                    .CopyTo(raw.AsSpan(i * encodedWidth, encodedWidth));
            }
            Mesh.ArrayType? slot = attribute.UsageIndex switch
            {
                0 => Mesh.ArrayType.TexUV,
                1 => Mesh.ArrayType.TexUV2,
                _ => null
            };
            result.Add(new(attribute.Index, attribute.BufferIndex, attribute.Source.BufferReferenceSlot,
                attribute.Source.ByteOffset, attribute.Source.TypeCode, attribute.Source.SemanticCode,
                attribute.UsageIndex, attribute.Encoding, draw.VertexStart, draw.VertexCount, slot,
                Array.AsReadOnly(values), attribute.Source.RawBytes.ToArray(), raw));
        }
        return result.AsReadOnly();
    }

    private static void VerifyTransforms(G1ModelTransforms source, G1ModelTransforms supplied)
    {
        // Derived matrices are source-dependent too: a synthetic Loaded* record may
        // contain valid geometry but transforms from another model or altered pose.
        if (!source.RawSkeletonHeader.SequenceEqual(supplied.RawSkeletonHeader) ||
            !source.RawMatrixHeader.SequenceEqual(supplied.RawMatrixHeader) ||
            source.JointDataOffset != supplied.JointDataOffset || source.UnknownValue16 != supplied.UnknownValue16 ||
            source.RawValue24 != supplied.RawValue24 || source.RawValue26 != supplied.RawValue26 ||
            !source.RawJointMap.SequenceEqual(supplied.RawJointMap) ||
            !source.UnknownBeforeJoints.SequenceEqual(supplied.UnknownBeforeJoints) ||
            source.Joints.Count != supplied.Joints.Count || source.ReferenceMatrices.Count != supplied.ReferenceMatrices.Count ||
            source.JointPalettes.Count != supplied.JointPalettes.Count ||
            !source.JointModelMatrices.SequenceEqual(supplied.JointModelMatrices))
            throw new InvalidDataException("Provided static transforms differ from the recorded model source.");
        for (int i = 0; i < source.Joints.Count; i++)
            if (source.Joints[i].Index != supplied.Joints[i].Index || source.Joints[i].Scale != supplied.Joints[i].Scale ||
                source.Joints[i].ParentIndex != supplied.Joints[i].ParentIndex || source.Joints[i].Rotation != supplied.Joints[i].Rotation ||
                source.Joints[i].Position != supplied.Joints[i].Position ||
                !source.Joints[i].RawBytes.SequenceEqual(supplied.Joints[i].RawBytes))
                throw new InvalidDataException("Provided joint record differs from the recorded model source.");
        for (int i = 0; i < source.ReferenceMatrices.Count; i++)
            if (source.ReferenceMatrices[i].Index != supplied.ReferenceMatrices[i].Index ||
                !source.ReferenceMatrices[i].RawBytes.SequenceEqual(supplied.ReferenceMatrices[i].RawBytes) ||
                source.ReferenceMatrices[i].Matrix != supplied.ReferenceMatrices[i].Matrix)
                throw new InvalidDataException("Provided reference matrix differs from the recorded model source.");
        for (int i = 0; i < source.JointPalettes.Count; i++)
        {
            var expected = source.JointPalettes[i].Entries; var actual = supplied.JointPalettes[i].Entries;
            if (source.JointPalettes[i].Index != supplied.JointPalettes[i].Index ||
                !source.JointPalettes[i].RawCount.SequenceEqual(supplied.JointPalettes[i].RawCount) || expected.Count != actual.Count)
                throw new InvalidDataException("Provided palette differs from the recorded model source.");
            for (int j = 0; j < expected.Count; j++)
                if (expected[j].Index != actual[j].Index || expected[j].MatrixIndex != actual[j].MatrixIndex ||
                    expected[j].JointIndex != actual[j].JointIndex || expected[j].RawValue4 != actual[j].RawValue4 ||
                    !expected[j].RawBytes.SequenceEqual(actual[j].RawBytes))
                    throw new InvalidDataException("Provided palette entry differs from the recorded model source.");
        }
    }

    private static void VerifyGeometry(G1ModelGeometry source, G1ModelGeometry supplied)
    {
        // Geometry records expose mutable byte arrays. Matching RawSection alone
        // does not prove that a separately replaced buffer/layout still has that source.
        if (source.Sections.Count != supplied.Sections.Count || source.VertexBuffers.Count != supplied.VertexBuffers.Count ||
            source.IndexBuffers.Count != supplied.IndexBuffers.Count || source.Layouts.Count != supplied.Layouts.Count)
            throw new InvalidDataException("Provided geometry tables differ from the recorded model source.");
        for (int i = 0; i < source.Sections.Count; i++)
        {
            G1GeometrySection expected = source.Sections[i], actual = supplied.Sections[i];
            if (expected.Index != actual.Index || expected.Id != actual.Id || expected.Offset != actual.Offset ||
                expected.Length != actual.Length || expected.ItemCount != actual.ItemCount || !expected.RawBytes.SequenceEqual(actual.RawBytes))
                throw new InvalidDataException("Provided geometry section differs from the recorded model source.");
        }
        for (int i = 0; i < source.VertexBuffers.Count; i++)
        {
            G1VertexBuffer expected = source.VertexBuffers[i], actual = supplied.VertexBuffers[i];
            if (expected.Index != actual.Index || expected.Stride != actual.Stride || expected.VertexCount != actual.VertexCount ||
                expected.UnknownValue0 != actual.UnknownValue0 || expected.UnknownValue12 != actual.UnknownValue12 ||
                !expected.RawHeader.SequenceEqual(actual.RawHeader) || !expected.Data.SequenceEqual(actual.Data))
                throw new InvalidDataException("Provided vertex buffer differs from the recorded model source.");
        }
        for (int i = 0; i < source.IndexBuffers.Count; i++)
        {
            G1IndexBuffer expected = source.IndexBuffers[i], actual = supplied.IndexBuffers[i];
            if (expected.Index != actual.Index || expected.IndexCount != actual.IndexCount || expected.BitsPerIndex != actual.BitsPerIndex ||
                expected.UnknownValue8 != actual.UnknownValue8 || !expected.RawHeader.SequenceEqual(actual.RawHeader) ||
                !expected.Data.SequenceEqual(actual.Data) || !expected.Padding.SequenceEqual(actual.Padding))
                throw new InvalidDataException("Provided index buffer differs from the recorded model source.");
        }
        for (int i = 0; i < source.Layouts.Count; i++)
        {
            G1VertexLayout expected = source.Layouts[i], actual = supplied.Layouts[i];
            if (expected.Index != actual.Index || !expected.RawBytes.SequenceEqual(actual.RawBytes) ||
                !expected.BufferReferences.SequenceEqual(actual.BufferReferences) || expected.Attributes.Count != actual.Attributes.Count)
                throw new InvalidDataException("Provided vertex layout differs from the recorded model source.");
            for (int j = 0; j < expected.Attributes.Count; j++)
            {
                G1VertexAttribute a = expected.Attributes[j], b = actual.Attributes[j];
                if (a.BufferReferenceSlot != b.BufferReferenceSlot || a.ByteOffset != b.ByteOffset || a.TypeCode != b.TypeCode ||
                    a.SemanticCode != b.SemanticCode || !a.RawBytes.SequenceEqual(b.RawBytes))
                    throw new InvalidDataException("Provided vertex attribute differs from the recorded model source.");
            }
        }
    }
}
