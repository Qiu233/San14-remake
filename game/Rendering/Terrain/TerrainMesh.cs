using Godot;
using San14.Data;
using San14.Data.Formats;

public sealed record TerrainPatch(ArrayMesh Mesh, SpatialTreeNode SourceNode,
    int ColumnOrigin, int RowOrigin, int SampleStep, IReadOnlyList<uint> PackedSamples,
    TerrainTriangleGeometry Geometry);

public sealed class TerrainMeshResult : IDisposable
{
    public LoadedTerrainResources Source { get; }
    public IReadOnlyList<TerrainPatch> Patches { get; }
    public int TreeDepth { get; }
    /// <summary>Explicit draw selection can mix levels or contain a visibility-filtered subset.</summary>
    public bool HasExplicitSelection => TreeDepth < 0;
    public ShaderMaterial PreviewMaterial { get; }
    private readonly Shader _shader;
    private bool _disposed;
    internal TerrainMeshResult(LoadedTerrainResources source, IReadOnlyList<TerrainPatch> patches,
        int depth, ShaderMaterial material, Shader shader)
        => (Source, Patches, TreeDepth, PreviewMaterial, _shader) = (source, patches, depth, material, shader);
    public void Dispose()
    {
        if (_disposed) return;
        foreach (TerrainPatch patch in Patches) patch.Mesh.Dispose();
        PreviewMaterial.Dispose(); _shader.Dispose(); _disposed = true;
    }
}

/// <summary>Uploads CPU-prepared terrain patches. Height sampling, source
/// selection and vertex attributes are independent of Godot device objects.</summary>
public static class TerrainMesh
{
    public static TerrainMeshResult Build(LoadedTerrainResources loaded, int treeDepth = 2)
        => Upload(TerrainMeshPreparation.Prepare(loaded, null, treeDepth));

    public static TerrainMeshResult Build(LoadedTerrainResources loaded,
        LoadedTerrainInitialHeightResources initialHeight, int treeDepth = 2)
        => Upload(TerrainMeshPreparation.Prepare(loaded,
            initialHeight ?? throw new ArgumentNullException(nameof(initialHeight)), treeDepth));

    public static TerrainMeshResult Build(LoadedTerrainResources loaded,
        LoadedTerrainInitialHeightResources initialHeight, IReadOnlyList<TerrainLodPatchSelection> selection)
        => Upload(TerrainMeshPreparation.Prepare(loaded,
            initialHeight ?? throw new ArgumentNullException(nameof(initialHeight)),
            TerrainMeshSelection.Explicit(loaded.SpatialTree, selection)));

    internal static TerrainMeshResult Upload(PreparedTerrainMesh prepared)
    {
        LoadedTerrainResources loaded = prepared.Source;
        LoadedTerrainInitialHeightResources? initialHeight = prepared.InitialHeight;
        MhkHeightField height = loaded.HeightField.HeightField;
        DokProperty rootPosition = prepared.RootPosition, fieldPosition = prepared.FieldPosition;
        int n = prepared.GridSize;
        int[] indices = prepared.Indices.ToArray();
        var patches = new List<TerrainPatch>();
        Shader? shader = null;
        ShaderMaterial? material = null;
        string context = loaded.Objects.Source.ResourceUri;
        try
        {
            shader = new Shader
            {
                ResourceName = "SourceHeightGrayscalePreview",
                Code = """
                    shader_type spatial;
                    render_mode unshaded, cull_disabled;
                    uniform float source_minimum;
                    uniform float source_maximum;
                    varying float source_height;
                    void vertex() { source_height = VERTEX.y; }
                    void fragment() {
                        float h = clamp((source_height-source_minimum)/max(source_maximum-source_minimum, 0.00001), 0.0, 1.0);
                        ALBEDO = vec3(mix(0.25, 0.65, h));
                    }
                    """
            };
            material = new ShaderMaterial { Shader = shader, ResourceName = "TerrainHeightPreview" };
            material.SetShaderParameter("source_minimum", height.MinimumHeight);
            material.SetShaderParameter("source_maximum", height.MaximumHeight);
            material.SetMeta("original_materials_resolved", false);
            foreach (PreparedTerrainMeshPatch patch in prepared.Patches)
            {
                SpatialTreeNode node = patch.Node;
                TerrainPatchInput? input = patch.Input;
                int originColumn = patch.ColumnOrigin, originRow = patch.RowOrigin, step = patch.SampleStep;
                Vector3[] vertices = ToGodot(patch.Positions), normals = ToGodot(patch.Normals);
                Vector2[]? uvs = input is null ? null : ToGodot(patch.Uvs);
                Vector2[]? phases = input is null ? null : ToGodot(patch.Phases);
                float[]? shaderPatchInputs = input is null ? null : patch.ShaderPatchInputs.ToArray();
                float[]? tangents = input is null ? null : patch.Tangents.ToArray();
                uint[] packed = patch.PackedSamples.ToArray();
                var mesh = new ArrayMesh { ResourceName = $"SourceTerrain_{loaded.HeightField.Source.FileId:X8}_Node{node.Index}" };
                patches.Add(new(mesh, node, originColumn, originRow, step, Array.AsReadOnly(packed), patch.Geometry));
                using Godot.Collections.Array arrays = [];
                arrays.Resize((int)Mesh.ArrayType.Max);
                arrays[(int)Mesh.ArrayType.Vertex] = vertices;
                arrays[(int)Mesh.ArrayType.Normal] = normals;
                if (uvs is not null) arrays[(int)Mesh.ArrayType.TexUV] = uvs;
                if (phases is not null) arrays[(int)Mesh.ArrayType.TexUV2] = phases;
                if (shaderPatchInputs is not null) arrays[(int)Mesh.ArrayType.Custom0] = shaderPatchInputs;
                if (tangents is not null) arrays[(int)Mesh.ArrayType.Tangent] = tangents;
                arrays[(int)Mesh.ArrayType.Index] = indices;
                Mesh.ArrayFormat customFormat = shaderPatchInputs is null ? (Mesh.ArrayFormat)0 :
                    (Mesh.ArrayFormat)((ulong)Mesh.ArrayCustomFormat.RgbFloat << (int)Mesh.ArrayFormat.FormatCustom0Shift);
                mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays, flags: customFormat);
                if (mesh.GetSurfaceCount() != 1 || mesh.SurfaceGetArrayLen(0) != vertices.Length ||
                    mesh.SurfaceGetArrayIndexLen(0) != indices.Length)
                    throw new InvalidDataException($"Node {node.Index} native mesh did not retain the source sample grid.");
                mesh.SetMeta("bundle_resource_uri", loaded.Objects.Source.ResourceUri);
                mesh.SetMeta("bundle_decoded_sha256", loaded.Objects.Source.DecodedSha256);
                mesh.SetMeta("height_resource_uri", loaded.HeightField.Source.ResourceUri);
                mesh.SetMeta("height_decoded_sha256", loaded.HeightField.Source.DecodedSha256);
                mesh.SetMeta("spatial_resource_uri", loaded.SpatialSource.ResourceUri);
                mesh.SetMeta("spatial_decoded_sha256", loaded.SpatialSource.DecodedSha256);
                mesh.SetMeta("source_tree_node", node.Index);
                mesh.SetMeta("source_tree_node_bytes", node.RawBytes);
                mesh.SetMeta("source_explicit_lod_selection", prepared.TreeDepth < 0);
                mesh.SetMeta("source_grid_origin", new Vector2I(originColumn, originRow));
                mesh.SetMeta("source_sample_step", step);
                mesh.SetMeta("preview_normals", initialHeight is null ?
                    "MHK centered LOD differences; legacy preview adapter" : "source VS1 point/clamp height kernel; texture-resolution delta; initial zero brush only");
                mesh.SetMeta("source_patch_grid_size", n);
                mesh.SetMeta("original_initial_height_adapter", initialHeight is not null);
                if (input is not null && initialHeight is not null)
                {
                    mesh.SetMeta("source_patch_instance_bytes", input.EncodeInstance());
                    mesh.SetMeta("source_patch_edge_flags", input.EdgeFlags);
                    mesh.SetMeta("source_patch_shader_frequency_count", new Vector2(input.ControlFrequency, input.ControlCount));
                    mesh.SetMeta("source_patch_material_class", node.Level == 0 ? "detail" : "coarse");
                    mesh.SetMeta("height_texture_resource_uri", initialHeight.HeightBinding.Resource.Source.ResourceUri);
                    mesh.SetMeta("height_texture_decoded_sha256", initialHeight.HeightBinding.Resource.Source.DecodedSha256);
                    mesh.SetMeta("height_generator_resource_uri", initialHeight.Generator.Source.ResourceUri);
                    mesh.SetMeta("height_generator_decoded_sha256", initialHeight.Generator.Source.DecodedSha256);
                    mesh.SetMeta("source_height_normal_info", new Vector2(initialHeight.HeightNormalInfo.X, initialHeight.HeightNormalInfo.Y));
                }
                mesh.SetMeta("source_sample_spacing", height.SampleSpacing);
                mesh.SetMeta("source_packed_samples", packed.Select(value => (long)value).ToArray());
                mesh.SetMeta("source_mhk_header", height.RawHeader);
                mesh.SetMeta("root_position_property", PropertyMetadata(rootPosition));
                mesh.SetMeta("field_position_property", PropertyMetadata(fieldPosition));
                mesh.SetMeta("source_is_override", loaded.Objects.Source.OverrideSource is not null ||
                    loaded.HeightField.Source.OverrideSource is not null || loaded.SpatialSource.OverrideSource is not null);
                mesh.SetMeta("original_world_mapping_verified", false);
                mesh.SetMeta("original_topology_verified", true);
                mesh.SetMeta("original_lod_selection_verified", false);
                mesh.SetMeta("original_materials_resolved", false);
                mesh.SetMeta("height_flags_interpreted", false);
                mesh.SetMeta("preview_convention", prepared.TreeDepth >= 0 ?
                    "column +X; row +Z; uniform source LOD; original checkerboard diagonal; unlit material preview" :
                    "explicit source nodes and stitch flags; original checkerboard diagonal; selection point/visibility owned by caller; unlit material preview");
            }
            return new(loaded, patches.AsReadOnly(), prepared.TreeDepth, material, shader);
        }
        catch (Exception error)
        {
            foreach (TerrainPatch patch in patches) patch.Mesh.Dispose();
            material?.Dispose(); shader?.Dispose();
            throw new InvalidDataException($"Original terrain mesh {context}: {error.Message}", error);
        }
    }

    private static Vector3[] ToGodot(ReadOnlyMemory<System.Numerics.Vector3> values)
    {
        var result = new Vector3[values.Length];
        for (int i = 0; i < result.Length; i++)
        {
            var value = values.Span[i];
            result[i] = new(value.X, value.Y, value.Z);
        }
        return result;
    }

    private static Vector2[] ToGodot(ReadOnlyMemory<System.Numerics.Vector2> values)
    {
        var result = new Vector2[values.Length];
        for (int i = 0; i < result.Length; i++)
        {
            var value = values.Span[i];
            result[i] = new(value.X, value.Y);
        }
        return result;
    }

    private static Godot.Collections.Dictionary PropertyMetadata(DokProperty value) => new()
    {
        ["name_hash"] = value.NameHash, ["descriptor_offset"] = value.DescriptorOffset,
        ["value_offset"] = value.ValueOffset, ["raw_descriptor"] = value.RawDescriptor, ["raw_value"] = value.RawValue
    };
}
