using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>A canonical selection in one imported spatial tree.</summary>
public sealed class TerrainMeshSelection
{
    private TerrainMeshSelection(SpatialTreeDocument tree, TerrainLodPatchSelection[] rows, int depth)
        => (Tree, Rows, TreeDepth) = (tree, Array.AsReadOnly(rows), depth);

    public SpatialTreeDocument Tree { get; }
    public IReadOnlyList<TerrainLodPatchSelection> Rows { get; }
    public int TreeDepth { get; }

    public static TerrainMeshSelection Uniform(SpatialTreeDocument tree, int depth)
    {
        ArgumentNullException.ThrowIfNull(tree);
        if ((uint)depth >= tree.LevelCount)
            throw new InvalidDataException($"Preview tree depth {depth} is outside the source levels.");
        int side = 1 << depth;
        TerrainLodPatchSelection[] rows = tree.Nodes.Where(node => node.Level == tree.LevelCount - depth - 1)
            .Select(node => new TerrainLodPatchSelection(node, 0)).ToArray();
        if (rows.Length != side * side) throw new InvalidDataException("Incomplete source LOD layer.");
        return new(tree, rows, depth);
    }

    /// <summary>Node indices identify rows in the supplied tree; the tree owns all node metadata.</summary>
    public static TerrainMeshSelection Explicit(SpatialTreeDocument tree,
        IReadOnlyList<TerrainLodPatchSelection> selection)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(selection);
        if (selection.Count == 0) throw new InvalidDataException("Terrain mesh requires a nonempty explicit node selection.");
        var rows = new TerrainLodPatchSelection[selection.Count];
        var indices = new HashSet<int>();
        for (int i = 0; i < rows.Length; i++)
        {
            TerrainLodPatchSelection row = selection[i] ?? throw new InvalidDataException("Null terrain selection row.");
            SpatialTreeNode node = row.Node ?? throw new InvalidDataException("Null selected terrain node.");
            if ((uint)node.Index >= tree.Nodes.Count || !indices.Add(node.Index) || (row.EdgeFlags & 0xFFFF0000) != 0)
                throw new InvalidDataException("Invalid, repeated or unsupported selected terrain node.");
            rows[i] = new(tree.Nodes[node.Index], row.EdgeFlags);
        }
        foreach (int index in indices)
            for (int parent = index; parent > 0;)
            {
                parent = (parent - 1) / 4;
                if (indices.Contains(parent))
                    throw new InvalidDataException("Selected terrain ancestors and descendants overlap.");
            }
        return new(tree, rows, -1);
    }

    /// <summary>Retain a subset without revalidating the already canonical selection.</summary>
    public TerrainMeshSelection Filter(Func<TerrainLodPatchSelection, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return new(Tree, Rows.Where(predicate).ToArray(), -1);
    }
}

/// <summary>Owned CPU arrays for a terrain patch, before any graphics allocation.</summary>
public sealed class PreparedTerrainMeshPatch
{
    internal PreparedTerrainMeshPatch(SpatialTreeNode node, int columnOrigin, int rowOrigin, int sampleStep,
        TerrainPatchInput? input, Vector3[] positions, Vector3[] normals, Vector2[] uvs, Vector2[] phases,
        float[] shaderPatchInputs, float[] tangents, uint[] packedSamples, int[] indices)
    {
        (Node, ColumnOrigin, RowOrigin, SampleStep, Input, Normals, Uvs, Phases,
            ShaderPatchInputs, Tangents, PackedSamples) = (node, columnOrigin, rowOrigin, sampleStep, input,
            normals, uvs, phases, shaderPatchInputs, tangents, packedSamples);
        Geometry = new(positions, indices);
        PackedSampleValues = Array.AsReadOnly(packedSamples);
    }

    public SpatialTreeNode Node { get; }
    public int ColumnOrigin { get; }
    public int RowOrigin { get; }
    public int SampleStep { get; }
    public TerrainPatchInput? Input { get; }
    public TerrainTriangleGeometry Geometry { get; }
    public ReadOnlyMemory<Vector3> Positions => Geometry.Positions;
    public ReadOnlyMemory<Vector3> Normals { get; }
    public ReadOnlyMemory<Vector2> Uvs { get; }
    public ReadOnlyMemory<Vector2> Phases { get; }
    public ReadOnlyMemory<float> ShaderPatchInputs { get; }
    public ReadOnlyMemory<float> Tangents { get; }
    public ReadOnlyMemory<uint> PackedSamples { get; }
    /// <summary>A retained immutable view of the producer-owned samples. It keeps the array
    /// alive for CPU consumers without copying or depending on the preparation packet's lifetime.</summary>
    public IReadOnlyList<uint> PackedSampleValues { get; }
}

/// <summary>CPU mesh preparation and the imported sources needed by its upload consumer.</summary>
public sealed class PreparedTerrainMesh
{
    internal PreparedTerrainMesh(LoadedTerrainResources source, LoadedTerrainInitialHeightResources? initialHeight,
        int treeDepth, int gridSize, DokProperty rootPosition, DokProperty fieldPosition,
        int[] indices, PreparedTerrainMeshPatch[] patches, bool includesRenderAttributes)
        => (Source, InitialHeight, TreeDepth, GridSize, RootPosition, FieldPosition, Indices, Patches, IncludesRenderAttributes) =
            (source, initialHeight, treeDepth, gridSize, rootPosition, fieldPosition, indices, Array.AsReadOnly(patches), includesRenderAttributes);

    public LoadedTerrainResources Source { get; }
    public LoadedTerrainInitialHeightResources? InitialHeight { get; }
    public int TreeDepth { get; }
    public int GridSize { get; }
    public DokProperty RootPosition { get; }
    public DokProperty FieldPosition { get; }
    public ReadOnlyMemory<int> Indices { get; }
    public IReadOnlyList<PreparedTerrainMeshPatch> Patches { get; }
    /// <summary>False for CPU geometry and picking packets, which cannot be uploaded as a rendered mesh.</summary>
    public bool IncludesRenderAttributes { get; }
}

/// <summary>Prepare source topology using either legacy MHK samples or the initial zero-brush height adapter.</summary>
public static class TerrainMeshPreparation
{
    // TriangleIndices validates a ushort-addressable square grid (2..sqrt(65536)).
    // This finite format catalogue owns immutable topology; it is independent of terrain,
    // node, stitch flags and camera. Public TriangleIndices still returns a fresh array.
    private static readonly Lazy<int[]>?[] SharedIndexStreams = CreateIndexStreams();
    private static Lazy<int[]>?[] CreateIndexStreams()
    {
        var streams = new Lazy<int[]>?[(int)Math.Sqrt(ushort.MaxValue + 1d) + 1];
        for (int grid = 2; grid < streams.Length; grid++)
        {
            int size = grid;
            streams[size] = new(() => Array.ConvertAll(TerrainPatchInputMath.TriangleIndices(size), static index => (int)index));
        }
        return streams;
    }
    private static int[] SharedIndices(int gridSize)
    {
        TerrainPatchInputMath.ValidateGrid(gridSize);
        return SharedIndexStreams[gridSize]!.Value;
    }
    public static PreparedTerrainMesh Prepare(LoadedTerrainResources loaded,
        LoadedTerrainInitialHeightResources? initialHeight, int treeDepth = 2)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        return Prepare(loaded, initialHeight, TerrainMeshSelection.Uniform(loaded.SpatialTree, treeDepth));
    }

    public static PreparedTerrainMesh Prepare(LoadedTerrainResources loaded,
        LoadedTerrainInitialHeightResources? initialHeight, TerrainMeshSelection selection)
        => Prepare(loaded, initialHeight, selection, includesRenderAttributes: true);

    /// <summary>Prepare the identical source triangles and packed samples for CPU consumers,
    /// without allocating shader-only vertex streams that those consumers do not retain.</summary>
    public static PreparedTerrainMesh PrepareGeometry(LoadedTerrainResources loaded,
        LoadedTerrainInitialHeightResources? initialHeight, int treeDepth = 2)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        return PrepareGeometry(loaded, initialHeight, TerrainMeshSelection.Uniform(loaded.SpatialTree, treeDepth));
    }

    public static PreparedTerrainMesh PrepareGeometry(LoadedTerrainResources loaded,
        LoadedTerrainInitialHeightResources? initialHeight, TerrainMeshSelection selection)
        => Prepare(loaded, initialHeight, selection, includesRenderAttributes: false);

    private static PreparedTerrainMesh Prepare(LoadedTerrainResources loaded,
        LoadedTerrainInitialHeightResources? initialHeight, TerrainMeshSelection selection, bool includesRenderAttributes)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        ArgumentNullException.ThrowIfNull(selection);
        SpatialTreeDocument tree = loaded.SpatialTree;
        if (!ReferenceEquals(selection.Tree, tree))
            throw new InvalidDataException("Terrain mesh selection belongs to a different imported spatial tree.");
        if (selection.Rows.Count == 0) throw new InvalidDataException("Terrain mesh requires a nonempty node selection.");
        if (initialHeight is null && selection.Rows.Any(row => row.EdgeFlags != 0))
            throw new InvalidDataException("Stitched terrain patches require the initial-height surface adapter.");
        if (initialHeight is not null && initialHeight.Terrain != loaded)
            throw new InvalidDataException("Initial height belongs to a different terrain source.");
        MhkHeightField height = loaded.HeightField.HeightField;
        if (!float.IsFinite(height.MaximumHeight - height.MinimumHeight))
            throw new InvalidDataException("Height range exceeds finite float arithmetic in the grayscale preview material.");
        DokProperty rootPosition = Position(loaded.RootObject), fieldPosition = Position(loaded.FieldObject);
        TerrainPatchInput? firstInput = initialHeight is null ? null : TerrainPatchInputMath.Ordinary(tree,
            selection.Rows[0].Node, initialHeight.HeightBinding.Image.Width, initialHeight.ControlBinding.Image.Width);
        int n = firstInput is null ? 65 : checked((int)firstInput.GridSize), cells = n - 1;
        if (height.CellWidth != height.CellHeight || (double)height.CellWidth * height.SampleSpacing != tree.RootSize)
            throw new InvalidDataException("Height grid cannot use this tree's source sampled patch layout.");
        int[] indices = SharedIndices(n);
        var patches = new PreparedTerrainMeshPatch[selection.Rows.Count];
        for (int patchIndex = 0; patchIndex < patches.Length; patchIndex++)
        {
            TerrainLodPatchSelection selected = selection.Rows[patchIndex];
            SpatialTreeNode node = selected.Node;
            int side = 1 << (tree.LevelCount - node.Level - 1);
            if (height.CellWidth % (side * cells) != 0)
                throw new InvalidDataException("Height grid cannot sample this source LOD node.");
            int patchCells = height.CellWidth / side, step = patchCells / cells;
            int originColumn = node.GridX * patchCells, originRow = node.GridY * patchCells;
            TerrainPatchInput? input = initialHeight is null ? null : TerrainPatchInputMath.Ordinary(tree, node,
                initialHeight.HeightBinding.Image.Width, initialHeight.ControlBinding.Image.Width, selected.EdgeFlags);
            var positions = new Vector3[n * n];
            Vector3[] normals = includesRenderAttributes ? new Vector3[positions.Length] : [];
            Vector2[] uvs = includesRenderAttributes && input is not null ? new Vector2[positions.Length] : [];
            Vector2[] phases = includesRenderAttributes && input is not null ? new Vector2[positions.Length] : [];
            float[] shaderPatchInputs = includesRenderAttributes && input is not null ? new float[positions.Length * 3] : [];
            float[] tangents = includesRenderAttributes && input is not null ? new float[positions.Length * 4] : [];
            var packed = new uint[positions.Length];
            for (int row = 0; row < n; row++)
                for (int column = 0; column < n; column++)
                {
                    int sourceColumn = originColumn + column * step, sourceRow = originRow + row * step;
                    int index = row * n + column;
                    packed[index] = height.GetPackedSample(sourceColumn, sourceRow);
                    if (input is not null && initialHeight is not null)
                    {
                        Vector2 local = TerrainPatchInputMath.LocalCoordinates(input, index);
                        Vector2 uv = TerrainPatchInputMath.HeightUv(input, local);
                        float h = TerrainInitialHeightResources.SamplePointClamp(initialHeight.Height, uv);
                        positions[index] = input.Origin + input.Extent * new Vector3(local.X, h, local.Y);
                        if (includesRenderAttributes)
                        {
                            Vector3 normal = TerrainInitialHeightResources.Normal(initialHeight.Height, uv, initialHeight.HeightNormalInfo);
                            Vector3 tangent = Vector3.Normalize(new(0, normal.Z, -normal.Y));
                            normals[index] = normal;
                            uvs[index] = uv;
                            phases[index] = local;
                            shaderPatchInputs[index * 3] = input.ControlFrequency;
                            shaderPatchInputs[index * 3 + 1] = input.ControlCount;
                            // Non-leaf levels use the coarse material; leaf groups share the ordinary surface path.
                            shaderPatchInputs[index * 3 + 2] = node.Level == 0 ? 0 : 2;
                            tangents[index * 4] = tangent.X;
                            tangents[index * 4 + 1] = tangent.Y;
                            tangents[index * 4 + 2] = tangent.Z;
                            tangents[index * 4 + 3] = 1;
                        }
                    }
                    else
                    {
                        positions[index] = new(sourceColumn * height.SampleSpacing,
                            (float)height.GetHeight(sourceColumn, sourceRow), sourceRow * height.SampleSpacing);
                        if (includesRenderAttributes)
                        {
                            int west = Math.Max(0, sourceColumn - step), east = Math.Min(height.CellWidth, sourceColumn + step);
                            int north = Math.Max(0, sourceRow - step), south = Math.Min(height.CellHeight, sourceRow + step);
                            float slopeX = (float)((height.GetHeight(west, sourceRow) - height.GetHeight(east, sourceRow)) /
                                ((east - west) * height.SampleSpacing));
                            float slopeZ = (float)((height.GetHeight(sourceColumn, north) - height.GetHeight(sourceColumn, south)) /
                                ((south - north) * height.SampleSpacing));
                            // Match the existing scalar normalization order rather than SIMD dot-product reassociation.
                            float lengthSquared = slopeX * slopeX + 1f + slopeZ * slopeZ;
                            float length = MathF.Sqrt(lengthSquared);
                            normals[index] = new(slopeX / length, 1f / length, slopeZ / length);
                        }
                    }
                    if (!IsFinite(positions[index]))
                        throw new InvalidDataException($"Node {node.Index} preview coordinates are not finite.");
                    if (includesRenderAttributes && !IsFinite(normals[index]))
                        throw new InvalidDataException($"Node {node.Index} preview normal is not finite.");
                }
            patches[patchIndex] = new(node, originColumn, originRow, step, input, positions, normals,
                uvs, phases, shaderPatchInputs, tangents, packed, indices);
        }
        return new(loaded, initialHeight, selection.TreeDepth, n, rootPosition, fieldPosition, indices, patches, includesRenderAttributes);
    }

    private static bool IsFinite(Vector3 vector) =>
        float.IsFinite(vector.X) && float.IsFinite(vector.Y) && float.IsFinite(vector.Z);

    private static DokProperty Position(DokObject item)
    {
        const uint positionName = 0x27D889E5;
        DokProperty[] values = item.Properties.Where(value => value.NameHash == positionName).ToArray();
        if (values.Length != 1 || values[0].TypeCode != 8 || values[0].ElementCount != 3 ||
            values[0].ElementSize != 4 || values[0].RawValue.Length != 12)
            throw new InvalidDataException($"Terrain object {item.ObjectId:X8} requires a unique float3 WorldPosition.");
        DokProperty property = values[0];
        for (int i = 0; i < 3; i++)
            if (BinaryPrimitives.ReadSingleLittleEndian(property.RawValue.AsSpan(i * 4, 4)) != 0)
                throw new InvalidDataException($"Terrain object {item.ObjectId:X8}: nonzero or nonfinite WorldPosition has no supported preview adapter.");
        return property;
    }
}
