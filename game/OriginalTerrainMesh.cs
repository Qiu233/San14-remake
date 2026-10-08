using System.Buffers.Binary;
using System.Security.Cryptography;
using Godot;
using San14.Data;
using San14.Data.Formats;

public sealed record OriginalTerrainPatch(ArrayMesh Mesh, SpatialTreeNode SourceNode,
    int ColumnOrigin, int RowOrigin, int SampleStep, IReadOnlyList<uint> PackedSamples);

public sealed class OriginalTerrainMeshResult : IDisposable
{
    public LoadedTerrainResources Source { get; }
    public IReadOnlyList<OriginalTerrainPatch> Patches { get; }
    public int TreeDepth { get; }
    /// <summary>Explicit draw selection can mix levels or contain a visibility-filtered subset.</summary>
    public bool HasExplicitSelection => TreeDepth < 0;
    public ShaderMaterial PreviewMaterial { get; }
    private readonly Shader _shader;
    private bool _disposed;
    internal OriginalTerrainMeshResult(LoadedTerrainResources source, IReadOnlyList<OriginalTerrainPatch> patches,
        int depth, ShaderMaterial material, Shader shader)
        => (Source, Patches, TreeDepth, PreviewMaterial, _shader) = (source, patches, depth, material, shader);
    public void Dispose()
    {
        if (_disposed) return;
        foreach (OriginalTerrainPatch patch in Patches) patch.Mesh.Dispose();
        PreviewMaterial.Dispose(); _shader.Dispose(); _disposed = true;
    }
}

/// <summary>
/// Displays source patch topology. The explicit initial-height overload uses the
/// original ordinary instance formula and VS1 normal kernel; uniform LOD remains a preview choice.
/// </summary>
public static class OriginalTerrainMesh
{
    public static OriginalTerrainMeshResult Build(LoadedTerrainResources loaded, int treeDepth = 2)
        => BuildCore(loaded, null, treeDepth);

    public static OriginalTerrainMeshResult Build(LoadedTerrainResources loaded,
        LoadedTerrainInitialHeightResources initialHeight, int treeDepth = 2)
        => BuildCore(loaded, initialHeight ?? throw new ArgumentNullException(nameof(initialHeight)), treeDepth);

    public static OriginalTerrainMeshResult Build(LoadedTerrainResources loaded,
        LoadedTerrainInitialHeightResources initialHeight, IReadOnlyList<TerrainLodPatchSelection> selection)
        => BuildCore(loaded, initialHeight ?? throw new ArgumentNullException(nameof(initialHeight)), -1,
            selection ?? throw new ArgumentNullException(nameof(selection)));

    private static OriginalTerrainMeshResult BuildCore(LoadedTerrainResources loaded,
        LoadedTerrainInitialHeightResources? initialHeight, int treeDepth,
        IReadOnlyList<TerrainLodPatchSelection>? explicitSelection = null)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        var patches = new List<OriginalTerrainPatch>();
        Shader? shader = null;
        ShaderMaterial? material = null;
        string context = loaded.Objects.Source.ResourceUri;
        try
        {
            (MhkHeightField height, SpatialTreeDocument tree, DokProperty rootPosition, DokProperty fieldPosition) = Verify(loaded);
            if (!float.IsFinite(height.MaximumHeight - height.MinimumHeight))
                throw new InvalidDataException("Height range exceeds finite float arithmetic in the grayscale preview material.");
            if (explicitSelection is null && (uint)treeDepth >= tree.LevelCount)
                throw new InvalidDataException($"Preview tree depth {treeDepth} is outside the source levels.");
            if (initialHeight is not null && initialHeight.Terrain != loaded)
                throw new InvalidDataException("Initial height belongs to a different terrain source.");
            TerrainLodPatchSelection[] selected;
            if (explicitSelection is null)
            {
                int side = 1 << treeDepth;
                selected = tree.Nodes.Where(node => node.Level == tree.LevelCount - treeDepth - 1)
                    .Select(node => new TerrainLodPatchSelection(node, 0)).ToArray();
                if (selected.Length != side * side) throw new InvalidDataException("Incomplete source LOD layer.");
            }
            else selected = VerifySelection(tree, explicitSelection);
            TerrainPatchInput? firstInput = initialHeight is null ? null : TerrainPatchInputMath.Ordinary(tree, selected[0].Node,
                initialHeight.HeightBinding.Image.Width, initialHeight.ControlBinding.Image.Width);
            int n = firstInput is null ? 65 : checked((int)firstInput.GridSize), cells = n - 1;
            if (height.CellWidth != height.CellHeight ||
                (double)height.CellWidth * height.SampleSpacing != tree.RootSize)
                throw new InvalidDataException("Height grid cannot use this tree's source sampled patch layout.");
            int[] indices = TerrainPatchInputMath.TriangleIndices(n).Select(value => (int)value).ToArray();
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
            foreach (TerrainLodPatchSelection selection in selected)
            {
                SpatialTreeNode node = selection.Node;
                int side = 1 << (tree.LevelCount - node.Level - 1);
                if (height.CellWidth % (side * cells) != 0)
                    throw new InvalidDataException("Height grid cannot sample this source LOD node.");
                int patchCells = height.CellWidth / side, step = patchCells / cells;
                int originColumn = node.GridX * patchCells, originRow = node.GridY * patchCells;
                TerrainPatchInput? input = initialHeight is null ? null : TerrainPatchInputMath.Ordinary(tree, node,
                    initialHeight.HeightBinding.Image.Width, initialHeight.ControlBinding.Image.Width, selection.EdgeFlags);
                var vertices = new Vector3[n * n];
                var normals = new Vector3[vertices.Length];
                var uvs = input is null ? null : new Vector2[vertices.Length];
                var phases = input is null ? null : new Vector2[vertices.Length];
                var shaderPatchInputs = input is null ? null : new float[vertices.Length * 3];
                var tangents = input is null ? null : new float[vertices.Length * 4];
                var packed = new uint[vertices.Length];
                double minimum = double.PositiveInfinity, maximum = double.NegativeInfinity;
                for (int row = 0; row < n; row++)
                    for (int column = 0; column < n; column++)
                    {
                        int sourceColumn = originColumn + column * step, sourceRow = originRow + row * step;
                        double y = height.GetHeight(sourceColumn, sourceRow);
                        double sampledRangeHeight = y;
                        int index = row * n + column;
                        packed[index] = height.GetPackedSample(sourceColumn, sourceRow);
                        if (input is not null && initialHeight is not null)
                        {
                            System.Numerics.Vector2 local = TerrainPatchInputMath.LocalCoordinates(input, index);
                            System.Numerics.Vector2 uv = TerrainPatchInputMath.HeightUv(input, local);
                            float h = OriginalTerrainInitialHeightResources.SamplePointClamp(initialHeight.Height, uv);
                            System.Numerics.Vector3 p = input.Origin + input.Extent * new System.Numerics.Vector3(local.X, h, local.Y);
                            System.Numerics.Vector3 normal = OriginalTerrainInitialHeightResources.Normal(initialHeight.Height, uv, initialHeight.HeightNormalInfo);
                            System.Numerics.Vector3 tangent = System.Numerics.Vector3.Normalize(new(0, normal.Z, -normal.Y));
                            vertices[index] = new(p.X, p.Y, p.Z);
                            normals[index] = new(normal.X, normal.Y, normal.Z);
                            uvs![index] = new(uv.X, uv.Y);
                            phases![index] = new(local.X, local.Y);
                            shaderPatchInputs![index * 3] = input.ControlFrequency;
                            shaderPatchInputs[index * 3 + 1] = input.ControlCount;
                            // Original producer sends non-leaf levels to group2;
                            // leaf groups0/1 share this ordinary non-tessellated surface path.
                            shaderPatchInputs[index * 3 + 2] = node.Level == 0 ? 0 : 2;
                            tangents![index * 4] = tangent.X; tangents[index * 4 + 1] = tangent.Y;
                            tangents[index * 4 + 2] = tangent.Z; tangents[index * 4 + 3] = 1;
                            y = p.Y;
                            // Source node extrema describe its regular sample grid.
                            // Stitching can remove a boundary extremum from the visible mesh.
                            if (input.EdgeFlags == 0) sampledRangeHeight = y;
                            else
                            {
                                var regularUv = TerrainPatchInputMath.HeightUv(input,
                                    new System.Numerics.Vector2((float)column / cells, (float)row / cells));
                                sampledRangeHeight = input.Origin.Y + input.Extent.Y *
                                    OriginalTerrainInitialHeightResources.SamplePointClamp(initialHeight.Height, regularUv);
                            }
                        }
                        else
                        {
                            vertices[index] = new Vector3(sourceColumn * height.SampleSpacing, (float)y, sourceRow * height.SampleSpacing);
                            int west = Math.Max(0, sourceColumn - step), east = Math.Min(height.CellWidth, sourceColumn + step);
                            int north = Math.Max(0, sourceRow - step), south = Math.Min(height.CellHeight, sourceRow + step);
                            float slopeX = (float)((height.GetHeight(west, sourceRow) - height.GetHeight(east, sourceRow)) /
                                ((east - west) * height.SampleSpacing));
                            float slopeZ = (float)((height.GetHeight(sourceColumn, north) - height.GetHeight(sourceColumn, south)) /
                                ((south - north) * height.SampleSpacing));
                            normals[index] = new Vector3(slopeX, 1, slopeZ).Normalized();
                        }
                        if (!vertices[index].IsFinite()) throw new InvalidDataException($"Node {node.Index} preview coordinates are not finite.");
                        if (!normals[index].IsFinite()) throw new InvalidDataException($"Node {node.Index} preview normal is not finite.");
                        minimum = Math.Min(minimum, sampledRangeHeight); maximum = Math.Max(maximum, sampledRangeHeight);
                    }
                if (Math.Abs(minimum - node.SampleMinimum) > 0.006 || Math.Abs(maximum - node.SampleMaximum) > 0.006)
                    throw new InvalidDataException($"Node {node.Index} LOD extrema disagree with {loaded.HeightField.Source.ResourceUri}.");
                var mesh = new ArrayMesh { ResourceName = $"SourceTerrain_{loaded.HeightField.Source.FileId:X8}_Node{node.Index}" };
                patches.Add(new(mesh, node, originColumn, originRow, step, Array.AsReadOnly(packed)));
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
                mesh.SetMeta("source_explicit_lod_selection", explicitSelection is not null);
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
                mesh.SetMeta("preview_convention", explicitSelection is null ?
                    "column +X; row +Z; uniform source LOD; original checkerboard diagonal; unlit material preview" :
                    "explicit source nodes and stitch flags; original checkerboard diagonal; selection point/visibility owned by caller; unlit material preview");
            }
            return new(loaded, patches.AsReadOnly(), treeDepth, material, shader);
        }
        catch (Exception error)
        {
            foreach (OriginalTerrainPatch patch in patches) patch.Mesh.Dispose();
            material?.Dispose(); shader?.Dispose();
            throw new InvalidDataException($"Original terrain mesh {context}: {error.Message}", error);
        }
    }

    internal static TerrainLodPatchSelection[] VerifySelection(SpatialTreeDocument tree,
        IReadOnlyList<TerrainLodPatchSelection> selection)
    {
        if (selection.Count == 0) throw new InvalidDataException("Terrain mesh requires a nonempty explicit node selection.");
        var result = new TerrainLodPatchSelection[selection.Count];
        var indices = new HashSet<int>();
        for (int i = 0; i < selection.Count; i++)
        {
            TerrainLodPatchSelection row = selection[i] ?? throw new InvalidDataException("Null terrain selection row.");
            SpatialTreeNode supplied = row.Node ?? throw new InvalidDataException("Null selected terrain node.");
            if ((uint)supplied.Index >= tree.Nodes.Count || !indices.Add(supplied.Index) || (row.EdgeFlags & 0xFFFF0000) != 0)
                throw new InvalidDataException("Invalid, repeated or unsupported selected terrain node.");
            SpatialTreeNode source = tree.Nodes[supplied.Index];
            if (supplied.Level != source.Level || supplied.GridX != source.GridX || supplied.GridY != source.GridY ||
                Bits(supplied.Size) != Bits(source.Size) || Bits(supplied.SampleMinimum) != Bits(source.SampleMinimum) ||
                Bits(supplied.SampleMaximum) != Bits(source.SampleMaximum) || supplied.Identifier20 != source.Identifier20 ||
                !supplied.RawBytes.SequenceEqual(source.RawBytes))
                throw new InvalidDataException("Selected terrain node differs from its recorded source.");
            result[i] = new(source, row.EdgeFlags);
        }
        foreach (int index in indices)
            for (int parent = index; parent > 0;)
            {
                parent = (parent - 1) / 4;
                if (indices.Contains(parent))
                    throw new InvalidDataException("Selected terrain ancestors and descendants overlap.");
            }
        return result;
    }

    private static (MhkHeightField, SpatialTreeDocument, DokProperty, DokProperty) Verify(LoadedTerrainResources loaded)
    {
        foreach (LoadedRdbAsset source in new[] { loaded.Objects.Source, loaded.SpatialSource, loaded.HeightField.Source })
            if (!Convert.ToHexString(SHA256.HashData(source.Bytes)).Equals(source.DecodedSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Source bytes differ from the recorded hash: {source.ResourceUri}.");
        DokDocument objects = DokReader.Read(loaded.Objects.Source.Bytes);
        if (objects.FormatCode != loaded.Objects.Document.FormatCode || objects.Identifier != loaded.Objects.Document.Identifier ||
            !objects.RawHeader.SequenceEqual(loaded.Objects.Document.RawHeader) || objects.Objects.Count != loaded.Objects.Document.Objects.Count)
            throw new InvalidDataException("Terrain object document differs from its source.");
        for (int i = 0; i < objects.Objects.Count; i++) VerifyObject(objects.Objects[i], loaded.Objects.Document.Objects[i]);
        DokObject[] roots = objects.Objects.Where(value => value.Kind == DokObjectKind.Inline && value.ClassId == 0x774DDC07).ToArray();
        if (roots.Length != 1) throw new InvalidDataException("Terrain requires one original root.");
        DokObject root = roots[0];
        uint fieldId = Reference(root, 0xE9CC029E);
        DokObject[] fields = objects.Objects.Where(value => value.ObjectId == fieldId).ToArray();
        if (fields.Length != 1 || fields[0].Kind != DokObjectKind.Inline || fields[0].ClassId != 0x468415D9)
            throw new InvalidDataException("Terrain root field reference is unsupported.");
        DokObject field = fields[0];
        VerifyObject(root, loaded.RootObject); VerifyObject(field, loaded.FieldObject);
        foreach (var reference in new[] { (Reference(field, 0xF981BC3D), loaded.SpatialSource), (Reference(field, 0xF17661E1), loaded.HeightField.Source) })
            if (reference.Item1 != reference.Item2.FileId || GameResourceId.Rdb("FieldEditor4", reference.Item1).ToString() != reference.Item2.ResourceUri)
                throw new InvalidDataException("Terrain field references do not match the supplied height/tree sources.");
        DokProperty rootPosition = Position(root), fieldPosition = Position(field);
        MhkHeightField height = MhkHeightFieldReader.Read(loaded.HeightField.Source.Bytes), suppliedHeight = loaded.HeightField.HeightField;
        if (height.CellWidth != suppliedHeight.CellWidth || height.CellHeight != suppliedHeight.CellHeight ||
            Bits(height.SampleSpacing) != Bits(suppliedHeight.SampleSpacing) || Bits(height.MinimumHeight) != Bits(suppliedHeight.MinimumHeight) ||
            Bits(height.MaximumHeight) != Bits(suppliedHeight.MaximumHeight) || !height.RawHeader.SequenceEqual(suppliedHeight.RawHeader) ||
            !height.PackedSamples.SequenceEqual(suppliedHeight.PackedSamples))
            throw new InvalidDataException("Provided height samples differ from their recorded source.");
        SpatialTreeDocument tree = SpatialTreeReader.Read(loaded.SpatialSource.Bytes), suppliedTree = loaded.SpatialTree;
        if (tree.LevelCount != suppliedTree.LevelCount || Bits(tree.RootSize) != Bits(suppliedTree.RootSize) ||
            !tree.RawHeader.SequenceEqual(suppliedTree.RawHeader) || tree.Nodes.Count != suppliedTree.Nodes.Count)
            throw new InvalidDataException("Provided tree differs from its recorded source.");
        for (int i = 0; i < tree.Nodes.Count; i++)
        {
            SpatialTreeNode a = tree.Nodes[i], b = suppliedTree.Nodes[i];
            if (a.Index != b.Index || a.Level != b.Level || a.GridX != b.GridX || a.GridY != b.GridY || a.Identifier20 != b.Identifier20 ||
                Bits(a.SampleMinimum) != Bits(b.SampleMinimum) || Bits(a.SampleMaximum) != Bits(b.SampleMaximum) ||
                Bits(a.Size) != Bits(b.Size) || !a.RawBytes.SequenceEqual(b.RawBytes))
                throw new InvalidDataException($"Provided tree node {i} differs from its recorded source.");
        }
        return (height, tree, rootPosition, fieldPosition);
    }

    private static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
    private static DokProperty Property(DokObject item, uint hash, uint type, int count)
    {
        DokProperty[] values = item.Properties.Where(value => value.NameHash == hash).ToArray();
        if (values.Length != 1 || values[0].TypeCode != type || values[0].ElementCount != count ||
            values[0].ElementSize != 4 || values[0].RawValue.Length != count * 4)
            throw new InvalidDataException($"Terrain object {item.ObjectId:X8} requires unique property {hash:X8} with type {type} and {count} values.");
        return values[0];
    }
    private static uint Reference(DokObject item, uint hash) => BinaryPrimitives.ReadUInt32LittleEndian(Property(item, hash, 5, 1).RawValue);
    private static DokProperty Position(DokObject item)
    {
        DokProperty property = Property(item, 0x27D889E5, 8, 3);
        for (int i = 0; i < 3; i++)
            if (BinaryPrimitives.ReadSingleLittleEndian(property.RawValue.AsSpan(i * 4, 4)) != 0)
                throw new InvalidDataException($"Terrain object {item.ObjectId:X8}: nonzero or nonfinite WorldPosition has no verified preview adapter.");
        return property;
    }
    private static Godot.Collections.Dictionary PropertyMetadata(DokProperty value) => new()
    {
        ["name_hash"] = value.NameHash, ["descriptor_offset"] = value.DescriptorOffset,
        ["value_offset"] = value.ValueOffset, ["raw_descriptor"] = value.RawDescriptor, ["raw_value"] = value.RawValue
    };
    private static void VerifyObject(DokObject a, DokObject b)
    {
        if (a.Index != b.Index || a.Offset != b.Offset || a.Length != b.Length || a.ObjectId != b.ObjectId || a.ClassId != b.ClassId ||
            a.Kind != b.Kind || a.Reference != b.Reference || !a.RawHeader.SequenceEqual(b.RawHeader) || !a.Padding.SequenceEqual(b.Padding) ||
            a.Properties.Count != b.Properties.Count)
            throw new InvalidDataException($"Provided terrain object {a.ObjectId:X8} differs from its source.");
        for (int i = 0; i < a.Properties.Count; i++)
        {
            DokProperty x = a.Properties[i], y = b.Properties[i];
            if (x.Index != y.Index || x.DescriptorOffset != y.DescriptorOffset || x.ValueOffset != y.ValueOffset || x.TypeCode != y.TypeCode ||
                x.ElementCount != y.ElementCount || x.NameHash != y.NameHash || x.ElementSize != y.ElementSize ||
                !x.RawDescriptor.SequenceEqual(y.RawDescriptor) || !x.RawValue.SequenceEqual(y.RawValue))
                throw new InvalidDataException($"Provided terrain object {a.ObjectId:X8} property {x.NameHash:X8} differs from its source.");
        }
    }
}
