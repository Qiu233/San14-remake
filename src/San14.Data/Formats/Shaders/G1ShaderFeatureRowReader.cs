using System.Buffers.Binary;

namespace San14.Data.Formats;

public sealed record G1ShaderFeatureRow(IReadOnlyList<uint> Words, ReadOnlyMemory<byte> RawBytes);
public sealed record G1ShaderFixedPasses(G1ShaderFeatureRow Feature, IReadOnlyList<G2ShaderCombination> Passes);

/// <summary>Read an identified original 48-byte G1S feature row, independently of its module or physical program.</summary>
public static class G1ShaderFeatureRowReader
{
    public const int WordCount = 12;
    public const int RecordWidth = WordCount * sizeof(uint);

    /// <summary>Import a fixed single-feature pass directory without requiring
    /// a binding-variant table. Empty variant declarations may have zero width.</summary>
    public static G1ShaderFixedPasses ReadFixedPasses(ReadOnlySpan<byte> source,
        G2ShaderContainer container, G2ShaderCombinations combinations)
    {
        if (source.Length != container.SourceByteLength)
            throw new InvalidDataException("Fixed G1S directory source length differs from its imported container.");
        uint passes = BinaryPrimitives.ReadUInt32LittleEndian(container.RawHeader.AsSpan(0x3C, 4));
        if (passes == 0 || passes != combinations.Rows.Count ||
            BinaryPrimitives.ReadUInt32LittleEndian(container.RawHeader.AsSpan(0x48, 4)) != 1)
            throw new NotSupportedException("Fixed G1S directory requires one feature and exactly its declared pass rows.");
        for (int dimension = 1; dimension < 7; dimension++)
            if (BinaryPrimitives.ReadUInt32LittleEndian(container.RawHeader.AsSpan(0x48 + dimension * 4, 4)) > 1)
                throw new NotSupportedException("Fixed G1S directory has additional selection rows.");
        uint start = BinaryPrimitives.ReadUInt32LittleEndian(container.RawHeader.AsSpan(0x78, 4));
        long end = (long)start + RecordWidth;
        if (start < container.MetadataRange.Offset || end > combinations.SourceRange.Offset ||
            end > (long)container.MetadataRange.Offset + container.MetadataRange.Length)
            throw new InvalidDataException("Fixed G1S feature exceeds its declared metadata region.");
        return new(ReadRow(source.Slice(checked((int)start), RecordWidth)), combinations.Rows);
    }

    public static G1ShaderFeatureRow ReadRow(ReadOnlySpan<byte> source)
    {
        if (source.Length != RecordWidth)
            throw new InvalidDataException("An original G1S feature row must contain exactly 48 bytes.");
        var words = new uint[WordCount];
        for (int i = 0; i < words.Length; i++)
            words[i] = BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(i * 4, 4));
        return new(Array.AsReadOnly(words), source.ToArray());
    }
}
