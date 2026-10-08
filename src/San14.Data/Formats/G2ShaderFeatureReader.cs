using System.Buffers.Binary;

namespace San14.Data.Formats;

public sealed record G2ShaderFeatureRecord(int Index, int SourceOffset, byte[] RawBytes);
public sealed record G2ShaderFeatureTable(int RecordWidth, DxbcRange SourceRange,
    IReadOnlyList<G2ShaderFeatureRecord> Records)
{
    public G2ShaderFeatureRecord? Match(ReadOnlySpan<byte> condition)
    {
        if (condition.Length != RecordWidth) throw new InvalidDataException("Shader feature condition width differs from the original matcher.");
        foreach (G2ShaderFeatureRecord row in Records)
            if (condition.SequenceEqual(row.RawBytes)) return row;
        return null;
    }
}

/// <summary>Original bytewise feature matcher; a miss is retained, not replaced by a guessed row.</summary>
public static class G2ShaderFeatureReader
{
    public static G2ShaderFeatureTable Read(ReadOnlySpan<byte> source)
    {
        G2ShaderContainer container = G2ShaderContainerReader.Read(source);
        G2ShaderCombinations combinations = G2ShaderCombinationReader.Read(source);
        uint count = U32(source, 0x60), width = U32(source, 0x64), start = U32(source, 0x90);
        long length = (long)count * width;
        if (width != 36 || count == 0 || count > combinations.Rows.Count || start < container.MetadataRange.Offset ||
            length > int.MaxValue || start > int.MaxValue || start + length > combinations.SourceRange.Offset)
            throw new InvalidDataException("Unsupported or overlapping original shader feature records.");
        var rows = new List<G2ShaderFeatureRecord>(checked((int)count));
        for (int i = 0; i < count; i++)
        {
            int at = checked((int)start + i * (int)width);
            rows.Add(new(i, at, source.Slice(at, (int)width).ToArray()));
        }
        return new((int)width, new((int)start, (int)length), rows.AsReadOnly());
    }
    private static uint U32(ReadOnlySpan<byte> bytes, int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(at, 4));
}
