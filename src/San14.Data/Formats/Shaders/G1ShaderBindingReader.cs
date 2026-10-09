using System.Buffers.Binary;

namespace San14.Data.Formats;

public sealed record G1ShaderFeatureRecord(int Index, int SourceOffset, G1ShaderFeatureRow Feature);
public sealed record G1ShaderVariantRecord(int Index, int SourceOffset, byte[] RawBytes);
public sealed record G1ShaderVariantTable(int RecordWidth, DxbcRange SourceRange,
    IReadOnlyList<G1ShaderVariantRecord> Records)
{
    /// <summary>First exact original variant match; a miss is kept, without applying the unresolved native fallback.</summary>
    public G1ShaderVariantRecord? Match(ReadOnlySpan<byte> condition)
    {
        if (condition.Length != RecordWidth)
            throw new InvalidDataException("G1S binding variant condition width differs from the original source.");
        foreach (G1ShaderVariantRecord record in Records)
            if (condition.SequenceEqual(record.RawBytes)) return record;
        return null;
    }
}

public sealed record G1ShaderBindingTables(uint PassCount, IReadOnlyList<uint> RawDimensionCounts,
    DxbcRange FeatureSourceRange, IReadOnlyList<G1ShaderFeatureRecord> Features,
    G1ShaderVariantTable Variants, G2ShaderCombinations Combinations)
{
    /// <summary>Resolve seven explicit source indices and a pass to an original combination row.
    /// Additional dimensions are caller inputs; their constructor values are not assumed.</summary>
    public G2ShaderCombination ResolveCombination(ReadOnlySpan<uint> indices, uint passIndex)
    {
        if (indices.Length != 7 || RawDimensionCounts.Count != 7 || passIndex >= PassCount)
            throw new InvalidDataException("G1S combination requires seven explicit dimensions and a valid pass index.");
        long flat = 0;
        for (int i = 0; i < indices.Length; i++)
        {
            uint count = Math.Max(RawDimensionCounts[i], 1u);
            if (indices[i] >= count)
                throw new InvalidDataException($"G1S combination dimension {i} exceeds its original source count.");
            if (flat > (int.MaxValue - (long)indices[i]) / count)
                throw new InvalidDataException("G1S combination index exceeds supported source bounds.");
            flat = flat * count + indices[i];
        }
        if (flat > (int.MaxValue - (long)passIndex) / PassCount)
            throw new InvalidDataException("G1S pass combination index exceeds supported source bounds.");
        flat = flat * PassCount + passIndex;
        if (flat >= Combinations.Rows.Count)
            throw new InvalidDataException("G1S combination index exceeds the validated source table.");
        return Combinations.Rows[(int)flat];
    }
}

/// <summary>Original G1S feature, binding-variant and combination tables in the current S2G3000 source.
/// Module+30 is the source origin; weighted feature and exact variant selection remain separate.</summary>
public static class G1ShaderBindingReader
{
    public static G1ShaderBindingTables Read(ReadOnlySpan<byte> source)
    {
        // Six stage tables, DXBC ranges and combinations share the existing
        // S2G container layout. Its name reflects the earlier G2 consumer.
        G2ShaderCombinations combinations = G2ShaderCombinationReader.Read(source);
        var dimensions = new uint[7];
        for (int i = 0; i < dimensions.Length; i++) dimensions[i] = U32(source, 0x48 + i * 4);
        uint passes = U32(source, 0x3C);
        long expected = passes;
        foreach (uint raw in dimensions)
        {
            uint factor = Math.Max(raw, 1u);
            if (expected > combinations.Rows.Count / (long)factor)
                throw new InvalidDataException("G1S dimension product exceeds its original combination table.");
            expected *= factor;
        }
        if (passes == 0 || expected != combinations.Rows.Count)
            throw new InvalidDataException("G1S pass and dimension product differs from its original combination row count.");

        uint featureStart = U32(source, 0x78), variantStart = U32(source, 0x90), variantWidth = U32(source, 0x64);
        if (variantWidth == 0 || variantWidth > int.MaxValue || featureStart > variantStart)
            throw new InvalidDataException("Unsupported original G1S feature/variant order or width.");
        DxbcRange featureRange = Range(featureStart, dimensions[0], G1ShaderFeatureRowReader.RecordWidth, variantStart);
        DxbcRange variantRange = Range(variantStart, dimensions[6], (int)variantWidth, (uint)combinations.SourceRange.Offset);
        var features = new List<G1ShaderFeatureRecord>((int)dimensions[0]);
        for (int i = 0; i < (int)dimensions[0]; i++)
        {
            int at = featureRange.Offset + i * G1ShaderFeatureRowReader.RecordWidth;
            features.Add(new(i, at, G1ShaderFeatureRowReader.ReadRow(source.Slice(at, G1ShaderFeatureRowReader.RecordWidth))));
        }
        var variants = new List<G1ShaderVariantRecord>((int)dimensions[6]);
        for (int i = 0; i < (int)dimensions[6]; i++)
        {
            int at = variantRange.Offset + i * (int)variantWidth;
            variants.Add(new(i, at, source.Slice(at, (int)variantWidth).ToArray()));
        }
        return new(passes, Array.AsReadOnly(dimensions), featureRange, features.AsReadOnly(),
            new((int)variantWidth, variantRange, variants.AsReadOnly()), combinations);
    }

    private static DxbcRange Range(uint start, uint count, int width, uint end)
    {
        long length = (long)count * width;
        if (start < 256 || start > int.MaxValue || length > int.MaxValue || start + length > end)
            throw new InvalidDataException("Original G1S feature/variant source range exceeds its metadata boundary.");
        return new((int)start, (int)length);
    }

    private static uint U32(ReadOnlySpan<byte> source, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(offset, 4));
}
