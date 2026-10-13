using System.Buffers.Binary;

namespace San14.Data.Formats;

public sealed record G1ShaderFeatureRow(IReadOnlyList<uint> Words, ReadOnlyMemory<byte> RawBytes);

/// <summary>Read an identified original 48-byte G1S feature row, independently of its module or physical program.</summary>
public static class G1ShaderFeatureRowReader
{
    public const int WordCount = 12;
    public const int RecordWidth = WordCount * sizeof(uint);

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
