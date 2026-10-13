using Godot;
using San14.Data;

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
        PreparedModelMesh prepared = ModelMeshPreparation.Prepare(loaded, allowArmyBindPose);
        string uri = prepared.Source.ResourceUri;
        IReadOnlyList<ModelMeshSurface> pending = prepared.Surfaces;
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
                    arrays[(int)Mesh.ArrayType.Vertex] = item.Positions.Select(value => new Vector3(value.X, value.Y, value.Z)).ToArray();
                    arrays[(int)Mesh.ArrayType.Index] = item.Indices.ToArray();
                    foreach (ModelMeshUvStream uv in item.SourceUvStreams)
                        if (UvArraySlot(uv.UsageIndex) is Mesh.ArrayType slot)
                            arrays[(int)slot] = uv.Values.Select(value => new Vector2(value.X, value.Y)).ToArray();
                    mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays, flags: (Mesh.ArrayFormat)0);
                    int surface = item.SurfaceIndex;
                    if (mesh.GetSurfaceCount() != surface + 1 || mesh.SurfaceGetArrayLen(surface) != item.Positions.Count ||
                        mesh.SurfaceGetArrayIndexLen(surface) != item.Indices.Count)
                        throw new InvalidDataException("Godot did not retain the complete source surface.");
                    mesh.SurfaceSetName(surface, $"OriginalDraw_{item.DrawIndex}");
                    if (preview is not null) mesh.SurfaceSetMaterial(surface, preview);
                    var uvMetadata = new Godot.Collections.Array<Godot.Collections.Dictionary>();
                    foreach (ModelMeshUvStream uv in item.SourceUvStreams)
                        uvMetadata.Add(new Godot.Collections.Dictionary
                        {
                            ["attribute_index"] = uv.AttributeIndex, ["buffer_index"] = uv.BufferIndex,
                            ["buffer_reference_slot"] = uv.BufferReferenceSlot, ["byte_offset"] = uv.ByteOffset,
                            ["type_code"] = uv.TypeCode, ["semantic_code"] = uv.SemanticCode,
                            ["usage_code"] = 5, ["usage_index"] = uv.UsageIndex,
                            ["encoding"] = uv.Encoding.ToString(), ["vertex_start"] = uv.VertexStart,
                            ["vertex_count"] = uv.VertexCount,
                            ["godot_array_slot"] = UvArraySlot(uv.UsageIndex) is Mesh.ArrayType slot ? (int)slot : -1,
                            ["adapted"] = UvArraySlot(uv.UsageIndex) is not null,
                            ["raw_descriptor"] = uv.RawDescriptor, ["raw_source_bytes"] = uv.RawSourceBytes,
                            ["source_values"] = uv.Values.Select(value => new Vector2(value.X, value.Y)).ToArray()
                        });
                    surfaceMetadata.Add(new Godot.Collections.Dictionary
                    {
                        ["surface_index"] = surface, ["draw_index"] = item.DrawIndex,
                        ["layout_index"] = item.LayoutIndex,
                        ["primary_vertex_buffer_index"] = item.PrimaryVertexBufferIndex,
                        ["index_buffer_index"] = item.IndexBufferIndex, ["palette_index"] = item.PaletteIndex,
                        ["vertex_start"] = item.VertexStart, ["vertex_count"] = item.VertexCount,
                        ["index_start"] = item.IndexStart, ["index_count"] = item.IndexCount,
                        ["source_indices"] = item.SourceIndices.ToArray(),
                        ["palette_entry_indices"] = item.PaletteEntryIndices.ToArray(),
                        ["palette_entry_indices_are_first_influence_only"] = item.SourceArmyBindPoseInfluences.Count != 0,
                        ["source_skin_influence_count"] = item.SourceArmyBindPoseInfluences.Count == 0 ? 0 : item.SourceArmyBindPoseInfluences[0].Count,
                        ["source_skin_register_offsets"] = item.SourceArmyBindPoseInfluences.SelectMany(row => row).Select(value => value.RegisterOffset).ToArray(),
                        ["source_skin_palette_entry_indices"] = item.SourceArmyBindPoseInfluences.SelectMany(row => row).Select(value => value.PaletteEntryIndex).ToArray(),
                        ["source_skin_weights"] = item.SourceArmyBindPoseInfluences.SelectMany(row => row).Select(value => value.Weight).ToArray(),
                        ["source_skin_implicit_last"] = item.SourceArmyBindPoseInfluences.SelectMany(row => row).Select(value => value.ImplicitLastWeight ? 1 : 0).ToArray(),
                        ["raw_draw_record"] = item.RawDrawRecord,
                        ["source_uv_streams"] = uvMetadata,
                        ["unadapted_uv_usage_indices"] = item.SourceUvStreams
                            .Where(uv => UvArraySlot(uv.UsageIndex) is null).Select(uv => (int)uv.UsageIndex).ToArray()
                    });
                }
                catch (Exception error) when (error is InvalidDataException or ArgumentException or InvalidOperationException or OverflowException)
                { throw new InvalidDataException($"Original model mesh {uri}, draw {item.DrawIndex}: {error.Message}", error); }
            }
            mesh.SetMeta("source_resource_uri", uri);
            mesh.SetMeta("source_decoded_sha256", loaded.Source.DecodedSha256);
            mesh.SetMeta("source_file_id", loaded.Source.FileId);
            mesh.SetMeta("source_is_override", loaded.Source.OverrideSource is not null);
            mesh.SetMeta("source_surfaces", surfaceMetadata);
            mesh.SetMeta("positions_space", "original model-local static pose");
            mesh.SetMeta("source_army_weighted_bind_pose", pending.Any(item => item.SourceArmyBindPoseInfluences.Count != 0));
            mesh.SetMeta("original_current_animation_verified", false);
            mesh.SetMeta("axis_conversion_applied", false);
            mesh.SetMeta("normals_generated", false);
            mesh.SetMeta("original_materials_resolved", false);
            mesh.SetMeta("preview_material_assigned", usePreviewMaterial);
            mesh.SetMeta("uv_flip_applied", false);
            mesh.SetMeta("uv_clamp_applied", false);
            mesh.SetMeta("uv_rescale_applied", false);
            mesh.SetMeta("uv_layers_adapted", pending.SelectMany(item => item.SourceUvStreams)
                .Where(uv => UvArraySlot(uv.UsageIndex) is not null).Select(uv => (int)uv.UsageIndex).Distinct().Order().ToArray());
            return new(mesh, loaded.Source, pending);
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

    private static Mesh.ArrayType? UvArraySlot(byte usageIndex) => usageIndex switch
    {
        0 => Mesh.ArrayType.TexUV,
        1 => Mesh.ArrayType.TexUV2,
        _ => null
    };
}
