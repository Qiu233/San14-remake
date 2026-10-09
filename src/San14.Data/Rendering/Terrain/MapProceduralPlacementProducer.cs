using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>BC6540's actual instancing bank: three float4 affine rows, with
/// translation in each W. Keep this separate from Matrix4x4's row-vector ABI.</summary>
public readonly record struct ProceduralAffine48(Vector4 Row0, Vector4 Row1, Vector4 Row2)
{
    public Vector3 Position => new(Row0.W, Row1.W, Row2.W);
    public void Write(Span<byte> destination)
    {
        if (destination.Length < 48) throw new ArgumentException("Affine destination needs 48 bytes.");
        WriteRow(destination, Row0); WriteRow(destination[16..], Row1); WriteRow(destination[32..], Row2);
    }
    private static void WriteRow(Span<byte> output, Vector4 v)
    {
        BinaryPrimitives.WriteSingleLittleEndian(output, v.X);
        BinaryPrimitives.WriteSingleLittleEndian(output[4..], v.Y);
        BinaryPrimitives.WriteSingleLittleEndian(output[8..], v.Z);
        BinaryPrimitives.WriteSingleLittleEndian(output[12..], v.W);
    }
}

public sealed record ProceduralInstance(int BlockX, int BlockZ, int BiomeIndex,
    int ElementIndex, int TemplateIndex, int CellIndex, int CellOrdinal, float Scale,
    float Yaw, ProceduralAffine48 Affine);
public sealed record ProceduralElementBank(int ElementIndex, int TemplateIndex,
    IReadOnlyList<ProceduralInstance> Instances);
public sealed record ProceduralBlock(int X, int Z,
    IReadOnlyList<ProceduralElementBank> Banks)
{
    public int InstanceCount => Banks.Sum(bank => bank.Instances.Count);
    public string SourceEvidence => "CE:BC6540;ResourceMap/DensityMap;biome-seed;LCG10DCD;" +
        "element-reset/cell-step/shuffle-stream/placement-stream;instancing48";
}
public sealed record ProceduralTemplateBounds(int TemplateIndex, Vector3 Minimum, Vector3 Maximum)
{
    /// <summary>DAE530, fresh unparented scale-one template. The preceding
    /// ModelCPU getter retains its two source float32 rounding stages.</summary>
    public static ProceduralTemplateBounds FreshScaleOne(int templateIndex,
        MapModelNativePreparedBounds resource)
    {
        Vector3 lo = resource.GetterMinimum, hi = resource.GetterMaximum;
        Vector3 center = new(M(A(hi.X, lo.X), .5f), M(A(lo.Y, hi.Y), .5f), M(A(lo.Z, hi.Z), .5f));
        float epsilon = MapModelNativeBoundsSources.MinimumHalfExtent;
        Vector3 extent = new(MathF.Max(M(S(hi.X, lo.X), .5f), epsilon),
            MathF.Max(M(S(hi.Y, lo.Y), .5f), epsilon), MathF.Max(M(S(hi.Z, lo.Z), .5f), epsilon));
        return new(templateIndex, new(S(center.X, extent.X), S(center.Y, extent.Y), S(center.Z, extent.Z)),
            new(A(center.X, extent.X), A(center.Y, extent.Y), A(center.Z, extent.Z)));
    }
    [MethodImpl(MethodImplOptions.NoInlining)] private static float A(float a, float b) => a + b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float S(float a, float b) => a - b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float M(float a, float b) => a * b;
}

/// <summary>The finite actual CE field producer. Import the declaration and
/// model bounds once, retain this object, and generate/cache requested blocks.
/// Generation uses the original cell order and independently seeded random
/// streams. Runtime current-view admission is the separate BB8710 producer.</summary>
public sealed class MapProceduralPlacementProducer
{
    private readonly ProceduralPlacementDeclaration declaration;
    private readonly ProceduralTemplateBounds[] bounds;
    private readonly int[] elementTemplates;
    private readonly float[] density;
    private readonly byte[] biomeMap;
    private readonly Func<float, float, float> height;
    public MapProceduralPlacementProducer(ProceduralPlacementDeclaration source,
        IReadOnlyList<ProceduralTemplateBounds> actualTemplateBounds,
        Func<float, float, float> actualTerrainHeight)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(actualTemplateBounds);
        ArgumentNullException.ThrowIfNull(actualTerrainHeight);
        declaration = source; height = actualTerrainHeight;
        if (source.TerrainType != 2 || source.GlobalFlags != 0 ||
            source.Biomes.Any(b => b.DensityMapMode != 0) ||
            source.Elements.Any(e => e.Type != 0 || e.Flags != 8 || e.Parameters.Count != 9 ||
                e.Parameters.Skip(3).Any(v => v != 0f)))
            throw new NotSupportedException("BC6540 producer requires the actual CE FieldScene finite source profile.");
        bounds = actualTemplateBounds.OrderBy(b => b.TemplateIndex).ToArray();
        if (bounds.Length != source.Templates.Count || bounds.Where((b, i) => b.TemplateIndex != i).Any())
            throw new InvalidDataException("BC6540 requires the original complete template-bound directory.");
        elementTemplates = source.Elements.Select(e => source.Templates.Single(t => t.NameHash == e.DisplaySetNameHash).Index).ToArray();
        if (elementTemplates.Any(i => !source.Templates[i].IsInstancing))
            throw new NotSupportedException("Finite source producer requires the actual active instancing templates.");
        biomeMap = source.ResourceMapBytes.ToArray();
        density = new float[source.DensityMapBytes.Length / 4];
        for (int i = 0; i < density.Length; i++)
            density[i] = BinaryPrimitives.ReadSingleLittleEndian(source.DensityMapBytes.Span[(i * 4)..]);
    }

    public ProceduralBlock GenerateBlock(int blockX, int blockZ)
    {
        if ((uint)blockX >= declaration.BlocksX || (uint)blockZ >= declaration.BlocksZ)
            throw new ArgumentOutOfRangeException(nameof(blockX));
        var banks = declaration.Elements.Select((e, i) => new List<ProceduralInstance>()).ToArray();
        float cellSize = declaration.CellSize;
        uint startX = (uint)blockX * declaration.CellsPerBlockX, startZ = (uint)blockZ * declaration.CellsPerBlockZ;
        foreach (var biome in declaration.Biomes)
        {
            uint random = unchecked(biome.RandomSeed * 0x35Cu + 0x1534FFu + (uint)blockX * 0x47u +
                (unchecked((uint)blockZ * 0x6A836u) >> 3));
            uint advance = unchecked((uint)blockZ * 3u + (uint)blockX * 2u) % 13u;
            for (uint n = 0; n < advance; n++) random = Step(random);
            uint first = Step(random);
            uint elementSalt = (uint)M(first >> 16, Unit(Step(first)));
            foreach (uint componentIndex in biome.ComponentIndices)
            foreach (uint elementIndex in declaration.Components[(int)componentIndex].ElementIndices)
            {
                var element = declaration.Elements[(int)elementIndex];
                random = unchecked(element.RandomSeed + elementSalt);
                var candidates = new List<(int Cell, int Ordinal)>();
                for (uint z = startZ; z < startZ + declaration.CellsPerBlockZ; z++)
                for (uint x = startX; x < startX + declaration.CellsPerBlockX; x++)
                {
                    random = Step(random); // consumed for every cell, including other biomes
                    int cell = checked((int)(z * declaration.CellsX + x));
                    if (biomeMap[cell] != biome.Index) continue;
                    float amount = D(M(M(M(density[cell], cellSize), cellSize), element.Parameters[0]), 10000f);
                    uint count = (uint)amount;
                    if (Unit(random) < S(amount, count)) count++;
                    for (int ordinal = 0; ordinal < count; ordinal++) candidates.Add((cell, ordinal));
                }
                uint fullShuffleSeed = Step(random), shuffle = fullShuffleSeed >> 16;
                for (int i = 0; i < candidates.Count - 1; i++)
                {
                    shuffle = Step(shuffle);
                    int j = (int)((shuffle >> 16) % (uint)(candidates.Count - i)) + i;
                    (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
                }
                uint placementSalt = Step(fullShuffleSeed) >> 16;
                int template = elementTemplates[element.Index];
                var modelBounds = bounds[template];
                // BC6540 uses (maximum-minimum)*.5+minimum, with no yaw rotation.
                float anchorX = A(M(S(modelBounds.Maximum.X, modelBounds.Minimum.X), .5f), modelBounds.Minimum.X);
                float anchorZ = A(M(S(modelBounds.Maximum.Z, modelBounds.Minimum.Z), .5f), modelBounds.Minimum.Z);
                foreach (var candidate in candidates)
                {
                    uint x = (uint)candidate.Cell % declaration.CellsX, z = (uint)candidate.Cell / declaration.CellsX;
                    random = unchecked(x * 123u + z * 456u + placementSalt + (uint)candidate.Ordinal * 789u);
                    advance = (uint)A(A(M(x, 1.2f), z), candidate.Ordinal * 12) % 13u;
                    for (uint n = 0; n < advance; n++) random = Step(random);
                    random = Step(random);
                    float scale = A(M(Unit(random), S(element.Parameters[2], element.Parameters[1])), element.Parameters[1]);
                    scale = MathF.Max(scale, 1.1920928955078125e-5f);
                    random = Step(random);
                    float yawHalf = M(Unit(random), 3.1415927410125732f), yaw = A(yawHalf, yawHalf);
                    // 1C6690 Euler(0,yaw,0), using its actual scalar 014590 polynomial.
                    var quaternion = new UnitGeometry().NativeQuaternion1C58E0(new Vector3(0f,yaw,0f));
                    float qx=quaternion.X,qy=quaternion.Y,qz=quaternion.Z,qw=quaternion.W;
                    random = Step(random);
                    float worldX = A(S(M(A(x, Unit(random)), cellSize), M(scale, anchorX)), declaration.FieldOffsetX);
                    random = Step(random);
                    float worldZ = A(S(M(A(Unit(random), z), cellSize), M(anchorZ, scale)), declaration.FieldOffsetZ);
                    float margin = M(cellSize, .001f);
                    worldX = Math.Clamp(worldX, A(declaration.FieldOffsetX, margin),
                        S(A(M(declaration.CellsX, cellSize), declaration.FieldOffsetX), margin));
                    worldZ = Math.Clamp(worldZ, A(declaration.FieldOffsetZ, margin),
                        S(A(M(declaration.CellsZ, cellSize), declaration.FieldOffsetZ), margin));
                    float worldY = height(worldX, worldZ);
                    float twiceX=M(qx,2f),twiceY=M(qy,2f),twiceZ=M(qz,2f);
                    // Keep all original zero products: Euler yaw can produce -0.
                    var affine = new ProceduralAffine48(
                        new(M(S(S(1f,M(qy,twiceY)),M(qz,twiceZ)),scale),
                            M(S(M(qy,twiceX),M(qw,twiceZ)),scale),
                            M(A(M(qx,twiceZ),M(qw,twiceY)),scale),worldX),
                        new(M(A(M(qy,twiceX),M(qw,twiceZ)),scale),
                            M(S(S(1f,M(qz,twiceZ)),M(qx,twiceX)),scale),
                            M(S(M(qz,twiceY),M(qw,twiceX)),scale),worldY),
                        new(M(S(M(qx,twiceZ),M(qw,twiceY)),scale),
                            M(A(M(qz,twiceY),M(qw,twiceX)),scale),
                            M(S(S(1f,M(qx,twiceX)),M(qy,twiceY)),scale),worldZ));
                    banks[element.Index].Add(new(blockX, blockZ, biome.Index, element.Index, template,
                        candidate.Cell, candidate.Ordinal, scale, yaw, affine));
                }
            }
        }
        return new(blockX, blockZ, Array.AsReadOnly(banks.Select((instances, i) =>
            new ProceduralElementBank(i, elementTemplates[i], Array.AsReadOnly(instances.ToArray()))).ToArray()));
    }

    private static uint Step(uint state) => unchecked(state * 0x10DCDu + 1u);
    private static float Unit(uint state) => D(state >> 16, 65535f);
    [MethodImpl(MethodImplOptions.NoInlining)] private static float A(float a, float b) => a + b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float S(float a, float b) => a - b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float M(float a, float b) => a * b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float D(float a, float b) => a / b;
}
