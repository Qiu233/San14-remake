using System.Buffers.Binary;

namespace San14.Data.Formats;

public sealed record G1ShaderDimensionRecord(int Index, int SourceOffset, ReadOnlyMemory<byte> RawBytes);
public sealed record G1ShaderDimension(int DimensionIndex, uint RawCount, int RecordWidth,
    DxbcRange SourceRange, IReadOnlyList<G1ShaderDimensionRecord> Records)
{
    public bool HasCapability => RawCount != 0;
    public bool HasMultipleRows => RawCount > 1;
}

/// <summary>Imported selection declarations. Current indices and pass state
/// belong to the renderer, independently of this immutable resource data.</summary>
public sealed class G1ShaderSelectionDimensions
{
    internal G1ShaderSelectionDimensions(G1ShaderBindingTables bindings,
        IReadOnlyList<G1ShaderDimension> dimensions)
    {
        Bindings = bindings;
        Dimensions = dimensions;
        Features = Array.AsReadOnly(bindings.Features.Select(record => record.Feature).ToArray());
        Passes = Array.AsReadOnly(Enumerable.Range(0, checked((int)bindings.PassCount)).Select(index => (uint)index).ToArray());
    }
    public G1ShaderBindingTables Bindings { get; }
    public IReadOnlyList<G1ShaderDimension> Dimensions { get; }
    public IReadOnlyList<G1ShaderFeatureRow> Features { get; }
    public IReadOnlyList<uint> Passes { get; }
}

/// <summary>Reads the seven G1 selection tables from their declared counts and
/// offsets. Empty declarations remain empty; no matcher row is synthesized.</summary>
public static class G1ShaderDimensionReader
{
    private static readonly int[] Widths = [48, 4, 16, 8, 40, 4];

    public static G1ShaderSelectionDimensions Read(ReadOnlySpan<byte> source)
    {
        var container = G2ShaderContainerReader.Read(source);
        return Read(source, G1ShaderBindingReader.Read(source, G2ShaderCombinationReader.Read(source, container)),
            G1ShaderMatrixBindingReader.Read(source, container));
    }

    internal static G1ShaderSelectionDimensions Read(ReadOnlySpan<byte> source,
        G1ShaderBindingTables bindings, G1ShaderMatrixBindings matrices)
    {
        var dimensions = new G1ShaderDimension[7];
        long previousEnd = 256;
        for (int dimension = 0; dimension < dimensions.Length; dimension++)
        {
            uint rawCount = bindings.RawDimensionCounts[dimension];
            int width = dimension == 6 ? bindings.Variants.RecordWidth : Widths[dimension];
            uint rawOffset = BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(0x78 + dimension * 4, 4));
            long length = (long)rawCount * width;
            if (rawCount > int.MaxValue || rawOffset < previousEnd || rawOffset > int.MaxValue || length > int.MaxValue ||
                rawOffset + length > (long)bindings.Combinations.SourceRange.Offset)
                throw new InvalidDataException($"Shader dimension {dimension} exceeds its metadata range.");
            int start = (int)rawOffset;
            var records = new List<G1ShaderDimensionRecord>((int)rawCount);
            for (int index = 0; index < (int)rawCount; index++)
            {
                int offset = checked(start + index * width);
                ReadOnlyMemory<byte> raw = dimension switch
                {
                    0 => bindings.Features[index].Feature.RawBytes,
                    2 => matrices.Rows[index].RawBytes,
                    6 => bindings.Variants.Records[index].RawBytes,
                    _ => source.Slice(offset, width).ToArray()
                };
                records.Add(new(index, offset, raw));
            }
            dimensions[dimension] = new(dimension, rawCount, width,
                new(start, (int)length), records.AsReadOnly());
            previousEnd = rawOffset + length;
        }
        return new(bindings, Array.AsReadOnly(dimensions));
    }
}
